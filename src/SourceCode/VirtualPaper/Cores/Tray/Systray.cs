using System.Diagnostics;
using System.Windows.Threading;
using VirtualPaper.Common;
using VirtualPaper.Cores.Desktop;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.PlaybackControl;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils.Theme;
using Cursor = System.Windows.Forms.Cursor;
using ToolTip = System.Windows.Controls.ToolTip;

namespace VirtualPaper.Cores.Tray
{
    public class Systray : ISystray
    {
        public Systray(
            IUIRunnerService uiRunner,
            IUserSettingsService userSettings,
            IWallpaperControl wpControl,
            //IAppUpdaterService appUpdater,
            IMonitorManager monitorManager,
            IPlayback playbackMonitor
            )
        {
            this._uiRunner = uiRunner;
            this._wpControl = wpControl;
            this._userSetting = userSettings;
            this._monitorManager = monitorManager;
            this._playbackMonitor = playbackMonitor;

            //NotifyIcon Fix: https://stackoverflow.com/questions/28833702/wpf-notifyicon-crash-on-first-run-the-root-visual-of-a-visualtarget-cannot-hav/29116917
            //Errors: "The root Visual of a VisualTarget cannot have a parent.."
            ToolTip tt = new()
            {
                IsOpen = true
            };
            tt.IsOpen = false;

            // Properties
            _notifyIcon.DoubleClick += (s, args) => uiRunner.ShowUI();
            _notifyIcon.Icon = Properties.Icons.virtualpaper;
            _notifyIcon.Text = "Virtual Paper";
            _notifyIcon.Visible = true;
            _notifyIcon.ContextMenuStrip = new()
            {
                ForeColor = Color.AliceBlue,
                Font = new Font("Segoe UI", 10F),
                Renderer = new ContextMenuTheme.RendererDark()
            };
            _notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;

            //ShowAsync UI
            _notifyIcon.ContextMenuStrip.Items.Add(App.GetResourceDicString("Systray_TextOpenApp"), Properties.Icons.icon_appwindow).Click += (s, e) => uiRunner.ShowUI();
            
            //Close wallpaper
            _notifyIcon.ContextMenuStrip.Items.Add(new ContextMenuTheme.StripSeparatorCustom().stripSeparator);
            _notifyIcon.ContextMenuStrip.Items.Add(App.GetResourceDicString("Systray_TextCloseAllWallpapers"), null).Click += (s, e) => wpControl.CloseAllWallpapers();
            
            //Wallpaper playbackMode
            _pauseBtn = new ToolStripMenuItem(App.GetResourceDicString("Systray_TextPauseWallpapers"), null);
            _pauseBtn.Click += (s, e) =>
            {
                playbackMonitor.WallpaperPlaybackMode = (playbackMonitor.WallpaperPlaybackMode == PlaybackMode.Play) ? PlaybackMode.Paused : PlaybackMode.Play;
            };
            _notifyIcon.ContextMenuStrip.Items.Add(_pauseBtn);
            
            //Random Wallpaper
            //_notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.TextChangeWallpaperRandom, null).Click += (s, e) => SetRandomWallpapers();
            
            //Customize wallpaper
            //_customiseWallpaperBtn = new ToolStripMenuItem(App.GetResourceDicString("Systray_TextCustomizeWallpaper"), null)
            //{
            //    //Systray is initialized first before restoring wallpaper
            //    Enabled = false,
            //};
            //_customiseWallpaperBtn.Click += CustomizeWallpaper;
            //_notifyIcon.ContextMenuStrip.Items.Add(_customiseWallpaperBtn);

            //Update check
            //if (!Constants.ApplicationType.IsMSIX)
            //{
            //    _notifyIcon.ContextMenuStrip.Items.Add(new ContextMenuTheme.StripSeparatorCustom().stripSeparator);
            //    _updateBtn = new ToolStripMenuItem(Properties.Resources.TextUpdateChecking, null)
            //    {
            //        Enabled = false
            //    };
            //    _updateBtn.Click += (s, e) => App.AppUpdateDialog(appUpdater.LastCheckUri, appUpdater.LastCheckChangelog);
            //    _notifyIcon.ContextMenuStrip.Items.Add(_updateBtn);
            //}

            //Report bug
            _notifyIcon.ContextMenuStrip.Items.Add(new ContextMenuTheme.StripSeparatorCustom().stripSeparator);
            _notifyIcon.ContextMenuStrip.Items.Add(App.GetResourceDicString("Systray_TextReportBug"), Properties.Icons.icon_bug).Click += (s, e) =>
            {
                Process.Start("https://github.com/PaperHammer/virtualpaper/issues");
            };
            
            //Exit app
            _notifyIcon.ContextMenuStrip.Items.Add(new ContextMenuTheme.StripSeparatorCustom().stripSeparator);
            _notifyIcon.ContextMenuStrip.Items.Add(App.GetResourceDicString("Systray_TextExit"), Properties.Icons.icon_close).Click += (s, e) => App.ShutDown();

            //Change events
            playbackMonitor.PlaybackModeChanged += Playback_PlaybackStateChanged;
            //wpControl.WallpaperChanged += DesktopCore_WallpaperChanged;
            //appUpdater.UpdateChecked += (s, e) => { SetUpdateMenu(e.UpdateStatus); };

            _notifyIcon.ShowBalloonTip(
                2000, 
                "Virtual Paper",
                App.GetResourceDicString("Virtual_Paper_isRunning"), 
                ToolTipIcon.Info);
        }

