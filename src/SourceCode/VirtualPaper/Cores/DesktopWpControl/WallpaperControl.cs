using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NLog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files.Models;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.Shell;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils;
using WinEventHook;
using static VirtualPaper.Common.Errors;
using Application = System.Windows.Application;
using WallpaperType = VirtualPaper.Common.WallpaperType;

namespace VirtualPaper.Cores.Desktop
{
    internal class WallpaperControl : IWallpaperControl
    {
        public nint DesktopWorkerW => _workerW;
        public ReadOnlyCollection<IWallpaper> Wallpapers => _wallpapers.AsReadOnly();

        public event EventHandler<WallpaperUpdateArgs>? WallpaperUpdated;
        public event EventHandler? WallpaperChanged;
        public event EventHandler<Exception>? WallpaperError;
        public event EventHandler? WallpaperReset;

        public WallpaperControl(IUserSettingsService userSettings,
            IMonitorManager monitorManager,
            ITaskbarService tkbService,
            IWatchdogService watchdog,
            IUIRunnerService uiRunner,
            IWallpaperFactory wallpaperFactory)
        {
            this._userSettings = userSettings;
            this._monitorManager = monitorManager;
            this._watchdog = watchdog;
            this._wallpaperFactory = wallpaperFactory;

            if (SystemParameters.HighContrast)
                _logger.Warn("Highcontrast mode detected, some functionalities may not work properly.");

            this._monitorManager.MonitorUpdated += MonitorSettingsChanged_Hwnd;
            WallpaperChanged += SetupDesktop_WallpaperChanged;

            SystemEvents.SessionSwitch += async (s, e) =>
            {
                if (e.Reason == SessionSwitchReason.SessionUnlock)
                {

                    if (!(DesktopWorkerW == IntPtr.Zero || Native.IsWindow(DesktopWorkerW)))
                    {
                        _logger.Info("WorkerW invalid after unlock, resetting..");
                        await ResetWallpaperAsync();
                    }
                    else
                    {
                        if (Wallpapers.Any(x => x.IsExited))
                        {
                            _logger.Info("Wallpaper crashed after unlock, resetting..");
                            await ResetWallpaperAsync();
                        }
                    }
                }
            };

            // Initialize WorkerW
            UpdateWorkerW();

            try
            {
                if (_workerW != IntPtr.Zero)
                {
                    _logger.Info("Hooking WorkerW events..");
                    var dwThreadId = Native.GetWindowThreadProcessId(_workerW, out int dwProcessId);
                    _workerWHook = new WindowEventHook(WindowEvent.EVENT_OBJECT_DESTROY);
                    _workerWHook.HookToThread(dwThreadId);
                    _workerWHook.EventReceived += WorkerWHook_EventReceived;
                }
                else
                {
                    _logger.Error("Failed to initialize Core, WorkerW is NULL");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"WorkerW hook failed: {ex.Message}");
            }
        }

        private async void WorkerWHook_EventReceived(object? sender, WinEventHookEventArgs e)
        {
            if (e.WindowHandle == _workerW && e.EventType == WindowEvent.EVENT_OBJECT_DESTROY)
            {
                _logger.Error("WorkerW destroyed.");
                await ResetWallpaperAsync();
            }
        }

