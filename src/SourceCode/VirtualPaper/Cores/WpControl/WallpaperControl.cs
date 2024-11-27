using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using NLog;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files.Models;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.Shell;
using VirtualPaper.Common.Utils.Storage;
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

namespace VirtualPaper.Cores.WpControl {
    internal partial class WallpaperControl : IWallpaperControl {
        public event EventHandler? WallpaperChanged;
        public event EventHandler<Exception>? WallpaperError;
        public event EventHandler? WallpaperReset;

        public nint DesktopWorkerW => _workerW;
        public ReadOnlyCollection<IWallpaperPlaying> Wallpapers => _wallpapers.AsReadOnly();

        public WallpaperControl(
            IUserSettingsService userSettings,
            IMonitorManager monitorManager,
            IWatchdogService watchdog,
            IWallpaperFactory wallpaperFactory) {
            this._userSettings = userSettings;
            this._monitorManager = monitorManager;
            this._watchdog = watchdog;
            this._wallpaperFactory = wallpaperFactory;

            if (SystemParameters.HighContrast)
                _logger.Warn("Highcontrast mode detected, some functionalities may not work properly.");

            this._monitorManager.MonitorUpdated += MonitorSettingsChanged_Hwnd;
            WallpaperChanged += SetupDesktop_WallpaperChanged;

            SystemEvents.SessionSwitch += async (s, e) => {
                if (e.Reason == SessionSwitchReason.SessionUnlock) {

                    if (!(DesktopWorkerW == IntPtr.Zero || Native.IsWindow(DesktopWorkerW))) {
                        _logger.Info("WorkerW invalid after unlock, resetting..");
                        await ResetWallpaperAsync();
                    }
                    else {
                        if (Wallpapers.Any(x => x.IsExited)) {
                            _logger.Info("Wallpaper crashed after unlock, resetting..");
                            await ResetWallpaperAsync();
                        }
                    }
                }
            };

            // Initialize WorkerW
            UpdateWorkerW();

            try {
                if (_workerW != IntPtr.Zero) {
                    _logger.Info("Hooking WorkerW events..");
                    var dwThreadId = Native.GetWindowThreadProcessId(_workerW, out int dwProcessId);
                    _workerWHook = new WindowEventHook(WindowEvent.EVENT_OBJECT_DESTROY);
                    _workerWHook.HookToThread(dwThreadId);
                    _workerWHook.EventReceived += WorkerWHook_EventReceived;
                }
                else {
                    _logger.Error("Failed to initialize Core, WorkerW is NULL");
                }
            }
            catch (Exception ex) {
                _logger.Error($"WorkerW hook failed: {ex.Message}");
            }
        }

        #region wallpaper actions
        public void CloseAllWallpapers() {
            if (_wallpapers.Count > 0) {
                _wallpapers.ForEach(x => x.Close());
                _wallpapers.Clear();
                _watchdog.Clear();
                WallpaperChanged?.Invoke(this, EventArgs.Empty);
            }
            _logger.Info("Closed all wallpapers");
        }

        public void CloseWallpaper(IMonitor monitor) {
            var tmp = _wallpapers.FindAll(x => x.Monitor.Equals(monitor));
            if (tmp.Count > 0) {
                tmp.ForEach(x => {
                    x.Close();
                    if (x.Proc != null) {
                        _watchdog.Remove(x.Proc.Id);
                    }
                });
                _wallpapers.RemoveAll(tmp.Contains);
                WallpaperChanged?.Invoke(this, EventArgs.Empty);

                _logger.Info("Closed wallpaper at monitor: " + monitor.DeviceName);
            }
        }

        public IWpMetadata GetWallpaper(string folderPath) {
            IWpMetadata metaData = WallpaperUtil.GetWallpaperByFolder(folderPath);

            return metaData;
        }

