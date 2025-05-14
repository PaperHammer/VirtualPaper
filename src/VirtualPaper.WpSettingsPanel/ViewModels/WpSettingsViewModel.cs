using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.WpSettingsPanel.ViewModels {
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
                if (_selectedWpArrangementsIndex == value) return;

                _selectedWpArrangementsIndex = value;
                UpdateWpArrange(value);
                OnPropertyChanged();
            }
        }

        private IMonitor _selectedMonitor;
        public IMonitor SelectedMonitor {
            get => _selectedMonitor;
            set { _selectedMonitor = value; OnPropertyChanged(); }
        }

        public WpSettingsViewModel(
            IMonitorManagerClient monitorManagerClient,
            IWallpaperControlClient wallpaperControlClient,
            IUserSettingsClient userSettingsClient) {
            _monitorManagerClient = monitorManagerClient;
            _wpControlClient = wallpaperControlClient;
            _userSettingsClient = userSettingsClient;

            InitText();
            InitMonitors();
        }

        #region Init
        private void InitText() {
            Text_Close = LanguageUtil.GetI18n(Constants.I18n.Text_Close);
            Text_Detect = LanguageUtil.GetI18n(Constants.I18n.Text_Detect);
            Text_Identify = LanguageUtil.GetI18n(Constants.I18n.Text_Identify);
            Text_Adjust = LanguageUtil.GetI18n(Constants.I18n.Text_Adjust);

            Text_WpArrange = LanguageUtil.GetI18n(Constants.I18n.Text_WpArrange);
            WpArrange_Per = LanguageUtil.GetI18n(Constants.I18n.WpArrange_Per);
            WpArrange_PerExplain = LanguageUtil.GetI18n(Constants.I18n.WpArrange_PerExplain);
            WpArrange_Expand = LanguageUtil.GetI18n(Constants.I18n.WpArrange_Expand);
            WpArrange_ExpandExplain = LanguageUtil.GetI18n(Constants.I18n.WpArrange_ExpandExplain);
            WpArrange_Duplicate = LanguageUtil.GetI18n(Constants.I18n.WpArrange_Duplicate);
            WpArrange_DuplicateExplain = LanguageUtil.GetI18n(Constants.I18n.WpArrange_DuplicateExplain);

            WpSettings_NavItem1 = LanguageUtil.GetI18n(Constants.I18n.WpSettings_NavTitle_LibraryContents);
            WpSettings_NavItem2 = LanguageUtil.GetI18n(Constants.I18n.WpSettings_NavTitle_ScrSettings);
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
                case WallpaperArrangement.Duplicate:
                case WallpaperArrangement.Expand: {
                        _monitors.Add(new Models.Cores.Monitor() {
                            Content = _userSettingsClient.Settings.WallpaperArrangement.ToString(),
                            ThumbnailPath = _monitorManagerClient.PrimaryMonitor.ThumbnailPath,
                        });
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
                _wpSettingsPanel.GetNotify().Loading(false, false, null);

                var type = (WallpaperArrangement)tag;
                if (type == _userSettingsClient.Settings.WallpaperArrangement) return;
                var oldType = _userSettingsClient.Settings.WallpaperArrangement;
                _userSettingsClient.Settings.WallpaperArrangement = type;
                await _userSettingsClient.SaveAsync<ISettings>();

                var response = await _wpControlClient.RestartAllWallpapersAsync();
                if (response.IsFinished != true) {                    
                    _wpSettingsPanel.GetNotify().ShowMsg(
                        true,
                        LanguageUtil.GetI18n(Constants.I18n.Dialog_Content_ApplyError),
                        InfoBarType.Error);
                    // 恢复
                    SelectedWpArrangementsIndex = (int)oldType;
                    _userSettingsClient.Settings.WallpaperArrangement = oldType;
                    await _userSettingsClient.SaveAsync<ISettings>();
                    return;
                }
            }
            catch (Exception ex) {
                _wpSettingsPanel.Log(LogType.Error, ex);
            }
            finally {
                InitMonitors();
                _wpSettingsPanel.GetNotify().Loaded(null);
            }
        }

        #region Buttons Command
        internal async void Close() {
            await _wpControlClient.CloseWallpaperAsync(SelectedMonitor);
            SelectedMonitor.ThumbnailPath = string.Empty;
        }

        internal void Detect() {
            InitMonitors();

            _wpSettingsPanel.GetNotify().ShowMsg(
                true,
                Constants.I18n.Dialog_Content_GetMonitorsAsync,
                InfoBarType.Informational,
                _monitorCnt.ToString());
        }

        internal async Task IdentifyAsync() {
            await _monitorManagerClient.IdentifyMonitorsAsync();
        }

        internal async Task AdjustAsync() {
            try {
                await _adjustSemaphoreSlim.WaitAsync();

                _ctsAdjust = new CancellationTokenSource();
                _wpSettingsPanel.GetNotify().Loading(true, false, [_ctsAdjust]);

                if (SelectedMonitor.ThumbnailPath == string.Empty) {
                    return;
                }

                bool isOk = await _wpControlClient.AdjustWallpaperAsync(SelectedMonitor.DeviceId, _ctsAdjust.Token);
                if (!isOk) {
                    throw new Exception("Failed to evoke custom adjustment window.");
                }
            }
            catch (RpcException ex) {
                if (ex.StatusCode == StatusCode.Cancelled) {
                    _wpSettingsPanel.GetNotify().ShowCanceled();
                }
                else {
                    _wpSettingsPanel.GetNotify().ShowExp(ex);
                }
            }
            catch (OperationCanceledException) {
                _wpSettingsPanel.GetNotify().ShowCanceled();
            }
            catch (Exception ex) {
                _wpSettingsPanel.GetNotify().ShowExp(ex);
            }
            finally {
                _wpSettingsPanel.GetNotify().Loaded([_ctsAdjust]);
                _adjustSemaphoreSlim.Release();
            }
        }
        #endregion

        private readonly IList<IMonitor> _monitors = [];
        internal IWpSettingsPanel _wpSettingsPanel;
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