        public void CloseAllWallpapers()
        {
            if (_wallpapers.Count > 0)
            {
                _wallpapers.ForEach(x => x.Close());
                _wallpapers.Clear();
                _watchdog.Clear();
                WallpaperChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void CloseWallpaper(IMonitor monitor)
        {
            var tmp = _wallpapers.FindAll(x => x.Monitor.Equals(monitor));
            if (tmp.Count > 0)
            {
                tmp.ForEach(x =>
                {
                    if (x.Proc != null)
                    {
                        _watchdog.Remove(x.Proc.Id);
                    }
                    x.Close();
                });
                _wallpapers.RemoveAll(x => tmp.Contains(x));

                WallpaperChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task ResetWallpaperAsync()
        {
            await _semaphoreSlimWallpaperLoadingLock.WaitAsync();

            try
            {
                _logger.Info("Restarting wallpaper service..");
                // Copy existing wallpapers
                var originalWallpapers = Wallpapers.ToList();
                CloseAllWallpapers();
                // Restart _workerW
                UpdateWorkerW();
                if (_workerW == IntPtr.Zero)
                {
                    // Final attempt
                    _logger.Info("Retry creating WorkerW after delay..");
                    await Task.Delay(500);
                    UpdateWorkerW();
                }
                foreach (var item in originalWallpapers)
                {
                    SetWallpaperAsync(item.MetaData, item.Monitor);
                    if (_userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Duplicate)
                        break;
                }
            }
            finally
            {
                _semaphoreSlimWallpaperLoadingLock.Release();
            }
        }

        public void RestoreWallpaper()
        {
            try
            {
                var wallpaperLayout = _userSettings.WallpaperLayout;
                if (_userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Span ||
                    _userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Duplicate)
                {
                    if (wallpaperLayout.Count != 0)
                    {
                        var metaData = WallpaperUtil.ScanWallpaperFolder(wallpaperLayout[0].FolderPath);
                        SetWallpaperAsync(metaData, _monitorManager.PrimaryMonitor);
                    }
                }
                else if (_userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Per)
                {
                    RestoreWallpaper(wallpaperLayout);
                }
            }
            catch (Exception e)
            {
                _logger.Error($"Failed to restore wallpaper: {e}");
            }
        }

        public void PreviewWallpaper(IMetaData metaData)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IWallpaper instance = _wallpaperFactory.CreateWallpaper(metaData, _monitorManager.PrimaryMonitor, _userSettings, true);
                instance.ShowAsync();
            });
        }

        public void SeekWallpaper(IMetaData metadData, float seek, PlaybackPosType type)
        {
            _wallpapers.ForEach(x =>
            {
                if (x.MetaData == metadData)
                {
                    x.SetPlaybackPos(seek, type);
                }
            });
        }

        public void SeekWallpaper(IMonitor monitor, float seek, PlaybackPosType type)
        {
            _wallpapers.ForEach(x =>
            {
                if (x.Monitor == monitor)
                {
                    x.SetPlaybackPos(seek, type);
                }
            });
        }

        public void SendMessageWallpaper(string infoPath, IpcMessage msg)
        {

        }

        public void SendMessageWallpaper(IMonitor monitor, string infoPath, IpcMessage msg)
        {

        }

        public IMetaData CreateWallpaper(string folderPath, string filePath, WallpaperType type, CancellationToken token)
        {
            MetaData metaData = new();

            try
            {
                // 创建随机不重复文件夹，更新 folderPath
                string dirName = Path.GetRandomFileName();
                folderPath = Path.Combine(folderPath, Constants.CommonPartialPaths.WallpaperInstallDir, dirName);

                // 创建壁纸存储路径与自定义配置文件路径
                Directory.CreateDirectory(folderPath);

                // 创建自定义配置文件
                string wpCustomizePath = Path.Combine(folderPath, "WpCustomize.json");
                WallpaperUtil.CreateCustomizeFile(wpCustomizePath, type);

                // 将原壁纸复制到 folder 下
                string copyFilePath = Path.Combine(folderPath, Path.GetRandomFileName() + Path.GetExtension(filePath));
                File.Copy(filePath, copyFilePath, true);

                // 创建展示缩略图
                string thumbnailPath = Path.Combine(folderPath, Path.GetRandomFileName() + ".gif");
                WallpaperUtil.TryCreateGif(filePath, thumbnailPath, type, token);
                //WpfUtil.CreateGifByImages(thumbnailPath, frames, token);

                metaData.Type = type;
                metaData.FolderPath = folderPath;
                metaData.FilePath = filePath;
                metaData.ThumbnailPath = thumbnailPath;
                metaData.WpCustomizePath = wpCustomizePath;
            }
            catch (Exception)
            {
                // 清理可能产生的临时文件或资源
                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, true);
                }
            }

            return metaData;
        }

        public FileProperty TryGetProeprtyInfo(string filePath, WallpaperType type)
        {
            return WallpaperUtil.TryGetProeprtyInfo(filePath, type);
        }

        public IMetaData GetWallpaper(string folderPath)
        {
            IMetaData metaData = WallpaperUtil.ScanWallpaperFolder(folderPath);

            return metaData;
        }