        public async Task<bool> PreviewWallpaperAsync(IWpPlayerData data, bool isCurrentWp) {
            IWallpaperPlaying? playingData;
            playingData = _wallpapers.Find(x => x.Data.WallpaperUid == data.WallpaperUid);
            if (playingData != null) {
                playingData.SendMessage(new VirtualPaperPreviewOnCmd());
                return true;
            }

            _previews.TryGetValue((data.WallpaperUid, data.RType), out playingData);
            if (playingData != null) {
                playingData.SendMessage(new VirtualPaperPreviewOnCmd());
                return true;
            }

            playingData = _wallpaperFactory.CreatePlayer(
                data,
                _monitorManager.PrimaryMonitor,
                _userSettings,
                true);
            playingData.Closing += IWallpaperPlayingClosing;
            void IWallpaperPlayingClosing(object? s, EventArgs e) {
                playingData.Closing -= IWallpaperPlayingClosing;
                _wallpapers.Remove(playingData);
                _previews.Remove((data.WallpaperUid, data.RType));
                _watchdog.Remove(playingData.Proc.Id);
            }
            _previews[(data.WallpaperUid, data.RType)] = playingData;
            bool isStarted = await playingData.ShowAsync();

            if (isStarted && playingData.Proc != null) {
                _watchdog.Add(playingData.Proc.Id);
            }

            return isStarted;
        }

