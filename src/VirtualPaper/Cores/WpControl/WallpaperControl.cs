using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files.Models;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.Shell;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.DataAssistor;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.lang;
using VirtualPaper.ML.DepthEstimate;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils;
using WinEventHook;
using static VirtualPaper.Common.Errors;

namespace VirtualPaper.Cores.WpControl {
    public partial class WallpaperControl : IWallpaperControl {
        public event EventHandler? WallpaperChanged;
        public event EventHandler<Exception>? WallpaperError;
        public event EventHandler? WallpaperReset;

        public nint DesktopWorkerW => _workerW;
        public ReadOnlyCollection<IWpPlayer> Wallpapers => _wallpapers.AsReadOnly();

        public WallpaperControl(
            IUserSettingsService userSettings,
            IMonitorManager monitorManager,
            IWallpaperFactory wallpaperFactory) {
            this._userSettings = userSettings;
            this._monitorManager = monitorManager;
            this._wallpaperFactory = wallpaperFactory;

            if (SystemParameters.HighContrast)
                ArcLog.GetLogger<WallpaperControl>().Warn("Highcontrast mode detected, some functionalities may not work properly.");

            this._monitorManager.MonitorUpdated += MonitorSettingsChanged_Hwnd;
            WallpaperChanged += SetupDesktop_WallpaperChanged;

            SystemEvents.SessionSwitch += (s, e) => {
                if (e.Reason == SessionSwitchReason.SessionUnlock) {
                    if (!(DesktopWorkerW == IntPtr.Zero || Native.IsWindow(DesktopWorkerW))) {
                        ArcLog.GetLogger<WallpaperControl>().Info("WorkerW invalid after unlock, resetting..");
                        ResetWallpaperAsync();
                    }
                    else {
                        if (Wallpapers.Any(x => x.IsExited)) {
                            ArcLog.GetLogger<WallpaperControl>().Info("Wallpaper crashed after unlock, resetting..");
                            ResetWallpaperAsync();
                        }
                    }
                }
            };

            // Initialize WorkerW
            UpdateWorkerW();

            try {
                if (_workerW != IntPtr.Zero) {
                    ArcLog.GetLogger<WallpaperControl>().Info("Hooking WorkerW events..");
                    var dwThreadId = Native.GetWindowThreadProcessId(_workerW, out int dwProcessId);
                    _workerWHook = new WindowEventHook(WindowEvent.EVENT_OBJECT_DESTROY);
                    _workerWHook.HookToThread(dwThreadId);
                    _workerWHook.EventReceived += WorkerWHook_EventReceived;
                }
                else {
                    ArcLog.GetLogger<WallpaperControl>().Error("Failed to initialize Core, WorkerW is NULL");
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WallpaperControl>().Error($"WorkerW hook failed: {ex.Message}");
            }
        }

        #region wallpaper actions
        public void CloseAllWallpapers() {
            if (_monitorManager.Monitors.Count > 0) {
                foreach (var item in _monitorManager.Monitors) {
                    item.ThumbnailPath = string.Empty;
                }
            }
            var tmp = _wallpapers.ToList();
            if (tmp.Count > 0) {
                tmp.ForEach(x => x.Close());
                tmp.Clear();
                ArcLog.GetLogger<WallpaperControl>().Info("Closed all wallpapers");
            }            
        }

        public void CloseWallpaper(IMonitor? monitor) {
            if (monitor == null) {
                ArcLog.GetLogger<WallpaperControl>().Warn("CloseWallpaper called with null monitor");
                return;
            }

            int idx = _monitorManager.Monitors.FindIndex(monitor);
            _monitorManager.Monitors[idx].ThumbnailPath = string.Empty;

            var tmp = _wallpapers.FindAll(x => x.Monitor.Equals(monitor));
            if (tmp.Count > 0) {
                tmp.ForEach(x => x.Close());
                _wallpapers.RemoveAll(tmp.Contains);

                ArcLog.GetLogger<WallpaperControl>().Info("Closed wallpaper at _monitor: " + monitor.DeviceId);
            }
        }

        public (string?, RuntimeType?) GetPrimaryWpFilePathRType() {
            var playingData = _wallpapers.FirstOrDefault(x => x.Monitor.IsPrimary);

            return (playingData?.Data.FilePath, playingData?.Data.RType);
        }

        public IWpMetadata GetWallpaperByFolderPath(string folderPath, string monitorContent, string rtype) {
            IWpMetadata data = WallpaperUtil.GetWallpaperByFolder(folderPath, monitorContent, rtype);

            return data;
        }

        public IWpBasicData GetWpBasicDataByForlderPath(string folderPath) {
            IWpBasicData data = WallpaperUtil.GetWpBasicDataByForlderPath(folderPath);

            return data;
        }

        public string? GetPlayerStartArgs(IWpPlayerData data, CancellationToken token = default) {
            var wpRuntimeData = CreateRuntimeData(data.FilePath, data.FolderPath, data.RType, true, _monitorManager.PrimaryMonitor.Content);
            DataAssist.FromRuntimeDataGetPlayerData(data, wpRuntimeData);

            var startArgs = _wallpaperFactory.CreatePlayerStartArgs(data, true);

            return startArgs;
        }

        public string GetPlayerStartArgsInRunning(string monitorId) {
            var instance = _wallpapers.FirstOrDefault(x => x.Monitor.DeviceId == monitorId);
            if (instance != null) {
                return instance.StartArgs;
            }
            return string.Empty;
        }

        public async Task ResetWallpaperAsync() {
            await _semaphoreSlimWallpaperLoadingLock.WaitAsync();

            try {
                ArcLog.GetLogger<WallpaperControl>().Info("Restarting wallpaper service..");
                // Copy existing wallpapers
                var originalWallpapers = Wallpapers.ToList();
                CloseAllWallpapers();
                // Restart _workerW
                UpdateWorkerW();
                if (_workerW == IntPtr.Zero) {
                    // Final attempt
                    ArcLog.GetLogger<WallpaperControl>().Info("Retry creating WorkerW after delay..");
                    await Task.Delay(500);
                    UpdateWorkerW();
                }
                foreach (var item in originalWallpapers) {
                    SetWallpaperAsync(item.Data, item.Monitor);
                    if (_userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Duplicate)
                        break;
                }
            }
            finally {
                _semaphoreSlimWallpaperLoadingLock.Release();
            }
        }

        public Grpc_RestartWallpaperResponse RestoreWallpaper() {
            Grpc_RestartWallpaperResponse response = new();

            try {
                ArcLog.GetLogger<WallpaperControl>().Info("Restore wallpapers...");
                var wallpaperLayouts = _userSettings.WallpaperLayouts.ToList().AsReadOnly();
                if (_userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Expand ||
                    _userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Duplicate) {
                    if (wallpaperLayouts.Count > 0) {
                        var layout = wallpaperLayouts.FirstOrDefault(x => x.MonitorDeviceId == _monitorManager.PrimaryMonitor.DeviceId);
                        var data = WallpaperUtil.GetWallpaperByFolder(layout.FolderPath, _monitorManager.PrimaryMonitor.Content, layout.RType);
                        SetWallpaperAsync(data.GetPlayerData(), _monitorManager.PrimaryMonitor);
                    }
                }
                else {
                    Restore(wallpaperLayouts);
                }

                response.IsFinished = true;
            }
            catch (Exception e) {
                _userSettings.WallpaperLayouts.Clear();
                _userSettings.Save<List<IWallpaperLayout>>();
                ArcLog.GetLogger<WallpaperControl>().Error($"Failed to restore wallpaper: {e}");
            }

            return response;
        }

        public async Task<Grpc_SetWallpaperResponse> SetWallpaperAsync(
            IWpPlayerData data,
            IMonitor? monitor,
            bool fromPreview = false,
            CancellationToken token = default) {
            await _semaphoreSlimWallpaperLoadingLock.WaitAsync(token);
            Grpc_SetWallpaperResponse response = new() {
                IsFinished = true
            };

            if (monitor == null) {
                response.IsFinished = false;
                return response;
            }

            try {
                ArcLog.GetLogger<WallpaperControl>().Info($"Setting wallpaper: {data.FilePath}");

                if (data.RType == RuntimeType.RUnknown) {
                    throw new Exception("rtype error");
                }

                #region pre-check
                if (_workerW == nint.Zero) {
                    ArcLog.GetLogger<WallpaperControl>().Error("WorkerW is not found");
                    response.IsFinished = false;

                    return response;
                }

                if (!_monitorManager.MonitorExists(monitor)) {
                    ArcLog.GetLogger<WallpaperControl>().Info($"Skipping wallpaper, _monitor {monitor.DeviceId} not found.");
                    WallpaperError?.Invoke(this, new ScreenNotFoundException($"Mnotir {monitor.DeviceId} not found."));

                    response.IsFinished = false;

                    return response;
                }
                else if (!File.Exists(data.FilePath)) {
                    //Only checking for wallpapers outside folder.
                    //This was before core separation, now the check can be simplified with just FolderPath != null.
                    ArcLog.GetLogger<WallpaperControl>().Info($"Skipping wallpaper, file {data.FilePath} not found.");
                    WallpaperError?.Invoke(this, new WallpaperNotFoundException($"{LanguageManager.Instance
                            ["WpControl_TextFileNotFound"]}\n{data.FilePath}"));
                    WallpaperChanged?.Invoke(this, EventArgs.Empty);

                    response.IsFinished = false;

                    return response;
                }
                #endregion

                bool isStarted = false;
                IWpRuntimeData? wpRuntimeData;
                // restore 时避免覆盖已有的自定义配置
                if (data.WpEffectFilePathUsing == string.Empty) {
                    if (fromPreview) {
                        wpRuntimeData = GetTempRuntimeData(data, monitor.Content);
                    }
                    else {
                        wpRuntimeData = CreateRuntimeData(data.FilePath, data.FolderPath, data.RType, false, monitor.Content);
                    }
                    DataAssist.FromRuntimeDataGetPlayerData(data, wpRuntimeData);
                }
                int monitorIdx = _monitorManager.Monitors.FindIndex(x => x.DeviceId == monitor.DeviceId);

                switch (_userSettings.Settings.WallpaperArrangement) {
                    case WallpaperArrangement.Per: {
                            var runningInstance = _wallpapers.Find(x => x.Monitor.DeviceId == monitor.DeviceId && x.Data.Arrangement == WallpaperArrangement.Per);
                            if (runningInstance != null) {
                                runningInstance.Update(data);
                                _monitorManager.UpdateTargetMonitorThu(monitorIdx, data.ThumbnailPath);
                                break;
                            }
                            runningInstance?.Close();

                            data.Arrangement = WallpaperArrangement.Per;
                            IWpPlayer instance = _wallpaperFactory.CreatePlayer(data, monitor);
                            CloseWallpaper(instance.Monitor);
                            isStarted = await instance.ShowAsync(token);

                            if (isStarted && !TrySetWallpaperPerMonitor(instance.ProcWindowHandle, instance.Monitor)) {
                                isStarted = false;
                                instance.Close();
                                _monitorManager.UpdateTargetMonitorThu(monitorIdx, string.Empty);
                                ArcLog.GetLogger<WallpaperControl>().Error("Failed to set wallpaper as child of WorkerW");

                                response.IsFinished = false;
                            }
                            else {
                                instance.Closing += ClosingEvent;
                                App.Jobs.AddProcess(instance.Proc.Id);
                                _monitorManager.UpdateTargetMonitorThu(monitorIdx, data.ThumbnailPath);
                                _wallpapers.Add(instance);
                            }
                        }
                        break;
                    case WallpaperArrangement.Expand: {
                            var runningInstance = _wallpapers.Find(x => x.Monitor.DeviceId == monitor.DeviceId && x.Data.Arrangement == WallpaperArrangement.Expand);
                            if (runningInstance != null) {
                                runningInstance.Update(data);
                                _monitorManager.UpdateTargetMonitorThu(monitorIdx, data.ThumbnailPath);
                                break;
                            }
                            runningInstance?.Close();

                            data.Arrangement = WallpaperArrangement.Expand;
                            IWpPlayer instance = _wallpaperFactory.CreatePlayer(data, monitor);
                            CloseAllWallpapers();
                            isStarted = await instance.ShowAsync(token);

                            if (isStarted && !TrySetWallpaperSpanMonitor(instance.ProcWindowHandle)) {
                                isStarted = false;
                                _monitorManager.UpdateTargetMonitorThu(monitorIdx, string.Empty);
                                ArcLog.GetLogger<WallpaperControl>().Error("Failed to set wallpaper as child of WorkerW");

                                response.IsFinished = false;
                            }
                            else {
                                instance.Closing += ClosingEvent;
                                App.Jobs.AddProcess(instance.Proc.Id);
                                _monitorManager.UpdateTargetMonitorThu(monitorIdx, data.ThumbnailPath);
                                _wallpapers.Add(instance);
                            }
                        }
                        break;
                    case WallpaperArrangement.Duplicate: {
                            CloseAllWallpapers();
                            foreach (var item in _monitorManager.Monitors) {
                                var runningInstance = _wallpapers.Find(x => x.Monitor.DeviceId == monitor.DeviceId && x.Data.Arrangement == WallpaperArrangement.Duplicate);
                                if (runningInstance != null) {
                                    runningInstance.Update(data);
                                    _monitorManager.UpdateTargetMonitorThu(monitorIdx, data.ThumbnailPath);
                                    break;
                                }
                                runningInstance?.Close();

                                data.Arrangement = WallpaperArrangement.Duplicate;
                                IWpPlayer instance = _wallpaperFactory.CreatePlayer(data, item);
                                isStarted = await instance.ShowAsync(token);

                                if (isStarted && !TrySetWallpaperPerMonitor(instance.ProcWindowHandle, instance.Monitor)) {
                                    isStarted = false;
                                    _monitorManager.UpdateTargetMonitorThu(monitorIdx, string.Empty);
                                    ArcLog.GetLogger<WallpaperControl>().Error("Failed to set wallpaper as child of WorkerW");

                                    response.IsFinished = false;
                                }
                                else {
                                    instance.Closing += ClosingEvent;
                                    App.Jobs.AddProcess(instance.Proc.Id);
                                    _monitorManager.UpdateTargetMonitorThu(monitorIdx, data.ThumbnailPath);
                                    _wallpapers.Add(instance);
                                }
                            }
                        }
                        break;
                }
                if (response.IsFinished) {
                    WallpaperChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Win32Exception ex) {
                ArcLog.GetLogger<WallpaperControl>().Error(ex);
                if (ex.NativeErrorCode == 2) //ERROR_FILE_NOT_FOUND
                    WallpaperError?.Invoke(this, new WallpaperPluginNotFoundException(ex.Message));
                else
                    WallpaperError?.Invoke(this, ex);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WallpaperControl>().Error(ex);
                WallpaperError?.Invoke(this, ex);
                WallpaperChanged?.Invoke(this, EventArgs.Empty);
            }
            finally {
                _semaphoreSlimWallpaperLoadingLock.Release();
            }

            return response;
        }

        private void UpdateWallpaper(IWpPlayer instance) {
            var monitorDeviceId = instance.Monitor.DeviceId;
            foreach (var item in _monitorManager.Monitors) {
                if (item.DeviceId == monitorDeviceId) {
                    continue;
                }

                var targetInstance = _wallpapers.FirstOrDefault(x => x.Monitor.DeviceId == item.DeviceId);
                if (targetInstance == null) {
                    SetWallpaperAsync(instance.Data, _monitorManager.PrimaryMonitor);
                }
                else {
                    targetInstance?.SendMessage(new VirtualPaperUpdateCmd() {
                        FilePath = instance.Data.FilePath,
                        RType = instance.Data.RType.ToString(),
                        WpEffectFilePathTemplate = instance.Data.WpEffectFilePathTemplate,
                        WpEffectFilePathTemporary = instance.Data.WpEffectFilePathTemporary,
                        WpEffectFilePathUsing = instance.Data.WpEffectFilePathUsing,
                    });
                }
            }
        }

        public void SeekWallpaper(IWpPlayerData playerData, float seek, PlaybackPosType type) {
            _wallpapers.ForEach(x => {
                if (x.Data == playerData) {
                    x.SetPlaybackPos(seek, type);
                }
            });
        }

        public void SeekWallpaper(IMonitor monitor, float seek, PlaybackPosType type) {
            _wallpapers.ForEach(x => {
                if (x.Monitor == monitor) {
                    x.SetPlaybackPos(seek, type);
                }
            });
        }

        public void SendMessageWallpaper(string deviceId, string ipcMsg) {
            IpcMessage msg = JsonSerializer.Deserialize(ipcMsg, IpcMessageContext.Default.IpcMessage)!;
            var arrangement = _userSettings.Settings.WallpaperArrangement;
            _wallpapers.ForEach(x => {
                if (arrangement == WallpaperArrangement.Duplicate || x.Monitor?.DeviceId == deviceId) {
                    x.SendMessage(msg);
                }
            });
        }
        #endregion

        #region data
        public IWpBasicData CreateBasicData(
            string filePath,
            FileType ftype,
            string? folderName = null,
            bool isAutoSave = true,
            CancellationToken token = default) {
            WpBasicData data = new();
            string folderPath = string.Empty;

            try {
                data.FType = ftype;
                data.AppInfo = new() {
                    AppName = _userSettings.Settings.AppName,
                    AppVersion = _userSettings.Settings.AppVersion,
                    FileVersion = Constants.CoreField.FileVersion,
                };
                data.IsSubscribed = true;

                // 创建随机不重复文件夹，并更新 wallpaperUid
                folderName ??= Path.GetRandomFileName();
                data.FolderName = folderName;
                data.WallpaperUid = "LCL" + folderName;
                folderPath = Path.Combine(_userSettings.Settings.WallpaperDir, folderName);
                data.FolderPath = folderPath;

                // 创建壁纸存储路径与自定义配置文件路径,将原壁纸复制到 folder 下                
                Directory.CreateDirectory(folderPath);
                string destFilePath = Path.Combine(folderPath, folderName + Path.GetExtension(filePath));
                if (filePath != destFilePath) {
                    File.Copy(filePath, destFilePath, true);
                }
                data.FilePath = destFilePath;

                #region 创建展示缩略图
                string thumbnailPath = Path.Combine(folderPath, folderName + Constants.Field.ThumGifSuff);
                WallpaperUtil.CreateGif(filePath, thumbnailPath, ftype, token);
                data.ThumbnailPath = thumbnailPath;
                #endregion

                #region 文件元数据
                var fileProperty = WallpaperUtil.GetWpProperty(filePath, ftype);
                data.Resolution = fileProperty.Resolution;
                data.AspectRatio = fileProperty.AspectRatio;
                data.FileSize = fileProperty.FileSize;
                data.FileExtension = fileProperty.FileExtension;
                data.CreatedTime = DateTime.UtcNow;

                string basicDatafilePath = Path.Combine(folderPath, Constants.Field.WpBasicDataFileName);
                if (isAutoSave) {
                    data.Save();
                }
                #endregion
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                if (Directory.Exists(folderPath)) {
                    Directory.Delete(folderPath, true);
                }

                throw;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WallpaperControl>().Error(ex);

                if (Directory.Exists(folderPath)) {
                    Directory.Delete(folderPath, true);
                }

                throw;
            }

            return data;
        }

        public IWpBasicData CreateBasicDataInMem(
            string filePath,
            FileType ftype,
            string? folderName = null,
            bool isAutoSave = true,
            CancellationToken token = default) {
            WpBasicData data = new();
            string folderPath = string.Empty;

            try {
                data.FType = ftype;
                data.AppInfo = new() {
                    AppName = _userSettings.Settings.AppName,
                    AppVersion = _userSettings.Settings.AppVersion,
                    FileVersion = _userSettings.Settings.FileVersion,
                };
                data.IsSubscribed = true;

                // 创建随机不重复文件夹，并更新 wallpaperUid
                folderName ??= Path.GetRandomFileName();
                data.FolderName = folderName;
                data.WallpaperUid = folderName;
                folderPath = Path.Combine(Constants.CommonPaths.TempDir, folderName);
                //_data.FolderPath = wallpaperUid;

                //// 创建壁纸存储路径与自定义配置文件路径,将原壁纸复制到 folder 下                
                Directory.CreateDirectory(folderPath);
                //string destFilePath = Path.Combine(wallpaperUid, folderName + Path.GetExtension(filePath));
                //if (filePath != destFilePath) {
                //    File.Copy(filePath, destFilePath, true);
                //}
                data.FilePath = filePath;

                #region 创建展示缩略图
                string thumbnailPath = Path.Combine(folderPath, folderName + Constants.Field.ThumGifSuff);
                WallpaperUtil.CreateGif(filePath, thumbnailPath, ftype, token);
                data.ThumbnailPath = thumbnailPath;
                #endregion

                #region 文件元数据
                var fileProperty = WallpaperUtil.GetWpProperty(filePath, ftype);
                data.Resolution = fileProperty.Resolution;
                data.AspectRatio = fileProperty.AspectRatio;
                data.FileSize = fileProperty.FileSize;
                data.FileExtension = fileProperty.FileExtension;

                //string basicDatafilePath = Path.Combine(wallpaperUid, Constants.Field.WpBasicDataFileName);
                //if (isAutoSave) {
                //    _data.Save();
                //}
                #endregion
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                if (Directory.Exists(folderPath)) {
                    Directory.Delete(folderPath, true);
                }

                throw;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WallpaperControl>().Error(ex);

                if (Directory.Exists(folderPath)) {
                    Directory.Delete(folderPath, true);
                }

                throw;
            }

            return data;
        }

        private IWpRuntimeData GetTempRuntimeData(IWpPlayerData playerData, string monitorContent) {
            WpRuntimeData data = new();

            try {
                data.AppInfo = new() {
                    AppName = _userSettings.Settings.AppName,
                    AppVersion = _userSettings.Settings.AppVersion,
                    FileVersion = _userSettings.Settings.FileVersion,
                };
                data.MonitorContent =
                    _userSettings.Settings.WallpaperArrangement switch {
                        WallpaperArrangement.Per => monitorContent,
                        WallpaperArrangement.Duplicate => "Duplicate",
                        WallpaperArrangement.Expand => "Expand",
                        _ => monitorContent
                    };
                data.FolderPath = playerData.FolderPath;
                data.RType = playerData.RType;

                data.WpEffectFilePathTemplate = playerData.WpEffectFilePathTemplate;
                data.WpEffectFilePathUsing = playerData.WpEffectFilePathUsing;
                data.WpEffectFilePathTemporary = playerData.WpEffectFilePathTemporary;
                File.Copy(data.WpEffectFilePathTemporary, data.WpEffectFilePathUsing, true);
                data.DepthFilePath = playerData.DepthFilePath;

                data.FromTempMoveToInstallPath(_userSettings.Settings.WallpaperDir);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WallpaperControl>().Error(ex);
                throw;
            }

            return data;
        }

        public IWpRuntimeData CreateRuntimeData(
            string filePath,
            string folderPath,
            RuntimeType rtype,
            bool isPreview,
            string monitorContent) {
            WpRuntimeData data = new();
            string wpEffectFilePathTemplate = string.Empty;
            string wpEffectFilePathTemporary = string.Empty;
            string wpEffectFilePathUsing = string.Empty;
            string storageFilePath = isPreview ? Path.Combine(Constants.CommonPaths.TempDir, Path.GetFileName(folderPath)) : folderPath;

            try {
                if (!Directory.Exists(storageFilePath)) {
                    Directory.CreateDirectory(storageFilePath);
                }

                data.AppInfo = new() {
                    AppName = _userSettings.Settings.AppName,
                    AppVersion = _userSettings.Settings.AppVersion,
                    FileVersion = _userSettings.Settings.FileVersion,
                };
                data.MonitorContent = monitorContent.ToString();
                data.FolderPath = storageFilePath;
                data.RType = rtype;

                wpEffectFilePathTemplate =
                    WallpaperUtil.CreateWpEffectFileTemplate(
                        storageFilePath,
                        rtype);
                data.WpEffectFilePathTemplate = wpEffectFilePathTemplate;

                wpEffectFilePathTemporary =
                    WallpaperUtil.CreateWpEffectFileUsingOrTemporary(
                       1,
                       storageFilePath,
                       wpEffectFilePathTemplate,
                       data.MonitorContent,
                       rtype);
                data.WpEffectFilePathTemporary = wpEffectFilePathTemporary;

                wpEffectFilePathUsing =
                   WallpaperUtil.CreateWpEffectFileUsingOrTemporary(
                       0,
                       storageFilePath,
                       wpEffectFilePathTemplate,
                       data.MonitorContent,
                       rtype);
                data.WpEffectFilePathUsing = wpEffectFilePathUsing;

                if (rtype == RuntimeType.RImage3D) {
                    var output = MiDaS.Run(filePath);
                    string depthFilePath = MiDaS.SaveDepthMap(output.Depth, output.Width, output.Height, output.OriginalWidth, output.OriginalHeight, folderPath);
                    data.DepthFilePath = depthFilePath;
                }

                data.Save();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WallpaperControl>().Error(ex);

                File.Delete(wpEffectFilePathTemplate);
                File.Delete(wpEffectFilePathTemporary);
                File.Delete(wpEffectFilePathUsing);
                Directory.Delete(storageFilePath, true);
            }

            return data;
        }

        public async Task<IWpBasicData> UpdateBasicDataAsync(
            string folderPath,
            string folderName,
            string filePath,
            FileType ftype,
            CancellationToken token = default) {
            IWpBasicData newData = CreateBasicData(filePath, ftype, token: token, folderName: folderName, isAutoSave: false)
                ?? throw new Exception("Create basic-data error");

            try {
                IWpBasicData oldData = await JsonSaver.LoadAsync<WpBasicData>(Path.Combine(folderPath, Constants.Field.WpBasicDataFileName), WpBasicDataContext.Default);
                newData.Merge(oldData);
                newData.Save();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WallpaperControl>().Error(ex);
            }

            return newData;
        }
        #endregion

        #region utils
        private readonly object _layoutChangeLock = new();
        public void ChangeWallpaperLayoutFolrderPath(string previousDir, string newDir) {
            lock (_layoutChangeLock) {
                var wpLayouts = _userSettings.WallpaperLayouts;
                for (int i = 0; i < wpLayouts.Count; ++i) {
                    var wpLayout = wpLayouts[i];
                    wpLayout.FolderPath = wpLayout.FolderPath.Replace(previousDir, newDir);
                    _userSettings.WallpaperLayouts[i] = wpLayout;
                }

                _userSettings.Save<List<IWallpaperLayout>>();
            }
        }

        public FileProperty GetWpProperty(string filePath, FileType ftype) {
            return WallpaperUtil.GetWpProperty(filePath, ftype);
        }

        public Grpc_MonitorData GetRunMonitorByWallpaper(string wpUid) {
            IMonitor monitor = _wallpapers.Find(x => x.Data.WallpaperUid == wpUid)!.Monitor;
            return DataAssist.MonitorDataToGrpc(monitor);
        }
        #endregion

        #region private utils
        private readonly object _monitorSettingsChangedLock = new();
        private void MonitorSettingsChanged_Hwnd(object? sender, EventArgs e) {
            lock (_monitorSettingsChangedLock) {
                ArcLog.GetLogger<WallpaperControl>().Info("Monitor settings changed, _monitor(s):");
                _monitorManager.Monitors.ToList().ForEach(x => ArcLog.GetLogger<WallpaperControl>().Info(x.DeviceId + " " + x.Bounds));

                RefreshWallpaper();
            }
        }

        private void RefreshWallpaper() {
            try {
                //Wallpapers still running on disconnected screens.
                var allScreens = _monitorManager.Monitors.ToList();//ScreenHelper.GetScreen();
                var orphanWallpapers = _wallpapers.FindAll(
                    wallpaper => allScreens.Find(
                        monitor => wallpaper.Monitor.Equals(monitor)) == null);

                //Updating user selected _monitor to primary if disconnected.
                _userSettings.Settings.SelectedMonitor =
                    allScreens.Find(x => _userSettings.Settings.SelectedMonitor.Equals(x)) ??
                    _monitorManager.PrimaryMonitor;
                _userSettings.Save<ISettings>();

                switch (_userSettings.Settings.WallpaperArrangement) {
                    case WallpaperArrangement.Per:
                        //No screens running _data needs to be removed.
                        if (orphanWallpapers.Count != 0) {
                            orphanWallpapers.ForEach(x => {
                                ArcLog.GetLogger<WallpaperControl>().Info($"Disconnected Screen: {x.Monitor.DeviceId} {x.Monitor.Bounds}");
                                x.Close();
                            });

                            _wallpapers.RemoveAll(orphanWallpapers.Contains);
                        }
                        break;
                    case WallpaperArrangement.Duplicate:
                        if (orphanWallpapers.Count != 0) {
                            orphanWallpapers.ForEach(x => {
                                ArcLog.GetLogger<WallpaperControl>().Info($"Disconnected Screen: {x.Monitor.DeviceId} {x.Monitor.Bounds}");
                                x.Close();
                            });
                            _wallpapers.RemoveAll(orphanWallpapers.Contains);
                        }
                        break;
                    case WallpaperArrangement.Expand:
                        //Only update _data rect.
                        break;
                }
                //Desktop size change when _monitor is added/removed/fileProperty changed.
                UpdateWallpaperRect();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WallpaperControl>().Error(ex.ToString());
            }
            finally {
                //Notifying display/_data change.
                WallpaperChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateWallpaperRect() {
            if (_monitorManager.IsMultiScreen() && _userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Expand) {
                if (_wallpapers.Count != 0) {
                    //Wallpapers[0].Play();
                    var screenArea = _monitorManager.VirtualScreenBounds;
                    ArcLog.GetLogger<WallpaperControl>().Info($"Updating _data rect(Expand): ({screenArea.Width}, {screenArea.Height}).");
                    //For Play/Pause, setting the new metadata.
                    Wallpapers[0].Monitor = _monitorManager.PrimaryMonitor;
                    Native.SetWindowPos(Wallpapers[0].RealPlayerWindowHandle, 1, 0, 0, screenArea.Width, screenArea.Height, (int)Native.SWP_NOACTIVATE);
                }
            }
            else {
                int i;
                foreach (var screen in _monitorManager.Monitors.ToList()) {
                    if ((i = _wallpapers.FindIndex(x => x.Monitor.Equals(screen))) != -1) {
                        //Wallpapers[i].Play();
                        ArcLog.GetLogger<WallpaperControl>().Info($"Updating _data rect(Screen): {Wallpapers[i].Monitor.Bounds} -> {screen.Bounds}.");
                        //For Play/Pause, setting the new metadata.
                        Wallpapers[i].Monitor = screen;

                        var screenArea = _monitorManager.VirtualScreenBounds;
                        if (!Native.SetWindowPos(Wallpapers[i].RealPlayerWindowHandle,
                            1,
                            screen.Bounds.X - screenArea.Location.X,
                            screen.Bounds.Y - screenArea.Location.Y,
                            screen.Bounds.Width,
                            screen.Bounds.Height,
                            (int)Native.SWP_NOACTIVATE)) {
                            //LogUtil.LogWin32Error("Failed to update _data rect");
                        }
                    }
                }
            }
            DesktopUtil.RefreshDesktop();
        }

        private void Restore(ReadOnlyCollection<IWallpaperLayout> wallpaperLayouts) {
            //CloseAllWallpapers();
            for (int i = 0; i < wallpaperLayouts.Count; i++) {
                var layout = wallpaperLayouts[i];
                try {
                    var monitor = _monitorManager.Monitors.FirstOrDefault(x => x.DeviceId == layout.MonitorDeviceId);
                    if (monitor == null) {
                        ArcLog.GetLogger<WallpaperControl>().Info($"Screen missing, skipping restoration of {layout.FolderPath} | {layout.MonitorDeviceId}");
                    }
                    else {
                        IWpMetadata data = WallpaperUtil.GetWallpaperByFolder(layout.FolderPath, layout.MonitorContent, layout.RType);
                        if (data == null || !data.IsAvailable()) {
                            ArcLog.GetLogger<WallpaperControl>().Error($"Skipping restoration of {layout.FolderPath}");
                            CloseWallpaper(monitor);
                            continue;
                        }
                        ArcLog.GetLogger<WallpaperControl>().Info($"Restoring data: {data.BasicData.FolderPath}");
                        SetWallpaperAsync(data.GetPlayerData(), monitor);
                    }
                }
                catch (Exception e) {
                    ArcLog.GetLogger<WallpaperControl>().Error($"An error occurred on restoration of {layout.FolderPath} | {e.Message}");
                }
            }
        }

        private void ClosingEvent(object? s, EventArgs e) {
            if (s is not IWpPlayer instance) return;

            instance.Closing -= ClosingEvent;
            instance.Closing = null;
            _wallpapers.Remove(instance);
            WallpaperChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SetupDesktop_WallpaperChanged(object? sender, EventArgs e) {
            SaveWallpaperLayout();
        }

        private readonly object _layoutWriteLock = new();
        private void SaveWallpaperLayout() {
            lock (_layoutWriteLock) {
                _userSettings.WallpaperLayouts.Clear();
                foreach (var wallpaper in _wallpapers) {
                    if (wallpaper.Monitor == null) continue;

                    string monitorContent = _userSettings.Settings.WallpaperArrangement switch {
                        WallpaperArrangement.Per => wallpaper.Monitor.Content,
                        WallpaperArrangement.Duplicate => "Duplicate",
                        WallpaperArrangement.Expand => "Expand",
                        _ => wallpaper.Monitor.Content
                    };

                    _userSettings.WallpaperLayouts.Add(new WallpaperLayout(
                        wallpaper.Data.FolderPath,
                        wallpaper.Monitor.DeviceId,
                        monitorContent,
                        wallpaper.Data.RType.ToString()
                    ));
                }

                try {
                    _userSettings.Save<List<IWallpaperLayout>>();
                }
                catch (Exception e) {
                    ArcLog.GetLogger<WallpaperControl>().Error(e.ToString());
                }
            }
        }

        private void UpdateWorkerW() {
            ArcLog.GetLogger<WallpaperControl>().Info("WorkerW initializing..");
            var retries = 5;
            while (true) {
                _workerW = CreateWorkerW();
                if (_workerW != IntPtr.Zero) {
                    break;
                }
                else {
                    retries--;
                    if (retries == 0)
                        break;

                    ArcLog.GetLogger<WallpaperControl>().Error($"Failed to create WorkerW, retrying ({retries})..");
                }
            }
            ArcLog.GetLogger<WallpaperControl>().Info($"WorkerW initialized {_workerW}");
            WallpaperReset?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Calculates the position of window w.r.T parent _workerW _handle & sets it as child window to it.
        /// </summary>
        /// <param name="handle">window _handle of process to add as wallpaper</param>
        /// <param name="targetMonitor">monitorstring of _monitor to sent wp to.</param>
        private static bool TrySetWallpaperPerMonitor(nint handle, IMonitor targetMonitor) {
            var success = TrySetParentWorkerW(handle);
            // Position the wp fullscreen to corresponding display.
            if (!Native.SetWindowPos(handle, 1, targetMonitor.Bounds.X, targetMonitor.Bounds.Y, targetMonitor.Bounds.Width, targetMonitor.Bounds.Height, (int)Native.SWP_NOACTIVATE)) {
                ArcLog.GetLogger<WallpaperControl>().Error("Failed to set perscreen wallpaper(1)}");
            }

            var prct = new Native.RECT();
            _ = Native.MapWindowPoints(handle, _workerW, ref prct, 2);
            if (!Native.SetWindowPos(handle, 1, prct.Left, prct.Top, targetMonitor.Bounds.Width, targetMonitor.Bounds.Height, (int)Native.SWP_NOACTIVATE)) {
                ArcLog.GetLogger<WallpaperControl>().Error("Failed to set perscreen wallpaper(2)");
            }
            DesktopUtil.RefreshDesktop();

            return success;
        }

        /// <summary>
        /// Spans wp across All screens.
        /// </summary>
        private static bool TrySetWallpaperSpanMonitor(nint handle) {
            //get spawned workerw rectangle data.
            _ = Native.GetWindowRect(_workerW, out Native.RECT prct);
            var success = TrySetParentWorkerW(handle);

            //fill wp into the whole workerw area.
            ArcLog.GetLogger<WallpaperControl>().Info($"Wallpaper(Span): ({prct.Left}, {prct.Top}, {prct.Right - prct.Left}, {prct.Bottom - prct.Top}).");
            if (!Native.SetWindowPos(handle, 1, 0, 0, prct.Right - prct.Left, prct.Bottom - prct.Top, (int)Native.SWP_NOACTIVATE)) {
                ArcLog.GetLogger<WallpaperControl>().Error("Failed to set span wallpaper");
            }
            DesktopUtil.RefreshDesktop();

            return success;
        }

        public static void ConvertPopupToChildWindow(IntPtr hwnd) {
            // Get the current window style
            long style = Native.GetWindowLong(hwnd, Native.GWL_STYLE);

            // Remove WS_POPUP and add WS_CHILD style
            style &= ~Native.WS_POPUP;
            style |= Native.WS_CHILD;

            // Apply the new window style
            Native.SetWindowLong(hwnd, Native.GWL_STYLE, style);

            // Set the new parent window
            // Native.SetParent(hwnd, newParentHwnd);
        }

        private static nint CreateWorkerW() {
            // Fetch the Progman window
            var progman = Native.FindWindow("Progman", null);

            nint result = nint.Zero;

            // Send 0x052C to Progman. This message directs Progman to spawn a 
            // WorkerW behind the desktop icons. If it is already there, nothing 
            // happens.
            Native.SendMessageTimeout(progman,
                                   0x052C,
                                   new IntPtr(0xD),
                                   new IntPtr(0x1),
                                   Native.SendMessageTimeoutFlags.SMTO_NORMAL,
                                   1000,
                                   out result);
            // Spy++ output
            // .....
            // 0x00010190 "" WorkerW
            //   ...
            //   0x000100EE "" SHELLDLL_DefView
            //     0x000100F0 "FolderView" SysListView32
            // 0x00100B8A "" WorkerW       <-- This is the WorkerW curInstance we are after!
            // 0x000100EC "Program Manager" Progman
            var _workerW = IntPtr.Zero;

            // We enumerate All Windows, until we find one, that has the SHELLDLL_DefView 
            // as a child. 
            // If we found that window, we take its next sibling and assign it to _workerW.
            Native.EnumWindows(new Native.EnumWindowsProc((tophandle, topparamhandle) => {
                IntPtr p = Native.FindWindowEx(tophandle,
                                            IntPtr.Zero,
                                            "SHELLDLL_DefView",
                                            IntPtr.Zero);

                if (p != IntPtr.Zero) {
                    // Gets the WorkerW Window after the current one.
                    _workerW = Native.FindWindowEx(IntPtr.Zero,
                                                    tophandle,
                                                    "WorkerW",
                                                    IntPtr.Zero);
                }

                return true;
            }), IntPtr.Zero);

            // Some Windows 11 builds have a different Progman window layout.
            // If the above code failed to find WorkerW, we should try this.
            // Spy++ output
            // 0x000100EC "Program Manager" Progman
            //   0x000100EE "" SHELLDLL_DefView
            //     0x000100F0 "FolderView" SysListView32
            //   0x00100B8A "" WorkerW       <-- This is the WorkerW curInstance we are after!
            if (_workerW == IntPtr.Zero) {
                _workerW = Native.FindWindowEx(progman,
                                                IntPtr.Zero,
                                                "WorkerW",
                                                IntPtr.Zero);
            }

            return _workerW;
        }

        /// <summary>
        /// Adds the _data as child of spawned desktop-_workerW window.
        /// </summary>
        /// <param name="handle">_handle of window</param>
        private static bool TrySetParentWorkerW(IntPtr handle) {
            IntPtr ret = Native.SetParent(handle, _workerW);
            if (ret.Equals(IntPtr.Zero))
                return false;

            return true;
        }

        private static bool TrySetParentWorkerW(nint childHandle, nint parentHandle) {
            IntPtr ret = Native.SetParent(childHandle, parentHandle);
            if (ret.Equals(IntPtr.Zero))
                return false;

            return true;
        }

        private async void WorkerWHook_EventReceived(object? sender, WinEventHookEventArgs e) {
            if (e.WindowHandle == _workerW && e.EventType == WindowEvent.EVENT_OBJECT_DESTROY) {
                ArcLog.GetLogger<WallpaperControl>().Error("WorkerW destroyed.");
                await ResetWallpaperAsync();
            }
        }
        #endregion

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    WallpaperChanged -= SetupDesktop_WallpaperChanged;
                    try {
                        CloseAllWallpapers();
                        DesktopUtil.RefreshDesktop();
                    }
                    catch (Exception e) {
                        ArcLog.GetLogger<WallpaperControl>().Error("Failed to shutdown core: " + e.ToString());
                    }
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly WindowEventHook? _workerWHook;
        private static readonly List<IWpPlayer> _wallpapers = [];
        private static nint _workerW;
        private static readonly SemaphoreSlim _semaphoreSlimWallpaperLoadingLock = new(1, 1);
        private readonly IUserSettingsService _userSettings;
        private readonly IWallpaperFactory _wallpaperFactory;
        private readonly IMonitorManager _monitorManager;
    }
}
