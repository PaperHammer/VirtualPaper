using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UI.Utils;

namespace VirtualPaper.UI.ViewModels {
    public partial class WpSettingsViewModel : ObservableObject {
        public ObservableList<IMonitor> MonitorThus { get; set; } = [];
        public List<WpArrangeDataModel> WpArrangements { get; set; } = [];
        public string WpSettings_NavItem1 { get; set; } = string.Empty;
        public string WpSettings_NavItem2 { get; set; } = string.Empty;
        public string Text_Close { get; set; } = string.Empty;
        public string Text_Detect { get; set; } = string.Empty;
        public string Text_Identify { get; set; } = string.Empty;
        public string Text_Adjust { get; set; } = string.Empty;
        public string Text_WpArrange { get; set; } = string.Empty;
        public string WpArrange_Per { get; set; } = string.Empty;
        public string WpArrange_PerExplain { get; set; } = string.Empty;
        public string WpArrange_Expand { get; set; } = string.Empty;
        public string WpArrange_ExpandExplain { get; set; } = string.Empty;
        public string WpArrange_Duplicate { get; set; } = string.Empty;
        public string WpArrange_DuplicateExplain { get; set; } = string.Empty;

        private int _selectedWpArrangementsIndex = -1;
        public int SelectedWpArrangementsIndex {
            get => _selectedWpArrangementsIndex;
            set {
                if (_selectedWpArrangementsIndex != value) {
                    _selectedWpArrangementsIndex = value;
                    OnPropertyChanged();
                    UpdateWpArrange(value);
                    InitMonitors();
                }
            }
        }

        private IMonitor _selectedMonitor;
        public IMonitor SelectedMonitor {
            get => _selectedMonitor;
            set { _selectedMonitor = value; OnPropertyChanged(); }
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

            InitText();
            InitMonitors();
        }

        #region Init
        private void InitText() {
            Text_Close = App.GetI18n(Constants.I18n.Text_Close);
            Text_Detect = App.GetI18n(Constants.I18n.Text_Detect);
            Text_Identify = App.GetI18n(Constants.I18n.Text_Identify);
            Text_Adjust = App.GetI18n(Constants.I18n.Text_Adjust);

            Text_WpArrange = App.GetI18n(Constants.I18n.Text_WpArrange);
            WpArrange_Per = App.GetI18n(Constants.I18n.WpArrange_Per);
            WpArrange_PerExplain = App.GetI18n(Constants.I18n.WpArrange_PerExplain);
            WpArrange_Expand = App.GetI18n(Constants.I18n.WpArrange_Expand);
            WpArrange_ExpandExplain = App.GetI18n(Constants.I18n.WpArrange_ExpandExplain);
            WpArrange_Duplicate = App.GetI18n(Constants.I18n.WpArrange_Duplicate);
            WpArrange_DuplicateExplain = App.GetI18n(Constants.I18n.WpArrange_DuplicateExplain);

            WpSettings_NavItem1 = App.GetI18n(Constants.I18n.WpSettings_NavTitle_LibraryContents);
            WpSettings_NavItem2 = App.GetI18n(Constants.I18n.WpSettings_NavTitle_ScrSettings);
        }

        internal void InitMonitors() {
            _monitors.Clear();
            switch (_userSettingsClient.Settings.WallpaperArrangement) {
                case WallpaperArrangement.Per: {
                        foreach (var monitor in _monitorManagerClient.Monitors) {
                            _monitors.Add(monitor);
                        }
                    }
                    break;
                case WallpaperArrangement.Expand: {
                        IMonitor monitor = _monitorManagerClient.PrimaryMonitor;
                        monitor.Content = "Expand";
                        monitor.ThumbnailPath = _monitorManagerClient.Monitors[0].ThumbnailPath;
                        _monitors.Add(monitor);
                    }
                    break;
                case WallpaperArrangement.Duplicate: {
                        IMonitor monitor = _monitorManagerClient.PrimaryMonitor;
                        monitor.Content = "Duplicate";
                        monitor.ThumbnailPath = _monitorManagerClient.Monitors[0].ThumbnailPath;
                        _monitors.Add(monitor);
                    }
                    break;
            }

            _monitorCnt = _monitorManagerClient.Monitors.Count;
            MonitorThus.SetRange(_monitors);
            if (MonitorThus.Count > 0) {
                SelectedMonitor = MonitorThus[0];
            }
        }

