using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using OpenCvSharp.Internal;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files.Models;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.Shell;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.DataAssistor;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.lang;
using VirtualPaper.ML.DepthEstimate;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils;
using WinEventHook;
using static VirtualPaper.Common.Errors;
// todo: webview dpi 感知；thu 大小;

namespace VirtualPaper.Cores.WpControl {
    public partial class WallpaperControl : IWallpaperControl {
        public event EventHandler? WallpaperChanged;
        public event EventHandler<Exception>? WallpaperError;
        public event EventHandler? WallpaperReset;

        public nint DesktopWorkerW => _workerW;
        public ReadOnlyCollection<IWpPlayer> Wallpapers => _wallpapers.AsReadOnly();
        //public ReadOnlyCollection<IWpBasicData> LibraryWallpapers => _librarywallpapers.AsReadOnly();

        public WallpaperControl(
            IUserSettingsService userSettings,
            IMonitorManager monitorManager,
            IWallpaperFactory wallpaperFactory) {
            this._userSettings = userSettings;
            this._monitorManager = monitorManager;
            this._wallpaperFactory = wallpaperFactory;

            if (SystemParameters.HighContrast)
                App.Log.Warn("Highcontrast mode detected, some functionalities may not work properly.");

            this._monitorManager.MonitorUpdated += MonitorSettingsChanged_Hwnd;
            WallpaperChanged += SetupDesktop_WallpaperChanged;

            SystemEvents.SessionSwitch += async (s, e) => {
                if (e.Reason == SessionSwitchReason.SessionUnlock) {

                    if (!(DesktopWorkerW == IntPtr.Zero || Native.IsWindow(DesktopWorkerW))) {
                        App.Log.Info("WorkerW invalid after unlock, resetting..");
                        await ResetWallpaperAsync();
                    }
                    else {
                        if (Wallpapers.Any(x => x.IsExited)) {
                            App.Log.Info("Wallpaper crashed after unlock, resetting..");
                            await ResetWallpaperAsync();
                        }
                    }
                }
            };

            // Initialize WorkerW
            UpdateWorkerW();

            try {
                if (_workerW != IntPtr.Zero) {
                    App.Log.Info("Hooking WorkerW events..");
                    var dwThreadId = Native.GetWindowThreadProcessId(_workerW, out int dwProcessId);
                    _workerWHook = new WindowEventHook(WindowEvent.EVENT_OBJECT_DESTROY);
                    _workerWHook.HookToThread(dwThreadId);
                    _workerWHook.EventReceived += WorkerWHook_EventReceived;
                }
                else {
                    App.Log.Error("Failed to initialize Core, WorkerW is NULL");
                }
            }
            catch (Exception ex) {
                App.Log.Error($"WorkerW hook failed: {ex.Message}");
            }
        }

        #region wallpaper actions
        public void CloseAllWallpapers() {
            if (_wallpapers.Count > 0) {
                _wallpapers.ForEach(x => x.Close());
                _wallpapers.Clear();
                WallpaperChanged?.Invoke(this, EventArgs.Empty);
            }
            App.Log.Info("Closed all wallpapers");
        }

        public void CloseWallpaper(IMonitor monitor) {
            int idx = _monitorManager.Monitors.FindIndex(monitor);
            _monitorManager.Monitors[idx].ThumbnailPath = string.Empty;

            var tmp = _wallpapers.FindAll(x => x.Monitor.Equals(monitor));
            if (tmp.Count > 0) {
                tmp.ForEach(x => {
                    x.Close();
                });

                _wallpapers.RemoveAll(tmp.Contains);
                WallpaperChanged?.Invoke(this, EventArgs.Empty);

                App.Log.Info("Closed wallpaper at monitor: " + monitor.DeviceId);
            }
        }