        public async Task<SetWallpaperResponse> SetWallpaperAsync(
            IMetaData metaData,
            IMonitor monitor,
            CancellationToken cancellationToken = default)
        {
            await _semaphoreSlimWallpaperLoadingLock.WaitAsync(cancellationToken);
            SetWallpaperResponse response = new()
            {
                IsWorked = true,
            };

            try
            {
                _logger.Info($"Setting wallpaper: {metaData.Title} | {metaData.FilePath}");
                if (!_isInitialized)
                {
                    if (SystemParameters.HighContrast)
                    {
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
                    Native.EnumWindows(new Native.EnumWindowsProc((tophandle, topparamhandle) =>
                    {
                        IntPtr p = Native.FindWindowEx(tophandle,
                                                    IntPtr.Zero,
                                                    "SHELLDLL_DefView",
                                                    IntPtr.Zero);

                        if (p != IntPtr.Zero)
                        {
                            // Gets the WorkerW Window after the current one.
                            _workerW = Native.FindWindowEx(IntPtr.Zero,
                                                           tophandle,
                                                           "WorkerW",
                                                           IntPtr.Zero);
                        }

                        return true;
                    }), IntPtr.Zero);

                    if (IntPtr.Equals(_workerW, IntPtr.Zero))
                    {
                        _logger.Error("Failed to setup core, WorkerW handle not found..");
                        WallpaperError?.Invoke(this, new WorkerWException(App.GetResourceDicString("WpControl_VirtualPaperExceptionWorkerWSetupFail")));
                        WallpaperChanged?.Invoke(this, EventArgs.Empty);

                        response.IsWorked = false;
                        response.Msg = "WpControl_Err_Setup_Core_Failed";

                        return response;
                    }
                    else
                    {
                        _logger.Info("Core initialized..");
                        _isInitialized = true;
                        WallpaperReset?.Invoke(this, EventArgs.Empty);
                        _watchdog.Start();
                    }
                }

                if (!_monitorManager.MonitorExists(monitor))
                {
                    _logger.Info($"Skipping wallpaper, monitor {monitor.DeviceName} not found.");
                    WallpaperError?.Invoke(this, new ScreenNotFoundException($"Mnotir {monitor.DeviceName} not found."));

                    response.IsWorked = false;
                    response.Msg = "WpControl_Err_Monitor_Not_Found";

                    return response;
                }
                else if (!File.Exists(metaData.FilePath))
                {
                    //Only checking for wallpapers outside folder.
                    //This was before core separation, now the check can be simplified with just FolderPath != null.
                    _logger.Info($"Skipping wallpaper, file {metaData.FilePath} not found.");
                    WallpaperError?.Invoke(this, new WallpaperNotFoundException($"{App.GetResourceDicString("WpControl_TextFileNotFound")}\n{metaData.FilePath}"));
                    WallpaperChanged?.Invoke(this, EventArgs.Empty);

                    response.IsWorked = false;
                    response.Msg = "WpControl_Err_File_Not_Found";

                    return response;
                }

                if (!_watchdog.IsRunning)
                    _watchdog.Start();

                bool isStarted = false;
                try
                {
                    switch (_userSettings.Settings.WallpaperArrangement)
                    {
                        case WallpaperArrangement.Per:
                            {
                                IWallpaper instance = _wallpaperFactory.CreateWallpaper(metaData, monitor, _userSettings);
                                CloseWallpaper(instance.Monitor);
                                isStarted = await instance.ShowAsync(cancellationToken);

                                if (isStarted && !TrySetWallpaperPerMonitor(instance.Handle, instance.Monitor))
                                {
                                    isStarted = false;
                                    _logger.Error("Failed to set wallpaper as child of WorkerW");

                                    response.IsWorked = false;
                                    response.Msg = "WpControl_Err_Set_Child_WokerW_Failed";
                                }

                                if (isStarted)
                                {
                                    if (instance.Proc != null)
                                        _watchdog.Add(instance.Proc.Id);

                                    _wallpapers.Add(instance);
                                }
                            }
                            break;
                        case WallpaperArrangement.Span:
                            {
                                CloseAllWallpapers();
                                IWallpaper instance = _wallpaperFactory.CreateWallpaper(metaData, monitor, _userSettings);
                                isStarted = await instance.ShowAsync(cancellationToken);

                                if (isStarted && !TrySetWallpaperSpanMonitor(instance.Handle))
                                {
                                    isStarted = false;
                                    _logger.Error("Failed to set wallpaper as child of WorkerW");

                                    response.IsWorked = false;
                                    response.Msg = "WpControl_Err_Set_Child_WokerW_Failed";
                                }

                                if (isStarted)
                                {
                                    if (instance.Proc != null)
                                        _watchdog.Add(instance.Proc.Id);

                                    _wallpapers.Add(instance);
                                }
                            }
                            break;
                        case WallpaperArrangement.Duplicate:
                            {
                                CloseAllWallpapers();
                                foreach (var item in _monitorManager.Monitors)
                                {
                                    IWallpaper instance = _wallpaperFactory.CreateWallpaper(metaData, item, _userSettings);
                                    isStarted = await instance.ShowAsync(cancellationToken);

                                    if (isStarted && !TrySetWallpaperPerMonitor(instance.Handle, instance.Monitor))
                                    {
                                        isStarted = false;
                                        _logger.Error("Failed to set wallpaper as child of WorkerW");

                                        response.IsWorked = false;
                                        response.Msg = "WpControl_Err_Set_Child_WokerW_Failed";
                                    }

                                    if (isStarted)
                                    {
                                        if (instance.Proc != null)
                                            _watchdog.Add(instance.Proc.Id);

                                        _wallpapers.Add(instance);
                                    }
                                }
                            }
                            break;
                    }
                    if (isStarted)
                        WallpaperChanged?.Invoke(this, EventArgs.Empty);
                }
                catch (Win32Exception ex)
                {
                    response.IsWorked = false;

                    _logger.Error(ex);
                    if (ex.NativeErrorCode == 2) //ERROR_FILE_NOT_FOUND
                        WallpaperError?.Invoke(this, new WallpaperPluginNotFoundException(ex.Message));
                    else
                        WallpaperError?.Invoke(this, ex);
                }
                catch (Exception ex2)
                {
                    response.IsWorked = false;

                    _logger.Error(ex2);
                    WallpaperError?.Invoke(this, ex2);
                    WallpaperChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception e)
            {
                response.IsWorked = false;

                _logger.Error(e.ToString());
                WallpaperError?.Invoke(this, new WallpaperPluginNotFoundException(e.Message));
                WallpaperChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _semaphoreSlimWallpaperLoadingLock.Release();
            }

            return response;
        }

        private object _layoutChangeLock = new();
        public void ChangeWallpaperLayoutFolrderPath(string previousDir, string newDir)
        {
            lock (_layoutChangeLock)
            {
                var wpLayouts = _userSettings.WallpaperLayout;
                for (int i = 0; i < wpLayouts.Count; ++i)
                {
                    var wpLayout = wpLayouts[i];
                    wpLayout.FolderPath = wpLayout.FolderPath.Replace(previousDir, newDir);
                }

                _userSettings.Save<List<IWallpaperLayout>>();
            }
        }

        private readonly object _monitorSettingsChangedLock = new();
        private void MonitorSettingsChanged_Hwnd(object? sender, EventArgs e)
        {
            lock (_monitorSettingsChangedLock)
            {
                _logger.Info("Monitor settings changed, monitor(s):");
                _monitorManager.Monitors.ToList().ForEach(x => _logger.Info(x.DeviceName + " " + x.Bounds));

                App.Services.GetRequiredService<IScreensaverService>().Stop();

                RefreshWallpaper();
                //RestoreDisconnectedWallpapers();
            }
        }

        private void RefreshWallpaper()
        {
            try
            {
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

                switch (_userSettings.Settings.WallpaperArrangement)
                {
                    case WallpaperArrangement.Per:
                        //No screens running metaData needs to be removed.
                        if (orphanWallpapers.Count != 0)
                        {
                            orphanWallpapers.ForEach(x =>
                            {
                                _logger.Info($"Disconnected Screen: {x.Monitor.DeviceName} {x.Monitor.Bounds}");
                                x.Close();
                            });

                            //var newOrphans = orphanWallpapers.FindAll(
                            //    oldOrphan => _wallpapersDisconnected.Find(
                            //        newOrphan => newOrphan.Monitor.Equals(oldOrphan.Monitor)) == null);
                            //foreach (var item in newOrphans)
                            //{
                            //    _wallpapersDisconnected.Add(new WallpaperLayout((Models.Cores.Monitor)item.Monitor, item.MetaData.FolderPath));
                            //}
                            _wallpapers.RemoveAll(x => orphanWallpapers.Contains(x));
                        }
                        break;
                    case WallpaperArrangement.Duplicate:
                        if (orphanWallpapers.Count != 0)
                        {
                            orphanWallpapers.ForEach(x =>
                            {
                                _logger.Info($"Disconnected Screen: {x.Monitor.DeviceName} {x.Monitor.Bounds}");
                                x.Close();
                            });
                            _wallpapers.RemoveAll(x => orphanWallpapers.Contains(x));
                        }
                        break;
                    case WallpaperArrangement.Span:
                        //Only update metaData rect.
                        break;
                }
                //Desktop size change when monitor is added/removed/property changed.
                UpdateWallpaperRect();
            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
            }
            finally
            {
                //Notifying display/metaData change.
                WallpaperChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateWallpaperRect()
        {
            if (_monitorManager.IsMultiScreen() && _userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Span)
            {
                if (_wallpapers.Count != 0)
                {
                    //Wallpapers[0].Play();
                    var screenArea = _monitorManager.VirtualScreenBounds;
                    _logger.Info($"Updating metaData rect(Span): ({screenArea.Width}, {screenArea.Height}).");
                    //For Play/Pause, setting the new metadata.
                    Wallpapers[0].Monitor = _monitorManager.PrimaryMonitor;
                    Native.SetWindowPos(Wallpapers[0].Handle, 1, 0, 0, screenArea.Width, screenArea.Height, 0x0010);
                }
            }
            else
            {
                int i;
                foreach (var screen in _monitorManager.Monitors.ToList())
                {
                    if ((i = _wallpapers.FindIndex(x => x.Monitor.Equals(screen))) != -1)
                    {
                        //Wallpapers[i].Play();
                        _logger.Info($"Updating metaData rect(Screen): {Wallpapers[i].Monitor.Bounds} -> {screen.Bounds}.");
                        //For Play/Pause, setting the new metadata.
                        Wallpapers[i].Monitor = screen;

                        var screenArea = _monitorManager.VirtualScreenBounds;
                        if (!Native.SetWindowPos(Wallpapers[i].Handle,
                                                        1,
                                                        (screen.Bounds.X - screenArea.Location.X),
                                                        (screen.Bounds.Y - screenArea.Location.Y),
                                                        (screen.Bounds.Width),
                                                        (screen.Bounds.Height),
                                                        0x0010))
                        {
                            //LogUtil.LogWin32Error("Failed to update metaData rect");
                        }
                    }
                }
            }
            DesktopUtil.RefreshDesktop();
        }

        private void RestoreWallpaper(List<IWallpaperLayout> wallpaperLayout)
        {
            foreach (var layout in wallpaperLayout)
            {
                IMetaData? metaData = null;
                try
                {
                    metaData = WallpaperUtil.ScanWallpaperFolder(layout.FolderPath);
                    if (metaData == null) throw new();
                }
                catch (Exception e)
                {
                    _logger.Info($"Skipping restoration of {layout.FolderPath} | {e.Message}");
                }

                var screen = _monitorManager.Monitors.FirstOrDefault(x => x.Equals(layout.Monitor));
                if (screen == null)
                {
                    _logger.Info($"Screen missing, skipping restoration of {layout.FolderPath} | {layout.Monitor.DeviceName}");
                }
                else
                {
                    _logger.Info($"Restoring metaData {metaData.Title} | {metaData.FolderPath}");
                    SetWallpaperAsync(metaData, screen);
                }
            }
        }

        private void SetupDesktop_WallpaperChanged(object? sender, EventArgs e)
        {
            SaveWallpaperLayout();
        }

        private readonly object _layoutWriteLock = new();
        private void SaveWallpaperLayout()
        {
            lock (_layoutWriteLock)
            {
                _userSettings.WallpaperLayout.Clear();
                _wallpapers.ForEach(wallpaper =>
                {
                    _userSettings.WallpaperLayout.Add(new WallpaperLayout(
                            (Models.Cores.Monitor)wallpaper.Monitor,
                            wallpaper.MetaData.FolderPath));
                });

                try
                {
                    _userSettings.Save<List<IWallpaperLayout>>();
                }
                catch (Exception e)
                {
                    _logger.Error(e.ToString());
                }
            }
        }

        private void UpdateWorkerW()
        {
            _logger.Info("WorkerW initializing..");
            var retries = 5;
            while (true)
            {
                _workerW = CreateWorkerW();
                if (_workerW != IntPtr.Zero)
                {
                    break;
                }
                else
                {
                    retries--;
                    if (retries == 0)
                        break;

                    _logger.Error($"Failed to create WorkerW, retrying ({retries})..");
                }
            }
            _logger.Info($"WorkerW initialized {_workerW}");
            WallpaperReset?.Invoke(this, EventArgs.Empty);
        }

        #region utils
        /// <summary>
        /// Calculates the position of window w.r.t parent _workerW handle & sets it as child window to it.
        /// </summary>
        /// <param name="handle">window handle of process to add as wallpaper</param>
        /// <param name="targetMonitor">monitorstring of monitor to sent wp to.</param>
        private bool TrySetWallpaperPerMonitor(IntPtr handle, IMonitor targetMonitor)
        {
            Native.RECT prct = new();
            _logger.Info($"Sending wallpaper(Monitor): {targetMonitor.DeviceName} | {targetMonitor.Bounds}");
            //Position the wp fullscreen to corresponding monitor.
            if (!Native.SetWindowPos(handle, 1, targetMonitor.Bounds.X, targetMonitor.Bounds.Y, (targetMonitor.Bounds.Width), (targetMonitor.Bounds.Height), 0x0010))
            {
                _logger.Error("Failed to set perscreen wallpaper(1)");
            }

            _ = Native.MapWindowPoints(handle, _workerW, ref prct, 2);
            var success = TrySetParentWorkerW(handle);

            //Position the wp window relative to the new parent window(_workerW).
            if (!Native.SetWindowPos(handle, 1, prct.Left, prct.Top, (targetMonitor.Bounds.Width), (targetMonitor.Bounds.Height), 0x0010))
            {
                _logger.Error("Failed to set perscreen wallpaper(2)");
            }
            DesktopUtil.RefreshDesktop();

            return success;
        }

        /// <summary>
        /// Spans wp across All screens.
        /// </summary>
        private bool TrySetWallpaperSpanMonitor(IntPtr handle)
        {
            //get spawned _workerW rectangle data.
            _ = Native.GetWindowRect(_workerW, out Native.RECT prct);
            var success = TrySetParentWorkerW(handle);

            //fill wp into the whole _workerW area.
            _logger.Info($"Sending wallpaper(Span): ({prct.Left}, {prct.Top}, {prct.Right - prct.Left}, {prct.Bottom - prct.Top}).");
            if (!Native.SetWindowPos(handle, 1, 0, 0, prct.Right - prct.Left, prct.Bottom - prct.Top, 0x0010))
            {
                //LogUtil.LogWin32Error("Failed to set Span wallpaper");
            }
            DesktopUtil.RefreshDesktop();
            return success;
        }

        private static IntPtr CreateWorkerW()
        {
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
            Native.EnumWindows(new Native.EnumWindowsProc((tophandle, topparamhandle) =>
            {
                IntPtr p = Native.FindWindowEx(tophandle,
                                            IntPtr.Zero,
                                            "SHELLDLL_DefView",
                                            IntPtr.Zero);

                if (p != IntPtr.Zero)
                {
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
            if (_workerW == IntPtr.Zero)
            {
                _workerW = Native.FindWindowEx(progman,
                                                IntPtr.Zero,
                                                "WorkerW",
                                                IntPtr.Zero);
            }
            return _workerW;
        }

        /// <summary>
        /// Adds the metaData as child of spawned desktop-_workerW window.
        /// </summary>
        /// <param name="windowHandle">handle of window</param>
        private bool TrySetParentWorkerW(IntPtr windowHandle)
        {
            //Win7
            if (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor == 1)
            {
                var progman = Native.FindWindow("Progman", null);
                if (!_workerW.Equals(progman)) //this should fix the win7 wallpaper disappearing issue.
                    Native.ShowWindow(_workerW, (uint)0);

                IntPtr ret = Native.SetParent(windowHandle, progman);
                if (ret.Equals(IntPtr.Zero))
                    return false;

                //_workerW is assumed as progman in win7, this is untested with All fn's: addwallpaper(), wp Pause, resize events.. 
                _workerW = progman;
            }
            else
            {
                IntPtr ret = Native.SetParent(windowHandle, _workerW);
                if (ret.Equals(IntPtr.Zero))
                    return false;
            }
            return true;
        }
        #endregion

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    WallpaperChanged -= SetupDesktop_WallpaperChanged;
                    if (_isInitialized)
                    {
                        try
                        {
                            CloseAllWallpapers();
                            DesktopUtil.RefreshDesktop();
                        }
                        catch (Exception e)
                        {
                            _logger.Error("Failed to shutdown core: " + e.ToString());
                        }
                    }
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly WindowEventHook _workerWHook;
        private readonly List<IWallpaper> _wallpapers = [];
        private IntPtr _progman, _workerW;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _semaphoreSlimWallpaperLoadingLock = new(1, 1);

        private readonly IUserSettingsService _userSettings;
        private readonly IWallpaperFactory _wallpaperFactory;
        private readonly IWatchdogService _watchdog;
        private readonly IMonitorManager _monitorManager;
    }
}