        internal void InitWpArrangments() {
            WpArrangements.Add(new() {
                Method = WpArrange_Per,
                Tooltip = WpArrange_PerExplain,
            });
            WpArrangements.Add(new() {
                Method = WpArrange_Duplicate,
                Tooltip = WpArrange_DuplicateExplain,
            });
            WpArrangements.Add(new() {
                Method = WpArrange_Expand,
                Tooltip = WpArrange_ExpandExplain,
            });
            SelectedWpArrangementsIndex = (int)_userSettingsClient.Settings.WallpaperArrangement;
        }
        #endregion

        internal async void UpdateWpArrange(int tag) {
            try {
                BasicUIComponentUtil.Loading(false, false, []);

                var type = (WallpaperArrangement)tag;
                if (type == _userSettingsClient.Settings.WallpaperArrangement) return;

                _userSettingsClient.Settings.WallpaperArrangement = type;
                await _userSettingsClient.SaveAsync<ISettings>();

                var response = await _wpControlClient.RestartAllWallpapersAsync();
                if (response.IsFinished != true) {
                    await _dialogService.ShowDialogAsync(
                        App.GetI18n("Dialog_Content_ApplyError")
                        , App.GetI18n("Dialog_Title_Error")
                        , App.GetI18n("Text_Confirm"));
                }
            }
            catch (Exception ex) {
                App.Log.Error(ex);
            }
            finally {
                BasicUIComponentUtil.Loaded([]);
            }
        }

        #region Buttons Command
        internal async void Close() {
            await _wpControlClient.CloseWallpaperAsync(SelectedMonitor);
            SelectedMonitor.ThumbnailPath = string.Empty;
        }

        internal async Task DetectAsync() {
            InitMonitors();

            await _dialogService.ShowDialogAsync(
                App.GetI18n(Constants.I18n.Dialog_Content_GetMonitorsAsync) + _monitorCnt
                , App.GetI18n(Constants.I18n.Dialog_Title_Prompt)
                , App.GetI18n(Constants.I18n.Text_Confirm));
        }

        internal async Task IdentifyAsync() {
            await _monitorManagerClient.IdentifyMonitorsAsync();
        }

        internal async Task AdjustAsync() {
            try {
                await _adjustSemaphoreSlim.WaitAsync();

                _ctsAdjust = new CancellationTokenSource();
                BasicUIComponentUtil.Loading(true, false, [_ctsAdjust]);

                if (SelectedMonitor.ThumbnailPath == string.Empty) {
                    return;
                }

                bool isOk = await _wpControlClient.AdjustWallpaperAsync(SelectedMonitor.DeviceId, _ctsAdjust.Token);
                if (!isOk) {
                    throw new Exception("Failed to evoke custom adjustment window.");
                }
            }
            catch (OperationCanceledException) {
                BasicUIComponentUtil.ShowCanceled();
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
            finally {
                BasicUIComponentUtil.Loaded([_ctsAdjust]);
                _adjustSemaphoreSlim.Release();
            }
        }
        #endregion

        private readonly IList<IMonitor> _monitors = [];
        private readonly IDialogService _dialogService;
        private readonly IMonitorManagerClient _monitorManagerClient;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly SemaphoreSlim _adjustSemaphoreSlim = new(1, 1);
        private CancellationTokenSource _ctsAdjust;
        private int _monitorCnt;

        public class WpArrangeDataModel {
            public string Method { get; set; }
            public string Tooltip { get; set; }
        }
    }
}
