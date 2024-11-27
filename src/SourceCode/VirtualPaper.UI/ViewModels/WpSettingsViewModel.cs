using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UIComponent.Utils;
using WinUI3Localizer;
using Monitor = VirtualPaper.Models.Cores.Monitor;

namespace VirtualPaper.UI.ViewModels {
    public partial class WpSettingsViewModel : ObservableObject {
        public ObservableList<IMonitor> Monitors { get; set; } = [];
        public List<NavigationViewItem> NavMenuItems { get; set; } = [];

        public string SelBarItem1 { get; set; } = string.Empty;
        public string SelBarItem2 { get; set; } = string.Empty;

        public string Text_Title { get; set; } = string.Empty;
        public string Text_Close { get; set; } = string.Empty;
        public string Text_Restore { get; set; } = string.Empty;
        public string Text_Detect { get; set; } = string.Empty;
        public string Text_Identify { get; set; } = string.Empty;
        public string Text_Preview { get; set; } = string.Empty;
        public string Text_Apply { get; set; } = string.Empty;
        public string SidebarWpConfig { get; set; } = string.Empty;
        public string SidebarLibraryContents { get; set; } = string.Empty;

        private int _monitorSelectedIdx;
        public int MonitorSelectedIdx {
            get => _monitorSelectedIdx;
            set { _monitorSelectedIdx = value; OnPropertyChanged(); }
        }

        public WpSettingsViewModel(
            IDialogService dialogService,
            IMonitorManagerClient monitorManagerClient,
            IWallpaperControlClient wallpaperControlClient,
            IUserSettingsClient userSettingsClient) {
            _dialogService = dialogService;
            _monitorManagerClient = monitorManagerClient;
            _wpControlClient = wallpaperControlClient;
            _userSettingsClient = userSettingsClient;
            _localizer = LanguageUtil.LocalizerInstacne;

            InitText();
        }

        #region Init
        private void InitText() {          
            Text_Title = _localizer.GetLocalizedString(Constants.LocalText.WpSettings_Text_Title);
            Text_Close = _localizer.GetLocalizedString(Constants.LocalText.Text_Close);
            Text_Detect = _localizer.GetLocalizedString(Constants.LocalText.Text_Detect);
            Text_Identify = _localizer.GetLocalizedString(Constants.LocalText.Text_Identify);
            Text_Preview = _localizer.GetLocalizedString(Constants.LocalText.Text_Preview);

            SelBarItem1 = _localizer.GetLocalizedString(Constants.LocalText.WpSettings_SidebarLibraryContents);
            SelBarItem2 = _localizer.GetLocalizedString(Constants.LocalText.WpSettings_SidebarSettings);
        }

        internal void UpdateMonitorLayout() {
            _monitors.Clear();
            switch (_userSettingsClient.Settings.WallpaperArrangement) {
                case WallpaperArrangement.Per: {
                        foreach (var monitor in _monitorManagerClient.Monitors) {
                            _monitors.Add(monitor);
                        }
                    }
                    break;
                case WallpaperArrangement.Expand: {
                        _monitors.Add(new Monitor(null, "Expand"));
                    }
                    break;
                case WallpaperArrangement.Duplicate: {
                        _monitors.Add(new Monitor(null, "Duplicate"));
                    }
                    break;
            }

            Monitors.SetRange(_monitors);
            if (_monitorSelectedIdx >= _monitors.Count) {
                _monitorSelectedIdx = 0;
            }
        }
        #endregion

        #region Control Buttons
        internal async void Close() {
            await _wpControlClient.CloseWallpaperAsync(Monitors[MonitorSelectedIdx]);
        }

        internal async Task DetectAsync() {
            UpdateMonitorLayout();

            await _dialogService.ShowDialogAsync(
                _localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_GetMonitorsAsync) + Monitors.Count
                , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Prompt)
                , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
        }

        internal async Task IdentifyAsync() {
            await _monitorManagerClient.IdentifyMonitorsAsync();
        }

        internal async Task PreviewAsync() {
            if (!Monitors[MonitorSelectedIdx].HasWallpaper) return;

            IWpMetadata data =  _wpControlClient.GetWpMetadataByMonitorThu(Monitors[MonitorSelectedIdx].ThumbnailPath);
            await _wpControlClient.PreviewWallpaperAsync(data);
        }

        internal string GetSelectedMonitorContent() {
            return GetSelectedMonitor().Content;
        }

        internal IMonitor GetSelectedMonitor() {
            return Monitors[MonitorSelectedIdx];
        }
        #endregion

        private readonly ILocalizer _localizer;
        private readonly IList<IMonitor> _monitors = [];
        private readonly IDialogService _dialogService;
        private readonly IMonitorManagerClient _monitorManagerClient;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
    }
}