        /// <summary>
        /// Fix for traymenu opening to the nearest screen instead of the screen in which cursor is located.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenuStrip_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            ContextMenuStrip menuStrip = (sender as ContextMenuStrip)!;
            if (_monitorManager.IsMultiScreen())
            {
                //Finding screen in which cursor is present.
                var screen = _monitorManager.GetMonitorByPoint(Cursor.Position);

                var mousePos = Cursor.Position;
                //Converting global cursor pos. to given screen pos.
                mousePos.X += -1 * screen.Bounds.X;
                mousePos.Y += -1 * screen.Bounds.Y;

                //guessing taskbar pos. based on cursor pos. on display.
                bool isLeft = mousePos.X < screen.Bounds.Width * .5;
                bool isTop = mousePos.Y < screen.Bounds.Height * .5;

                //menu popup pos. rule.
                if (isLeft && isTop)
                {
                    //not possible?
                    menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.Default);
                }
                if (isLeft && !isTop)
                {
                    menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.AboveRight);
                }
                else if (!isLeft && isTop)
                {
                    menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.BelowLeft);
                }
                else if (!isLeft && !isTop)
                {
                    menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.AboveLeft);
                }
            }
            else
            {
                menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.AboveLeft);
            }
        }

        public void ShowBalloonNotification(int timeout, string title, string msg)
        {
            _notifyIcon.ShowBalloonTip(timeout, title, msg, ToolTipIcon.None);
        }

        private void Playback_PlaybackStateChanged(object? sender, PlaybackMode e)
        {
            _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                _pauseBtn.Checked = e == PlaybackMode.Paused;
                //_notifyIcon.Icon = (e == PlaybackState.Paused) ? Properties.Icons.appicon_gray : Properties.Icons.appicon;
            }));
        }

        //private void DesktopCore_WallpaperChanged(object? sender, EventArgs e)
        //{
        //    _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(delegate
        //    {
        //        _customiseWallpaperBtn.Enabled = _wpControl.Wallpapers.Any(x => x.MetaData.FilePath != null);
        //    }));
        //}

        //private void CustomizeWallpaper(object? sender, EventArgs e)
        //{
        //    var items = wpControl.Wallpapers.Where(x => x.MetaData.FilePath != null);
        //    if (items.Any())
        //    {
        //        _uiRunner.ShowCustomisWallpaperePanel();
        //    }
        //}

        //private void SetUpdateMenu(AppUpdateStatus status)
        //{
        //    switch (status)
        //    {
        //        case AppUpdateStatus.uptodate:
        //            _updateBtn.Enabled = false;
        //            _updateBtn.Text = Properties.Resources.TextUpdateUptodate;
        //            break;
        //        case AppUpdateStatus.available:
        //            _updateBtn.Enabled = true;
        //            _updateBtn.Text = Properties.Resources.TextUpdateAvailable;
        //            break;
        //        case AppUpdateStatus.invalid:
        //            _updateBtn.Enabled = false;
        //            _updateBtn.Text = "Fancy~";
        //            break;
        //        case AppUpdateStatus.notchecked:
        //            _updateBtn.Enabled = false;
        //            break;
        //        case AppUpdateStatus.Error:
        //            _updateBtn.Enabled = true;
        //            _updateBtn.Text = Properties.Resources.TextupdateCheckFail;
        //            break;
        //    }
        //}

        /// <summary>
        /// Sets random library item as wallpaper.
        /// </summary>
        //private void SetRandomWallpapers()
        //{
        //    switch (_userSettings.Settings.WallpaperArrangement)
        //    {
        //        case WallpaperArrangement.Per:
        //            {
        //                var screenCount = _monitorManager.Monitors.Count;
        //                var wallpapersRandom = GetRandomWallpaper().Take(screenCount);
        //                var wallpapersCount = wallpapersRandom.Count();
        //                if (wallpapersCount > 0)
        //                {
        //                    for (int i = 0; i < screenCount; i++)
        //                    {
        //                        _wpControl.SetWallpaperAsync(wallpapersRandom.ElementAt(i > wallpapersCount - 1 ? 0 : i), _monitorManager.Monitors[i]);
        //                    }
        //                }
        //            }
        //            break;
        //        case WallpaperArrangement.Span:
        //        case WallpaperArrangement.Duplicate:
        //            {
        //                try
        //                {
        //                    _wpControl.SetWallpaperAsync(GetRandomWallpaper().First(), _monitorManager.PrimaryMonitor);
        //                }
        //                catch (InvalidOperationException)
        //                {
        //                    //No _wallpapers present.
        //                }
        //            }
        //            break;
        //    }
        //}

        #region helpers
        //private IEnumerable<ILibraryModel> GetRandomWallpaper()
        //{
        //    var dir = new List<string>();
        //    string[] folderPaths = {
        //        Path.Combine(_userSettings.Settings.WallpaperDir, Constants.CommonPartialPaths.WallpaperInstallDir),
        //        Path.Combine(_userSettings.Settings.WallpaperDir, Constants.CommonPartialPaths.WallpaperInstallTempDir)
        //    };
        //    for (int i = 0; i < folderPaths.Count(); i++)
        //    {
        //        try
        //        {
        //            dir.AddRange(Directory.GetDirectories(folderPaths[i], "*", SearchOption.TopDirectoryOnly));
        //        }
        //        catch { /* TODO */ }
        //    }

        //    //Fisher-Yates shuffle
        //    int n = dir.Count;
        //    while (n > 1)
        //    {
        //        n--;
        //        int k = _random.Next(n + 1);
        //        var value = dir[k];
        //        dir[k] = dir[n];
        //        dir[n] = value;
        //    }

        //    for (int i = 0; i < dir.Count; i++)
        //    {
        //        ILibraryModel libItem = null;
        //        try
        //        {
        //            libItem = WallpaperUtil.ScanWallpaperFolder(dir[i]);
        //        }
        //        catch { }

        //        if (libItem != null)
        //        {
        //            yield return libItem;
        //        }
        //    }
        //}
        #endregion

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon?.Icon?.Dispose();
                    _notifyIcon?.Dispose();
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

        //private readonly Random _random = new();
        private readonly NotifyIcon _notifyIcon = new();
        private readonly ToolStripMenuItem _pauseBtn;
        //private readonly ToolStripMenuItem _customiseWallpaperBtn;
        //private readonly ToolStripMenuItem _updateBtn;
        private readonly IUIRunnerService _uiRunner;
        private readonly IWallpaperControl _wpControl;
        private readonly IMonitorManager _monitorManager;
        private readonly IUserSettingsService _userSetting;
        private readonly IPlayback _playbackMonitor;
        //private DiagnosticMenu diagnosticMenu;
    }
}