        public void CloseAllPreview() {
            foreach (var kvp in _previews) {
                kvp.Value.Close();
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

        public bool AdjustWallpaper(string monitorDeviceId, CancellationToken token) {
            var instance = _wallpapers.FirstOrDefault(x => x.Monitor.DeviceId == monitorDeviceId);
            if (instance != null) {
                instance.SendMessage(new VirtualPaperActiveCmd());
                return true;
            }

            return false;
        }

        public bool PreviewWallpaper(string monitorDeviceId, CancellationToken token) {
            var instance = _wallpapers.FirstOrDefault(x => x.Monitor.DeviceId == monitorDeviceId);
            if (instance == null) {
                return false;
            }
            instance.SendMessage(new VirtualPaperActiveCmd());

            return true;
        }

        public async Task<bool> PreviewWallpaperAsync(IWpPlayerData data, string monitorDeviceId, CancellationToken token) {
            _previews.TryGetValue((data.WallpaperUid, data.RType), out IWpPlayer? instance);
            if (instance != null) {
                instance.SendMessage(new VirtualPaperActiveCmd());
                return true;
            }

            try {
                var monitor = _monitorManager.Monitors.FirstOrDefault(x => x.DeviceId == monitorDeviceId) ?? _monitorManager.PrimaryMonitor;
                var wpRuntimeData = CreateRuntimeData(data.FilePath, data.FolderPath, data.RType, true, monitor.Content);
                DataAssist.FromRuntimeDataGetPlayerData(data, wpRuntimeData);

                instance = _wallpaperFactory.CreatePlayer(data, monitor, _userSettings, true);
                instance.Closing += IWallpaperPlayingClosingForPreiview;
                instance.ToBackground += FromPreviewToBackgound;

                _previews[(data.WallpaperUid, data.RType)] = instance;
                bool isStarted = await instance.ShowAsync(token);

                return isStarted;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                _previews.Remove((data.WallpaperUid, data.RType));
                instance?.Dispose();

                return false;
            }
            catch (Exception ex) {
                App.Log.Error($"An error occurred while preview wallpaper: {ex.Message}");
                _previews.Remove((data.WallpaperUid, data.RType));

                throw;
            }
        }

        private void FromPreviewToBackgound(object? s, EventArgs e) {
            if (s is not IWpPlayer instance) return;

            instance.Close();
            SetWallpaperAsync(instance.Data, instance.Monitor, fromPreview: true);
        }

        public async Task ResetWallpaperAsync() {
            await _semaphoreSlimWallpaperLoadingLock.WaitAsync();

            try {
                App.Log.Info("Restarting wallpaper service..");
                // Copy existing wallpapers
                var originalWallpapers = Wallpapers.ToList();
                CloseAllWallpapers();
                // Restart _workerW
                UpdateWorkerW();
                if (_workerW == IntPtr.Zero) {
                    // Final attempt
                    App.Log.Info("Retry creating WorkerW after delay..");
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
                App.Log.Info("Restore wallpapers...");
                var wallpaperLayouts = _userSettings.WallpaperLayouts.ToList();
                if (_userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Expand ||
                    _userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Duplicate) {
                    if (wallpaperLayouts.Count != 0) {
                        var layout = wallpaperLayouts[0];
                        var data = WallpaperUtil.GetWallpaperByFolder(
                            layout.FolderPath, layout.MonitorContent, layout.RType);
                        SetWallpaperAsync(data.GetPlayerData(), _monitorManager.PrimaryMonitor);
                    }
                }
                else if (_userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Per) {
                    RestoreWallpaper(wallpaperLayouts);
                }

                response.IsFinished = true;
            }
            catch (Exception e) {
                _userSettings.WallpaperLayouts.Clear();
                _userSettings.Save<List<IWallpaperLayout>>();
                App.Log.Error($"Failed to restore wallpaper: {e}");
            }

            return response;
        }

        public async Task<Grpc_SetWallpaperResponse> SetWallpaperAsync(
            IWpPlayerData data,
            IMonitor monitor,
            CancellationToken token = default,
            bool fromPreview = false) {
            await _semaphoreSlimWallpaperLoadingLock.WaitAsync(token);
            Grpc_SetWallpaperResponse response = new();

            try {
                App.Log.Info($"Setting wallpaper: {data.FilePath}");

                #region init
                if (!_isInitialized) {
                    if (SystemParameters.HighContrast) {
                        App.Log.Warn("Highcontrast mode detected, some functionalities may not work properly!");
                    }

                    // Fetch the Progman window
                    _progman = Native.FindWindow("Progman", null);

                    IntPtr result = IntPtr.Zero;

                    // Send 0x052C to Progman. This message directs Progman to spawn a 
                    // WorkerW behind the desktop icons. If it is already there, nothing 
                    // happens.
                    Native.SendMessageTimeout(_progman,
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
                    // 0x00100B8A "" WorkerW       <-- This is the WorkerW instance we are after!
                    // 0x000100EC "Program Manager" Progman
                    _workerW = IntPtr.Zero;

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

                    if (IntPtr.Equals(_workerW, IntPtr.Zero)) {
                        App.Log.Error("Failed to setup core, WorkerW handle not found..");
                        WallpaperError?.Invoke(this, new WorkerWException(LanguageManager.Instance
                            ["WpControl_VirtualPaperExceptionWorkerWSetupFail"]));
                        WallpaperChanged?.Invoke(this, EventArgs.Empty);

                        response.IsFinished = false;

                        return response;
                    }
                    else {
                        App.Log.Info("Core initialized..");
                        _isInitialized = true;
                        WallpaperReset?.Invoke(this, EventArgs.Empty);
                    }
                }

                if (!_monitorManager.MonitorExists(monitor)) {
                    App.Log.Info($"Skipping wallpaper, monitor {monitor.DeviceId} not found.");
                    WallpaperError?.Invoke(this, new ScreenNotFoundException($"Mnotir {monitor.DeviceId} not found."));

                    response.IsFinished = false;

                    return response;
                }
                else if (!File.Exists(data.FilePath)) {
                    //Only checking for wallpapers outside folder.
                    //This was before core separation, now the check can be simplified with just FolderPath != null.
                    App.Log.Info($"Skipping wallpaper, file {data.FilePath} not found.");
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
                            //bool isSetted = UpdateWallpaper(monitorIdx, monitor.DeviceId, data);
                            //if (isSetted) {
                            //    response.IsFinished = true;
                            //    return response;
                            //}

                            IWpPlayer instance = _wallpaperFactory.CreatePlayer(data, monitor, _userSettings);
                            instance.Closing += IWallpaperPlayingClosingForBG;
                            CloseWallpaper(instance.Monitor);
                            isStarted = await instance.ShowAsync(token);

                            if (isStarted && !TrySetWallpaperPerMonitor(instance, instance.Monitor)) {
                                isStarted = false;
                                App.Log.Error("Failed to set wallpaper as child of WorkerW");

                                response.IsFinished = false;
                            }
                            else {
                                App.Jobs.AddProcess(instance.Proc.Id);
                                _monitorManager.UpdateTargetMonitorThu(monitorIdx, data.ThumbnailPath);
                                _wallpapers.Add(instance);
                            }
                        }
                        break;
                    case WallpaperArrangement.Expand: {
                            IWpPlayer instance = _wallpaperFactory.CreatePlayer(data, monitor, _userSettings);
                            instance.Closing += IWallpaperPlayingClosingForBG;
                            CloseAllWallpapers();
                            isStarted = await instance.ShowAsync(token);

                            if (isStarted && !TrySetWallpaperSpanMonitor(instance)) {
                                isStarted = false;
                                App.Log.Error("Failed to set wallpaper as child of WorkerW");

                                response.IsFinished = false;
                            }
                            else {
                                App.Jobs.AddProcess(instance.Proc.Id);
                                _monitorManager.UpdateTargetMonitorThu(monitorIdx, data.ThumbnailPath);
                                _wallpapers.Add(instance);
                            }
                        }
                        break;
                    case WallpaperArrangement.Duplicate: {
                            CloseAllWallpapers();
                            foreach (var item in _monitorManager.Monitors) {
                                IWpPlayer instance = _wallpaperFactory.CreatePlayer(data, item, _userSettings);
                                instance.Closing += IWallpaperPlayingClosingForBG;
                                isStarted = await instance.ShowAsync(token);

                                if (isStarted && !TrySetWallpaperPerMonitor(instance, instance.Monitor)) {
                                    isStarted = false;
                                    App.Log.Error("Failed to set wallpaper as child of WorkerW");

                                    response.IsFinished = false;
                                }
                                else {
                                    App.Jobs.AddProcess(instance.Proc.Id);
                                    _monitorManager.UpdateTargetMonitorThu(monitorIdx, data.ThumbnailPath);
                                    _wallpapers.Add(instance);
                                }
                            }
                        }
                        break;
                }
                if (isStarted) {
                    response.IsFinished = true;
                    WallpaperChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Win32Exception ex) {
                App.Log.Error(ex);
                if (ex.NativeErrorCode == 2) //ERROR_FILE_NOT_FOUND
                    WallpaperError?.Invoke(this, new WallpaperPluginNotFoundException(ex.Message));
                else
                    WallpaperError?.Invoke(this, ex);
            }
            catch (Exception ex) {
                App.Log.Error(ex);
                WallpaperError?.Invoke(this, ex);
                WallpaperChanged?.Invoke(this, EventArgs.Empty);
            }
            finally {
                _semaphoreSlimWallpaperLoadingLock.Release();
            }

            return response;
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

        public void SendMessageWallpaper(IMonitor monitor, string folderPath, string ipcMsg) {
            IpcMessage msg = JsonSerializer.Deserialize<IpcMessage>(
                ipcMsg)!;

            _wallpapers.ForEach(x => {
                if (x.Data.FolderPath == folderPath && x.Monitor == monitor) {
                    x.SendMessage(msg);
                }
            });
        }

        //private bool UpdateWallpaper(int monitorIdx, string monitorDeviceId, IWpPlayerData data) {
        //    int idx = _wallpapers.FindIndex(x => x.Monitor.DeviceId == monitorDeviceId);
        //    if (idx == -1) return false;
        //    // RImage3D 暂不支持热替换
        //    if (data.RType == RuntimeType.RImage3D || _wallpapers[idx].Data.RType == RuntimeType.RImage3D) return false;

        //    _wallpapers[idx].Update(data);
        //    _monitorManager.UpdateTargetMonitorThu(monitorIdx, data.ThumbnailPath);
        //    WallpaperChanged?.Invoke(this, EventArgs.Empty);

        //    return true;
        //}
        #endregion

        #region data
        public IWpBasicData CreateBasicData(
            string filePath,
            FileType ftype,
            CancellationToken token) {
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

                // 创建随机不重复文件夹，并更新 folderPath
                string folderName = Path.GetRandomFileName();
                data.FolderName = folderName;
                data.WallpaperUid = "LCL" + folderName;
                folderPath = Path.Combine(_userSettings.Settings.WallpaperDir, folderName);
                data.FolderPath = folderPath;

                // 创建壁纸存储路径与自定义配置文件路径,将原壁纸复制到 folder 下
                Directory.CreateDirectory(folderPath);
                string destFilePath = Path.Combine(folderPath, folderName + Path.GetExtension(filePath));
                File.Copy(filePath, destFilePath, true);
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

                string basicDatafilePath = Path.Combine(folderPath, Constants.Field.WpBasicDataFileName);
                data.Save();
                #endregion
            }
            catch (OperationCanceledException) {
                if (Directory.Exists(folderPath)) {
                    Directory.Delete(folderPath, true);
                }
            }
            catch (Exception ex) {
                App.Log.Error(ex);

                if (Directory.Exists(folderPath)) {
                    Directory.Delete(folderPath, true);
                }
            }

            return data;
        }

        public IWpRuntimeData GetTempRuntimeData(IWpPlayerData playerData, string monitorContent) {
            WpRuntimeData data = new();

            try {
                data.AppInfo = new() {
                    AppName = _userSettings.Settings.AppName,
                    AppVersion = _userSettings.Settings.AppVersion,
                    FileVersion = _userSettings.Settings.FileVersion,
                };
                data.MonitorContent = monitorContent;
                data.FolderPath = playerData.FolderPath;
                data.RType = playerData.RType;

                data.WpEffectFilePathTemplate = playerData.WpEffectFilePathTemplate;
                data.WpEffectFilePathUsing = playerData.WpEffectFilePathUsing;
                data.WpEffectFilePathTemporary = playerData.WpEffectFilePathTemporary;
                File.Copy(data.WpEffectFilePathTemporary, data.WpEffectFilePathUsing, true);
                data.DepthFilePath = playerData.DepthFilePath;

                data.FromTempToInstallPath(_userSettings.Settings.WallpaperDir);
            }
            catch (Exception ex) {
                App.Log.Error(ex);
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
                data.MonitorContent = monitorContent;
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
                       monitorContent,
                       rtype,
                       _userSettings.Settings.WallpaperArrangement);
                data.WpEffectFilePathTemporary = wpEffectFilePathTemporary;

                wpEffectFilePathUsing =
                   WallpaperUtil.CreateWpEffectFileUsingOrTemporary(
                       0,
                       storageFilePath,
                       wpEffectFilePathTemplate,
                       monitorContent,
                       rtype,
                       _userSettings.Settings.WallpaperArrangement);
                data.WpEffectFilePathUsing = wpEffectFilePathUsing;

                if (rtype == RuntimeType.RImage3D) {
                    var output = MiDaS.Run(filePath);
                    string depthFilePath = MiDaS.SaveDepthMap(output.Depth, output.Width, output.Height, output.OriginalWidth, output.OriginalHeight, folderPath);
                    data.DepthFilePath = depthFilePath;
                }

                data.Save();
            }
            catch (Exception ex) {
                App.Log.Error(ex);

                File.Delete(wpEffectFilePathTemplate);
                File.Delete(wpEffectFilePathTemporary);
                File.Delete(wpEffectFilePathUsing);
                Directory.Delete(storageFilePath, true);
            }

            return data;
        }

        public IWpBasicData UpdateBasicData(
            string folderPath,
            string folderName,
            string filePath,
            FileType ftype,
            CancellationToken token) {
            WpBasicData data = new();

            try {
                data.FType = ftype;
                data.AppInfo = new() {
                    AppName = _userSettings.Settings.AppName,
                    AppVersion = _userSettings.Settings.AppVersion,
                    FileVersion = _userSettings.Settings.FileVersion,
                };
                data.IsSubscribed = true;
                data.FolderName = folderName;
                data.FolderPath = folderPath;
                data.FilePath = filePath;

                #region 创建展示缩略图
                string coverFilePath = Path.Combine(folderPath, folderName + Constants.Field.ThumGifSuff);
                WallpaperUtil.CreateGif(filePath, coverFilePath, ftype, token);
                data.ThumbnailPath = coverFilePath;
                #endregion

                #region 文件元数据
                var fileProperty = WallpaperUtil.GetWpProperty(filePath, ftype);
                data.Resolution = fileProperty.Resolution;
                data.AspectRatio = fileProperty.AspectRatio;
                data.FileSize = fileProperty.FileSize;
                data.FileExtension = fileProperty.FileExtension;

                data.Save();
                #endregion
            }
            catch (Exception ex) {
                App.Log.Error(ex);
            }

            return data;
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
                App.Log.Info("Monitor settings changed, monitor(s):");
                _monitorManager.Monitors.ToList().ForEach(x => App.Log.Info(x.DeviceId + " " + x.Bounds));

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

                //Updating user selected monitor to primary if disconnected.
                _userSettings.Settings.SelectedMonitor =
                    allScreens.Find(x => _userSettings.Settings.SelectedMonitor.Equals(x)) ??
                    _monitorManager.PrimaryMonitor;
                _userSettings.Save<ISettings>();

                switch (_userSettings.Settings.WallpaperArrangement) {
                    case WallpaperArrangement.Per:
                        //No screens running data needs to be removed.
                        if (orphanWallpapers.Count != 0) {
                            orphanWallpapers.ForEach(x => {
                                App.Log.Info($"Disconnected Screen: {x.Monitor.DeviceId} {x.Monitor.Bounds}");
                                x.Close();
                            });

                            _wallpapers.RemoveAll(x => orphanWallpapers.Contains(x));
                        }
                        break;
                    case WallpaperArrangement.Duplicate:
                        if (orphanWallpapers.Count != 0) {
                            orphanWallpapers.ForEach(x => {
                                App.Log.Info($"Disconnected Screen: {x.Monitor.DeviceId} {x.Monitor.Bounds}");
                                x.Close();
                            });
                            _wallpapers.RemoveAll(x => orphanWallpapers.Contains(x));
                        }
                        break;
                    case WallpaperArrangement.Expand:
                        //Only update data rect.
                        break;
                }
                //Desktop size change when monitor is added/removed/fileProperty changed.
                UpdateWallpaperRect();
            }
            catch (Exception ex) {
                App.Log.Error(ex.ToString());
            }
            finally {
                //Notifying display/data change.
                WallpaperChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateWallpaperRect() {
            if (_monitorManager.IsMultiScreen() && _userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Expand) {
                if (_wallpapers.Count != 0) {
                    //Wallpapers[0].Play();
                    var screenArea = _monitorManager.VirtualScreenBounds;
                    App.Log.Info($"Updating data rect(Expand): ({screenArea.Width}, {screenArea.Height}).");
                    //For Play/Pause, setting the new metadata.
                    Wallpapers[0].Monitor = _monitorManager.PrimaryMonitor;
                    Native.SetWindowPos(Wallpapers[0].Handle, 1, 0, 0, screenArea.Width, screenArea.Height, 0x0010);
                }
            }
            else {
                int i;
                foreach (var screen in _monitorManager.Monitors.ToList()) {
                    if ((i = _wallpapers.FindIndex(x => x.Monitor.Equals(screen))) != -1) {
                        //Wallpapers[i].Play();
                        App.Log.Info($"Updating data rect(Screen): {Wallpapers[i].Monitor.Bounds} -> {screen.Bounds}.");
                        //For Play/Pause, setting the new metadata.
                        Wallpapers[i].Monitor = screen;

                        var screenArea = _monitorManager.VirtualScreenBounds;
                        if (!Native.SetWindowPos(Wallpapers[i].Handle,
                            1,
                            (screen.Bounds.X - screenArea.Location.X),
                            (screen.Bounds.Y - screenArea.Location.Y),
                            (screen.Bounds.Width),
                            (screen.Bounds.Height),
                            0x0010)) {
                            //LogUtil.LogWin32Error("Failed to update data rect");
                        }
                    }
                }
            }
            DesktopUtil.RefreshDesktop();
        }

        private void RestoreWallpaper(List<IWallpaperLayout> wallpaperLayout) {
            for (int i = 0; i < wallpaperLayout.Count; i++) {
                var layout = wallpaperLayout[i];
                try {
                    IWpMetadata data = WallpaperUtil.GetWallpaperByFolder(
                        layout.FolderPath, layout.MonitorContent, layout.RType);
                    if (data == null || !data.IsAvailable()) {
                        App.Log.Error($"Skipping restoration of {layout.FolderPath}");
                        wallpaperLayout[i] = null;
                        continue;
                    }

                    var monitor = _monitorManager.Monitors.FirstOrDefault(x => x.DeviceId == layout.MonitorDeviceId);
                    if (monitor == null) {
                        App.Log.Info($"Screen missing, skipping restoration of {layout.FolderPath} | {layout.MonitorDeviceId}");
                    }
                    else {
                        App.Log.Info($"Restoring data: {data.BasicData.FolderPath}");
                        SetWallpaperAsync(data.GetPlayerData(), monitor);
                    }
                }
                catch (Exception e) {
                    App.Log.Error($"An error occurred on restoration of {layout.FolderPath} | {e.Message}");
                }
            }
        }

        private void IWallpaperPlayingClosingForBG(object? s, EventArgs e) {
            if (s is not IWpPlayer instance) return;

            instance.Closing -= IWallpaperPlayingClosingForBG;
            instance.Closing = null;
        }

        private void IWallpaperPlayingClosingForPreiview(object? s, EventArgs e) {
            if (s is not IWpPlayer instance) return;

            instance.Closing -= IWallpaperPlayingClosingForPreiview;
            instance.ToBackground -= FromPreviewToBackgound;
            instance.Closing = null;
            instance.ToBackground = null;
            _previews.Remove((instance.Data.WallpaperUid, instance.Data.RType));
        }

        private void SetupDesktop_WallpaperChanged(object? sender, EventArgs e) {
            SaveWallpaperLayout();
        }

        private readonly object _layoutWriteLock = new();
        private void SaveWallpaperLayout() {
            lock (_layoutWriteLock) {
                _userSettings.WallpaperLayouts.Clear();
                _wallpapers.ForEach(wallpaper => {
                    _userSettings.WallpaperLayouts.Add(new WallpaperLayout(
                        wallpaper.Data.FolderPath,
                        wallpaper.Monitor.DeviceId,
                        wallpaper.Monitor.Content,
                        wallpaper.Data.RType.ToString()));
                });

                try {
                    _userSettings.Save<List<IWallpaperLayout>>();
                }
                catch (Exception e) {
                    App.Log.Error(e.ToString());
                }
            }
        }

        private void UpdateWorkerW() {
            App.Log.Info("WorkerW initializing..");
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

                    App.Log.Error($"Failed to create WorkerW, retrying ({retries})..");
                }
            }
            App.Log.Info($"WorkerW initialized {_workerW}");
            WallpaperReset?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Calculates the position of window w.r.t parent _workerW handle & sets it as child window to it.
        /// </summary>
        /// <param name="handle">window handle of process to add as wallpaper</param>
        /// <param name="targetMonitor">monitorstring of monitor to sent wp to.</param>
        private static bool TrySetWallpaperPerMonitor(IWpPlayer wallpaper, IMonitor targetMonitor) {
            IntPtr handle = wallpaper.Proc.MainWindowHandle;
            RemoveTitleBarAndBorder(handle);

            Native.RECT prct = new();
            App.Log.Info($"Sending wallpaper(Monitor): {targetMonitor.DeviceId} | {targetMonitor.Bounds}");
            //Position the wp fullscreen to corresponding monitor.
            if (!Native.SetWindowPos(handle, 1, targetMonitor.Bounds.X, targetMonitor.Bounds.Y, targetMonitor.Bounds.Width, targetMonitor.Bounds.Height, (int)Native.SWP_NOACTIVATE)) {
                App.Log.Error("Failed to set perscreen wallpaper");
            }

            _ = Native.MapWindowPoints(handle, _workerW, ref prct, 2);
            //Native.SetWindowRect(handle, prct.Left, prct.Top, prct.Right - prct.Left, prct.Bottom - prct.Top);

            wallpaper.SendMessage(new VirtualPaperMessageRECT() {
                X = 0,
                Y = 0,
                Width = prct.Right - prct.Left,
                Height = prct.Bottom - prct.Top
            });
            var success = TrySetParentProgman(handle) && TrySetParentWorkerW(wallpaper.Handle);

            if (success) {
                // Move wallpaper.Handle to the top of the Z order
                Native.SetWindowPos(wallpaper.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                    (int)(Native.SWP_NOACTIVATE | Native.SWP_NOMOVE | Native.SWP_NOSIZE));

                // Alternatively, you can use HWND_TOP instead of HWND_TOPMOST for non-topmost behavior
                //Native.SetWindowPos(handle, Native.HWND_TOP, 0, 0, 0, 0,
                //                  (int)(Native.SWP_NOACTIVATE | Native.SWP_NOMOVE | Native.SWP_NOSIZE));
            }
            DesktopUtil.RefreshDesktop();

            return success;
        }

        private static void RemoveTitleBarAndBorder(nint handle) {
            long style = Native.GetWindowLong(handle, Native.GWL_STYLE);
            style &= ~Native.WS_OVERLAPPEDWINDOW; // 移除边框和标题栏
            style |= Native.WS_POPUP | Native.WS_VISIBLE; // 添加弹出窗口风格和可见性
            Native.SetWindowLong(handle, Native.GWL_STYLE, style);
        }

        /// <summary>
        /// Spans wp across All screens.
        /// </summary>
        private static bool TrySetWallpaperSpanMonitor(IWpPlayer wallpaper) {
            IntPtr handle = wallpaper.Proc.MainWindowHandle;
            RemoveTitleBarAndBorder(handle);

            _ = Native.GetWindowRect(_workerW, out Native.RECT prct);
            App.Log.Info($"Sending wallpaper(Expand): ({prct.Left}, {prct.Top}, {prct.Right - prct.Left}, {prct.Bottom - prct.Top}).");
            //Position the wp fullscreen to corresponding monitor.
            if (!Native.SetWindowPos(handle, 1, 0, 0, prct.Right - prct.Left, prct.Bottom - prct.Top, (int)Native.SWP_NOACTIVATE)) {
                App.Log.Error("Failed to set perscreen wallpaper");
            }

            _ = Native.MapWindowPoints(handle, _workerW, ref prct, 2);
            wallpaper.SendMessage(new VirtualPaperMessageRECT() {
                X = 0,
                Y = 0,
                Width = prct.Right - prct.Left,
                Height = prct.Bottom - prct.Top
            });
            var success = TrySetParentProgman(handle) && TrySetParentWorkerW(wallpaper.Handle);
            if (success) {
                // Move wallpaper.Handle to the top of the Z order
                Native.SetWindowPos(wallpaper.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                    (int)(Native.SWP_NOACTIVATE | Native.SWP_NOMOVE | Native.SWP_NOSIZE));

                // Alternatively, you can use HWND_TOP instead of HWND_TOPMOST for non-topmost behavior
                //Native.SetWindowPos(handle, Native.HWND_TOP, 0, 0, 0, 0,
                //                  (int)(Native.SWP_NOACTIVATE | Native.SWP_NOMOVE | Native.SWP_NOSIZE));
            }
            DesktopUtil.RefreshDesktop();

            return success;
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
        /// Adds the data as child of spawned desktop-_workerW window.
        /// </summary>
        /// <param name="windowHandle">handle of window</param>
        private static bool TrySetParentWorkerW(IntPtr windowHandle) {
            IntPtr ret = Native.SetParent(windowHandle, _workerW);
            if (ret.Equals(IntPtr.Zero))
                return false;

            return true;
        }

        /// <summary>
        /// Adds the data as child of spawned desktop-_progman window.
        /// </summary>
        /// <param name="windowHandle">handle of window</param>
        private static bool TrySetParentProgman(IntPtr windowHandle) {
            IntPtr ret = Native.SetParent(windowHandle, _progman);
            if (ret.Equals(IntPtr.Zero))
                return false;

            return true;
        }

        private async void WorkerWHook_EventReceived(object? sender, WinEventHookEventArgs e) {
            if (e.WindowHandle == _workerW && e.EventType == WindowEvent.EVENT_OBJECT_DESTROY) {
                App.Log.Error("WorkerW destroyed.");
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
                    if (_isInitialized) {
                        try {
                            CloseAllWallpapers();
                            DesktopUtil.RefreshDesktop();
                        }
                        catch (Exception e) {
                            App.Log.Error("Failed to shutdown core: " + e.ToString());
                        }
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
        //private static readonly List<IWpBasicData> _librarywallpapers = [];
        private static readonly Dictionary<(string, RuntimeType), IWpPlayer> _previews = [];
        //private static readonly Dictionary<(string, RuntimeType), IWpPlayer> _wallpapers = [];
        private static nint _workerW, _progman;
        private static bool _isInitialized = false;
        private static readonly SemaphoreSlim _semaphoreSlimWallpaperLoadingLock = new(1, 1);
        private readonly IUserSettingsService _userSettings;
        private readonly IWallpaperFactory _wallpaperFactory;
        private readonly IMonitorManager _monitorManager;
    }
}