        public async Task ResetWallpaperAsync() {
            await _semaphoreSlimWallpaperLoadingLock.WaitAsync();

            try {
                _logger.Info("Restarting wallpaper service..");
                // Copy existing wallpapers
                var originalWallpapers = Wallpapers.ToList();
                CloseAllWallpapers();
                // Restart _workerW
                UpdateWorkerW();
                if (_workerW == IntPtr.Zero) {
                    // Final attempt
                    _logger.Info("Retry creating WorkerW after delay..");
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
                _logger.Info("Restore wallpapers...");
                var wallpaperLayout = _userSettings.WallpaperLayouts.ToList();
                if (_userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Expand ||
                    _userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Duplicate) {
                    if (wallpaperLayout.Count != 0) {
                        var metaData = WallpaperUtil.GetWallpaperByFolder(wallpaperLayout[0].FolderPath);
                        SetWallpaperAsync(metaData.GetPlayerData(), _monitorManager.PrimaryMonitor);
                    }
                }
                else if (_userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Per) {
                    RestoreWallpaper(wallpaperLayout);
                }

                response.IsFinished = true;
            }
            catch (Exception e) {
                _userSettings.WallpaperLayouts.Clear();
                _userSettings.Save<List<IWallpaperLayout>>();
                _logger.Error($"Failed to restore wallpaper: {e}");
            }

            return response;
        }

        public async Task<Grpc_SetWallpaperResponse> SetWallpaperAsync(
            IWpPlayerData data,
            IMonitor monitor,
            CancellationToken token = default) {
            await _semaphoreSlimWallpaperLoadingLock.WaitAsync(token);
            Grpc_SetWallpaperResponse response = new();

            try {
                _logger.Info($"Setting wallpaper: {data.FilePath}");
                #region init
                if (!_isInitialized) {
                    if (SystemParameters.HighContrast) {
                        _logger.Warn("Highcontrast mode detected, some functionalities may not work properly!");
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
                        _logger.Error("Failed to setup core, WorkerW handle not found..");
                        WallpaperError?.Invoke(this, new WorkerWException(LanguageManager.Instance
                            ["WpControl_VirtualPaperExceptionWorkerWSetupFail"]));
                        WallpaperChanged?.Invoke(this, EventArgs.Empty);

                        response.IsFinished = false;

                        return response;
                    }
                    else {
                        _logger.Info("Core initialized..");
                        _isInitialized = true;
                        WallpaperReset?.Invoke(this, EventArgs.Empty);
                        _watchdog.Start();
                    }
                }

                if (!_monitorManager.MonitorExists(monitor)) {
                    _logger.Info($"Skipping wallpaper, monitor {monitor.DeviceName} not found.");
                    WallpaperError?.Invoke(this, new ScreenNotFoundException($"Mnotir {monitor.DeviceName} not found."));

                    response.IsFinished = false;

                    return response;
                }
                else if (!File.Exists(data.FilePath)) {
                    //Only checking for wallpapers outside folder.
                    //This was before core separation, now the check can be simplified with just FolderPath != null.
                    _logger.Info($"Skipping wallpaper, file {data.FilePath} not found.");
                    WallpaperError?.Invoke(this, new WallpaperNotFoundException($"{LanguageManager.Instance
                            ["WpControl_TextFileNotFound"]}\n{data.FilePath}"));
                    WallpaperChanged?.Invoke(this, EventArgs.Empty);

                    response.IsFinished = false;

                    return response;
                }

                if (!_watchdog.IsRunning)
                    _watchdog.Start();
                #endregion

                bool isStarted = false;
                try {
                    switch (_userSettings.Settings.WallpaperArrangement) {
                        case WallpaperArrangement.Per: {
                                IWallpaperPlaying instance = _wallpaperFactory.CreatePlayer(data, monitor, _userSettings);
                                CloseWallpaper(instance.Monitor);
                                isStarted = await instance.ShowAsync(token);

                                if (isStarted && !TrySetWallpaperPerMonitor(instance, instance.Monitor)) {
                                    isStarted = false;
                                    _logger.Error("Failed to set wallpaper as child of WorkerW");

                                    response.IsFinished = false;
                                }

                                if (isStarted) {
                                    if (instance.Proc != null)
                                        _watchdog.Add(instance.Proc.Id);

                                    _wallpapers.Add(instance);
                                    monitor.ThumbnailPath = data.ThumbnailPath;
                                }
                            }
                            break;
                        case WallpaperArrangement.Expand: {
                                CloseAllWallpapers();
                                IWallpaperPlaying instance = _wallpaperFactory.CreatePlayer(data, monitor, _userSettings);
                                isStarted = await instance.ShowAsync(token);

                                if (isStarted && !TrySetWallpaperSpanMonitor(instance)) {
                                    isStarted = false;
                                    _logger.Error("Failed to set wallpaper as child of WorkerW");

                                    response.IsFinished = false;
                                }

                                if (isStarted) {
                                    if (instance.Proc != null)
                                        _watchdog.Add(instance.Proc.Id);

                                    _wallpapers.Add(instance);
                                    monitor.ThumbnailPath = data.ThumbnailPath;
                                }
                            }
                            break;
                        case WallpaperArrangement.Duplicate: {
                                CloseAllWallpapers();
                                foreach (var item in _monitorManager.Monitors) {
                                    IWallpaperPlaying instance = _wallpaperFactory.CreatePlayer(data, item, _userSettings);
                                    isStarted = await instance.ShowAsync(token);

                                    if (isStarted && !TrySetWallpaperPerMonitor(instance, instance.Monitor)) {
                                        isStarted = false;
                                        _logger.Error("Failed to set wallpaper as child of WorkerW");

                                        response.IsFinished = false;
                                    }

                                    if (isStarted) {
                                        if (instance.Proc != null)
                                            _watchdog.Add(instance.Proc.Id);

                                        _wallpapers.Add(instance);
                                        monitor.ThumbnailPath = data.ThumbnailPath;
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
                    _logger.Error(ex);
                    if (ex.NativeErrorCode == 2) //ERROR_FILE_NOT_FOUND
                        WallpaperError?.Invoke(this, new WallpaperPluginNotFoundException(ex.Message));
                    else
                        WallpaperError?.Invoke(this, ex);
                }
                catch (Exception ex2) {
                    _logger.Error(ex2);
                    WallpaperError?.Invoke(this, ex2);
                    WallpaperChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception e) {
                _logger.Error(e.ToString());
                WallpaperError?.Invoke(this, new WallpaperPluginNotFoundException(e.Message));
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

        public void UpdateWallpaper(string monitorId, IWpPlayerData data, CancellationToken token) {
            try {
                int idx = _wallpapers.FindIndex(x => x.Monitor.DeviceId == monitorId);
                if (idx == -1) return;

                _wallpapers[idx].Update(data);
                WallpaperChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) {
                _logger.Info(ex);
            }
        }
        #endregion

        #region data
        public IWpBasicData CreateMetadataBasic(
            string folderPath,
            string filePath,
            FileType ftype,
            CancellationToken token) {
            WpBasicData data = new();
            string wpFolderPath = string.Empty;

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
                wpFolderPath = Path.Combine(folderPath, folderName);
                data.FolderPath = wpFolderPath;

                // 创建壁纸存储路径与自定义配置文件路径,将原壁纸复制到 folder 下
                Directory.CreateDirectory(wpFolderPath);
                string destFilePath = Path.Combine(wpFolderPath, folderName + Path.GetExtension(filePath));
                File.Copy(filePath, destFilePath, true);
                data.FilePath = destFilePath;

                #region 创建展示缩略图
                string thumbnailPath = Path.Combine(wpFolderPath, folderName + Constants.Field.ThumGifSuff);
                WallpaperUtil.CreateGif(filePath, thumbnailPath, ftype, token);
                data.ThumbnailPath = thumbnailPath;
                #endregion

                #region 文件元数据
                var fileProperty = WallpaperUtil.GetWpProperty(filePath, ftype);
                data.Resolution = fileProperty.Resolution;
                data.AspectRatio = fileProperty.AspectRatio;
                data.FileSize = fileProperty.FileSize;
                data.FileExtension = fileProperty.FileExtension;

                string basciDatafilePath = Path.Combine(wpFolderPath, Constants.Field.WpBasicDataFileName);
                JsonStorage<WpBasicData>.StoreData(basciDatafilePath, data);
                #endregion
            }
            catch (Exception ex) {
                _logger.Error(ex);

                if (Directory.Exists(wpFolderPath)) {
                    Directory.Delete(wpFolderPath, true);
                }
            }

            return data;
        }

        public IWpRuntimeData CreateMetadataRuntime(
            string filePath,
            string folderPath,
            RuntimeType rtype) {
            WpRuntimeData data = new();
            string wpEffectFilePathTemplate = string.Empty;
            string wpEffectFilePathTemporary = string.Empty;
            string wpEffectFilePathUsing = string.Empty;

            try {
                data.AppInfo = new() {
                    AppName = _userSettings.Settings.AppName,
                    AppVersion = _userSettings.Settings.AppVersion,
                    FileVersion = _userSettings.Settings.FileVersion,
                };
                data.FolderPath = folderPath;
                data.RType = rtype;

                wpEffectFilePathTemplate =
                    WallpaperUtil.CreateWpEffectFileTemplate(
                        folderPath,
                        rtype);
                data.WpEffectFilePathTemplate = wpEffectFilePathTemplate;

                wpEffectFilePathTemporary =
                    WallpaperUtil.CreateWpEffectFileTemporary(
                        folderPath,
                        wpEffectFilePathTemplate);
                data.WpEffectFilePathTemporary = wpEffectFilePathTemporary;

                if (rtype == RuntimeType.RImage3D) {
                    var output = MiDaS.Run(filePath);
                    string depthFilePath = MiDaS.SaveDepthMap(output.Depth, output.Width, output.Height, output.OriginalWidth, output.OriginalHeight, folderPath);
                    data.DepthFilePath = depthFilePath;
                }
            }
            catch (Exception ex) {
                _logger.Error(ex);

                File.Delete(wpEffectFilePathTemplate);
                File.Delete(wpEffectFilePathTemporary);
                File.Delete(wpEffectFilePathUsing);
            }

            return data;
        }

        public string CreateMetadataRuntimeUsing(
            string folderPath,
            string wpEffectFilePathTemplate,
            string monitorContent) {
            string wpEffectFilePathUsing = string.Empty;

            try {
                wpEffectFilePathUsing =
                   WallpaperUtil.CreateWpEffectFileUsing(
                       folderPath,
                       wpEffectFilePathTemplate,
                       monitorContent,
                       _userSettings.Settings.WallpaperArrangement);
            }
            catch (Exception ex) {
                _logger.Error(ex);

                File.Delete(wpEffectFilePathUsing);
            }

            return wpEffectFilePathUsing;
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
                string coverFilePath = Path.Combine(folderPath, folderName + "_cover.gif");
                WallpaperUtil.CreateGif(filePath, coverFilePath, ftype, token);
                data.ThumbnailPath = coverFilePath;
                #endregion

                #region 文件元数据
                var fileProperty = WallpaperUtil.GetWpProperty(filePath, ftype);
                data.Resolution = fileProperty.Resolution;
                data.AspectRatio = fileProperty.AspectRatio;
                data.FileSize = fileProperty.FileSize;
                data.FileExtension = fileProperty.FileExtension;

                string basciDatafilePath = Path.Combine(folderPath, Constants.Field.WpBasicDataFileName);
                JsonStorage<WpBasicData>.StoreData(basciDatafilePath, data);
                #endregion
            }
            catch (Exception ex) {
                _logger.Error(ex);
            }

            return data;
        }

        public IWpRuntimeData UpdateMetadataRuntime(
            string folderPath,
            RuntimeType rtype,
            CancellationToken token) {
            WpRuntimeData data = new();
            string wpEffectFilePathTemplate = string.Empty;
            string wpEffectFilePathTemporary = string.Empty;

            try {
                data.AppInfo = new() {
                    AppName = _userSettings.Settings.AppName,
                    AppVersion = _userSettings.Settings.AppVersion,
                    FileVersion = _userSettings.Settings.FileVersion,
                };
                data.FolderPath = folderPath;

                wpEffectFilePathTemplate =
                    WallpaperUtil.CreateWpEffectFileTemplate(
                        folderPath,
                        rtype);
                data.WpEffectFilePathTemplate = wpEffectFilePathTemplate;

                wpEffectFilePathTemporary =
                   WallpaperUtil.CreateWpEffectFileTemporary(
                       folderPath,
                       wpEffectFilePathTemplate);
                data.WpEffectFilePathTemporary = wpEffectFilePathTemporary;
            }
            catch (Exception ex) {
                _logger.Error(ex);

                File.Delete(wpEffectFilePathTemplate);
                File.Delete(wpEffectFilePathTemporary);
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

        //public void ModifyPreview(string controlName, string propertyName, string value) {
        //    if (_previewInstance == null) return;
        //    Application.Current.Dispatcher.Invoke(() => {
        //        _previewInstance.Modify(controlName, propertyName, value);
        //    });
        //}
        #endregion

        #region private utils
        private readonly object _monitorSettingsChangedLock = new();
        private void MonitorSettingsChanged_Hwnd(object? sender, EventArgs e) {
            lock (_monitorSettingsChangedLock) {
                _logger.Info("Monitor settings changed, monitor(s):");
                _monitorManager.Monitors.ToList().ForEach(x => _logger.Info(x.DeviceName + " " + x.Bounds));

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
                                _logger.Info($"Disconnected Screen: {x.Monitor.DeviceName} {x.Monitor.Bounds}");
                                x.Close();
                            });

                            _wallpapers.RemoveAll(x => orphanWallpapers.Contains(x));
                        }
                        break;
                    case WallpaperArrangement.Duplicate:
                        if (orphanWallpapers.Count != 0) {
                            orphanWallpapers.ForEach(x => {
                                _logger.Info($"Disconnected Screen: {x.Monitor.DeviceName} {x.Monitor.Bounds}");
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
                _logger.Error(ex.ToString());
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
                    _logger.Info($"Updating data rect(Expand): ({screenArea.Width}, {screenArea.Height}).");
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
                        _logger.Info($"Updating data rect(Screen): {Wallpapers[i].Monitor.Bounds} -> {screen.Bounds}.");
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
            foreach (var layout in wallpaperLayout) {
                IWpMetadata? metaData = null;
                try {
                    metaData = WallpaperUtil.GetWallpaperByFolder(layout.FolderPath);
                    if (metaData == null) throw new();
                }
                catch (Exception e) {
                    _logger.Info($"Skipping restoration of {layout.FolderPath} | {e.Message}");
                }

                var screen = _monitorManager.Monitors.FirstOrDefault(x => x.Equals(layout.Monitor));
                if (screen == null) {
                    _logger.Info($"Screen missing, skipping restoration of {layout.FolderPath} | {layout.Monitor.DeviceName}");
                }
                else {
                    _logger.Info($"Restoring data: {metaData.BasicData.FolderPath}");
                    SetWallpaperAsync(metaData.GetPlayerData(), screen);
                }
            }
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
                            (Models.Cores.Monitor)wallpaper.Monitor,
                            wallpaper.Data.FolderPath));
                });

                try {
                    _userSettings.Save<List<IWallpaperLayout>>();
                }
                catch (Exception e) {
                    _logger.Error(e.ToString());
                }
            }
        }

        private void UpdateWorkerW() {
            _logger.Info("WorkerW initializing..");
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

                    _logger.Error($"Failed to create WorkerW, retrying ({retries})..");
                }
            }
            _logger.Info($"WorkerW initialized {_workerW}");
            WallpaperReset?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Calculates the position of window w.r.t parent _workerW handle & sets it as child window to it.
        /// </summary>
        /// <param name="handle">window handle of process to add as wallpaper</param>
        /// <param name="targetMonitor">monitorstring of monitor to sent wp to.</param>
        private bool TrySetWallpaperPerMonitor(IWallpaperPlaying wallpaper, IMonitor targetMonitor) {
            IntPtr handle = wallpaper.Handle;
            Native.RECT prct = new();
            _logger.Info($"Sending wallpaper(Monitor): {targetMonitor.DeviceName} | {targetMonitor.Bounds}");
            //Position the wp fullscreen to corresponding monitor.
            if (!Native.SetWindowPos(handle, 1, targetMonitor.Bounds.X, targetMonitor.Bounds.Y, targetMonitor.Bounds.Width, targetMonitor.Bounds.Height, 0x0010)) {
                _logger.Error("Failed to set perscreen wallpaper(1)");
            }

            _ = Native.MapWindowPoints(handle, _workerW, ref prct, 2);
            var success = TrySetParentWorkerW(handle);

            //wallpaper.SendMessage(new VirtualPaperMessageRECT() {
            //    X = prct.Left,
            //    Y = prct.Top,
            //    Width = targetMonitor.Bounds.Width,
            //    Height = targetMonitor.Bounds.Height
            //});

            //Position the wp window relative to the new parent window(_workerW).
            if (!Native.SetWindowPos(handle, 1, prct.Left, prct.Top, targetMonitor.Bounds.Width, targetMonitor.Bounds.Height, 0x0010)) {
                _logger.Error("Failed to set perscreen wallpaper(2)");
            }
            DesktopUtil.RefreshDesktop();

            return success;
        }

        /// <summary>
        /// Spans wp across All screens.
        /// </summary>
        private bool TrySetWallpaperSpanMonitor(IWallpaperPlaying wallpaper) {
            IntPtr handle = wallpaper.Handle;
            //get spawned _workerW rectangle data.
            _ = Native.GetWindowRect(_workerW, out Native.RECT prct);
            var success = TrySetParentWorkerW(handle);

            //fill wp into the whole _workerW area.
            wallpaper.SendMessage(new VirtualPaperMessageRECT() {
                X = 0,
                Y = 0,
                Width = prct.Right - prct.Left,
                Height = prct.Bottom - prct.Top
            });

            _logger.Info($"Sending wallpaper(Expand): ({prct.Left}, {prct.Top}, {prct.Right - prct.Left}, {prct.Bottom - prct.Top}).");
            if (!Native.SetWindowPos(handle, 1, 0, 0, prct.Right - prct.Left, prct.Bottom - prct.Top, 0x0010)) {
                //LogUtil.LogWin32Error("Failed to set Expand wallpaper");
            }
            DesktopUtil.RefreshDesktop();
            return success;
        }

        private static IntPtr CreateWorkerW() {
            // Fetch the Progman window
            var progman = Native.FindWindow("Progman", null);

            IntPtr result = IntPtr.Zero;

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
            // 0x00100B8A "" WorkerW       <-- This is the WorkerW instance we are after!
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
            //   0x00100B8A "" WorkerW       <-- This is the WorkerW instance we are after!
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
        private bool TrySetParentWorkerW(IntPtr windowHandle) {
            //Win7
            if (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor == 1) {
                var progman = Native.FindWindow("Progman", null);
                if (!_workerW.Equals(progman)) //this should fix the win7 wallpaper disappearing issue.
                    Native.ShowWindow(_workerW, (uint)0);

                IntPtr ret = Native.SetParent(windowHandle, progman);
                if (ret.Equals(IntPtr.Zero))
                    return false;

                //_workerW is assumed as _progman in win7, this is untested with All fn's: addwallpaper(), wp Pause, resize events.. 
                _workerW = progman;
            }
            else {
                IntPtr ret = Native.SetParent(windowHandle, _workerW);
                if (ret.Equals(IntPtr.Zero))
                    return false;
            }
            return true;
        }

        private async void WorkerWHook_EventReceived(object? sender, WinEventHookEventArgs e) {
            if (e.WindowHandle == _workerW && e.EventType == WindowEvent.EVENT_OBJECT_DESTROY) {
                _logger.Error("WorkerW destroyed.");
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
                            _logger.Error("Failed to shutdown core: " + e.ToString());
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

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly WindowEventHook _workerWHook;
        private readonly List<IWallpaperPlaying> _wallpapers = [];
        private readonly Dictionary<(string, RuntimeType), IWallpaperPlaying> _previews = [];
        private IntPtr _progman, _workerW;
        private bool _isInitialized = false;
        //private IWallpaperPlaying? _previewInstance;
        private readonly SemaphoreSlim _semaphoreSlimWallpaperLoadingLock = new(1, 1);

        private readonly IUserSettingsService _userSettings;
        private readonly IWallpaperFactory _wallpaperFactory;
        private readonly IWatchdogService _watchdog;
        private readonly IMonitorManager _monitorManager;
    }
}
