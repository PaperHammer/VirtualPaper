using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using VirtualPaper.Common;
using VirtualPaper.Cores.TrayControl;
using VirtualPaper.Cores.PlaybackControl;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.lang;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;
using VirtualPaper.Common.Utils.PInvoke;
using System.Windows.Interop;

namespace VirtualPaper {
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow(
            IUIRunnerService uiRunner,
            IUserSettingsService userSettingsService,
            IWallpaperControl wpControl,
            IPlayback playbackMonitor) {
            InitializeComponent();

            _uiRunnerService = uiRunner;
            _wpControl = wpControl;
            _playbackMonitor = playbackMonitor;
            _userSettingsService = userSettingsService;

            _playbackMonitor.PlaybackModeChanged += Playback_PlaybackStateChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            WindowInteropHelper wndHelper = new(this);
            Constants.Runtime.MainWindowHwnd = wndHelper.Handle;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            notifyIcon.Dispose();
            ToastNotificationManagerCompat.Uninstall();
        }

        private void Window_SourceInitialized(object sender, EventArgs e) {
            new ToastContentBuilder()
                .AddText(LanguageManager.Instance["Virtual_Paper_isRunning"])
                .AddText(LanguageManager.Instance["Virtual_Paper_isRunning_Greetings"])
                .Show();
        }

        private void NotifyIcon_LeftDoubleClick(Wpf.Ui.Tray.Controls.NotifyIcon sender, RoutedEventArgs e) {
            _uiRunnerService.ShowUI();
            e.Handled = true;
        }

        private void OpenAppMenuItem_Click(object sender, RoutedEventArgs e) {
            _uiRunnerService.ShowUI();
        }

        private void CloseAllWpMenuItem_Click(object sender, RoutedEventArgs e) {
            _wpControl.CloseAllWallpapers();
        }

        private void PauseAllWpMenuItem_Click(object sender, RoutedEventArgs e) {
            if (_playbackMonitor.WallpaperPlaybackMode == PlaybackMode.Play) {
                _playbackMonitor.WallpaperPlaybackMode = PlaybackMode.Paused;
                pauseMenuItem.Icon = new SymbolIcon() {
                    Height = 25,
                    Width = 25,
                    Symbol = SymbolRegular.Checkmark20,
                };
            }
            else {
                _playbackMonitor.WallpaperPlaybackMode = PlaybackMode.Play;
                pauseMenuItem.IsChecked = false;
                pauseMenuItem.Icon = new SymbolIcon() {
                    Height = 25,
                    Width = 25,
                };
            }
        }

        private void ReportBugMenuItem_Click(object sender, RoutedEventArgs e) {
            ProcessStartInfo startInfo = new() {
                FileName = "cmd",
                Arguments = $"/c start https://github.com/PaperHammer/virtualpaper/issues",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(startInfo);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e) {
            App.ShutDown();
        }

        private void Playback_PlaybackStateChanged(object? sender, PlaybackMode e) {
            _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(delegate {
                pauseMenuItem.IsChecked = e == PlaybackMode.Paused;
            }));
        }

        private void IsOnSrcsaver(object sender, RoutedEventArgs e) {
            ChangeScrStatu((MenuItem)sender);
        }

        private void IsOnRunningLock(object sender, RoutedEventArgs e) {
            ChangeRunLockStatu((MenuItem)sender);
        }

        private void ChangeRunLockStatu(MenuItem mi) {
            string tag = mi.Tag.ToString()!;
            SetRunLock(!(tag == "On"));

            UpdateSettings();
        }

        private void ChangeScrStatu(MenuItem mi) {
            string tag = mi.Tag.ToString()!;
            SetScr(!(tag == "On"));

            UpdateSettings();
        }

        private void DeNone_Click(object sender, RoutedEventArgs e) {
            ResetDe();
            deNone.Icon = new SymbolIcon() { Height = 25, Width = 25, Symbol = SymbolRegular.CircleSmall20 };
            deNone.Tag = "On";

            UpdateSettings();
        }

        private void DeBubble_Click(object sender, RoutedEventArgs e) {
            ResetDe();
            deBubble.Icon = new SymbolIcon() { Height = 25, Width = 25, Symbol = SymbolRegular.CircleSmall20 };
            deBubble.Tag = "On";

            UpdateSettings();
        }

        private void SrcsaverSubOpen(object sender, RoutedEventArgs e) {
            if (sender == e.Source) {
                InitScrData();
                srcsaverMenuItem.IsSubmenuOpen = true;
                Keyboard.Focus(srcsaver);
            }
        }

        private void InitScrData() {
            bool isOn = _userSettingsService.Settings.IsScreenSaverOn;
            SetScr(isOn);

            isOn = _userSettingsService.Settings.IsRunningLock;
            SetRunLock(isOn);

            ResetDe();
            var de = _userSettingsService.Settings.ScreenSaverEffect;
            switch (de) {
                case ScrEffect.None:
                    deNone.Icon = new SymbolIcon() { Height = 25, Width = 25, Symbol = SymbolRegular.CircleSmall20 };
                    deNone.Tag = "On";
                    break;
                case ScrEffect.Bubble:
                    deBubble.Icon = new SymbolIcon() { Height = 25, Width = 25, Symbol = SymbolRegular.CircleSmall20 };
                    deBubble.Tag = "On";
                    break;
            }
        }

        private void SetRunLock(bool isOn) {
            lockScr.Icon = new SymbolIcon() {
                Height = 25,
                Width = 25,
                Symbol = isOn ? SymbolRegular.Checkmark20 : SymbolRegular.Empty,
            };
            lockScr.Tag = isOn ? "On" : "Off";
        }

        private void SetScr(bool isOn) {
            srcsaver.Icon = new SymbolIcon() {
                Height = 25,
                Width = 25,
                Symbol = isOn ? SymbolRegular.Checkmark20 : SymbolRegular.Empty,
            };
            srcsaver.Tag = isOn ? "On" : "Off";
            deNone.IsEnabled = isOn;
            deBubble.IsEnabled = isOn;
        }

        private void ResetDe() {
            deNone.Icon = new SymbolIcon() { Height = 25, Width = 25, Symbol = SymbolRegular.Empty };
            deNone.Tag = "Off";
            deBubble.Icon = new SymbolIcon() { Height = 25, Width = 25, Symbol = SymbolRegular.Empty };
            deBubble.Tag = "Off";
        }

        private void UpdateSettings() {
            _userSettingsService.Settings.IsScreenSaverOn = srcsaver.Tag.ToString() == "On";
            _userSettingsService.Settings.IsRunningLock = lockScr.Tag.ToString() == "On";
            _userSettingsService.Settings.ScreenSaverEffect
                = deNone.Tag.ToString() == "On" ? ScrEffect.None : ScrEffect.Bubble;
            _userSettingsService.Save<ISettings>();

            var pipeClient = App.Services.GetRequiredService<TrayCommand>();
            pipeClient.SendMsgToUI("UPDATE_SCRSETTINGS");
        }

        private readonly IUIRunnerService _uiRunnerService;
        private readonly IWallpaperControl _wpControl;
        private readonly IPlayback _playbackMonitor;
        private readonly IUserSettingsService _userSettingsService;
    }
}
