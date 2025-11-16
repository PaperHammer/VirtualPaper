using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Logging;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.WpSettingsPanel.ViewModels {
    public partial class WpSettingsViewModel : ObservableObject {
        public ObservableList<IMonitor> MonitorThus { get; set; } = [];
        public List<WpArrangeDataModel> WpArrangements { get; set; } = [];

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

        private IMonitor _selectedMonitor = null!;
        public IMonitor SelectedMonitor {
            get => _selectedMonitor;
            set { if (_selectedMonitor == value) return; _selectedMonitor = value; OnPropertyChanged(); }
        }

        public WpSettingsViewModel(
            IMonitorManagerClient monitorManagerClient,
            IWallpaperControlClient wallpaperControlClient,
            IUserSettingsClient userSettingsClient) {
            _monitorManagerClient = monitorManagerClient;
            _wpControlClient = wallpaperControlClient;
            _userSettingsClient = userSettingsClient;

            InitMonitors();
        }

        #region Init
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

            MonitorThus.SetRange(_monitors);
            if (MonitorThus.Count > 0) {
                SelectedMonitor = MonitorThus[0];
            }
        }

        internal void InitWpArrangments() {
            WpArrangements.Add(new WpArrangeDataModel(
                Method: LanguageUtil.GetI18n(Constants.I18n.WpArrange_Per),
                Tooltip: LanguageUtil.GetI18n(Constants.I18n.WpArrange_PerExplain)));
            WpArrangements.Add(new WpArrangeDataModel(
                Method: LanguageUtil.GetI18n(Constants.I18n.WpArrange_Duplicate),
                Tooltip: LanguageUtil.GetI18n(Constants.I18n.WpArrange_DuplicateExplain)));
            WpArrangements.Add(new WpArrangeDataModel(
                Method: LanguageUtil.GetI18n(Constants.I18n.WpArrange_Expand),
                Tooltip: LanguageUtil.GetI18n(Constants.I18n.WpArrange_ExpandExplain)));

            SelectedWpArrangementsIndex = (int)_userSettingsClient.Settings.WallpaperArrangement;
        }
        #endregion

        internal async void UpdateWpArrange(int tag) {
            try {
                PageContextManager.GetContext<WpSettings>()?.Loading?.ShowLoading(false);

                var type = (WallpaperArrangement)tag;
                if (type == _userSettingsClient.Settings.WallpaperArrangement) return;
                var oldType = _userSettingsClient.Settings.WallpaperArrangement;
                _userSettingsClient.Settings.WallpaperArrangement = type;
                await _userSettingsClient.SaveAsync<ISettings>();

                var response = await _wpControlClient.RestartAllWallpapersAsync();
                if (response.IsFinished != true) {
                    GlobalMessageUtil.ShowError(
                        message: nameof(Constants.I18n.Dialog_Content_ApplyError),
                        isNeedLocalizer: true);
                    // 恢复
                    SelectedWpArrangementsIndex = (int)oldType;
                    _userSettingsClient.Settings.WallpaperArrangement = oldType;
                    await _userSettingsClient.SaveAsync<ISettings>();
                    return;
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WpSettingsViewModel>().Error(ex);
            }
            finally {
                InitMonitors();
                PageContextManager.GetContext<WpSettings>()?.Loading?.HideLoading();
            }
        }

        #region Buttons Command
        internal async void Close() {
            await _wpControlClient.CloseWallpaperAsync(SelectedMonitor);
            SelectedMonitor.ThumbnailPath = string.Empty;
        }

        internal void Detect() {
            InitMonitors();
            GlobalMessageUtil.ShowInfo(
                message: Constants.I18n.Dialog_Content_GetMonitorsAsync, 
                isNeedLocalizer: true, 
                extraMsg: $" {MonitorThus.Count}");
        }

        internal async Task IdentifyAsync() {
            await _monitorManagerClient.IdentifyMonitorsAsync();
        }

        internal async Task AdjustAsync() {
            try {
                await _adjustSemaphoreSlim.WaitAsync();

                var _ctsAdjust = new CancellationTokenSource();
                PageContextManager.GetContext<WpSettings>()?.Loading?.SetCancellationToken([_ctsAdjust]);
                PageContextManager.GetContext<WpSettings>()?.Loading?.ShowLoading();

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
                    GlobalMessageUtil.ShowCanceled();
                }
                else {
                    GlobalMessageUtil.ShowException(ex);
                }
            }
            catch (OperationCanceledException) {
                GlobalMessageUtil.ShowCanceled();
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowException(ex);
            }
            finally {
                PageContextManager.GetContext<WpSettings>()?.Loading?.HideLoading();
                _adjustSemaphoreSlim.Release();
            }
        }
        #endregion

        private readonly IList<IMonitor> _monitors = [];
        private readonly IMonitorManagerClient _monitorManagerClient;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly SemaphoreSlim _adjustSemaphoreSlim = new(1, 1);        
    }
    
    public record WpArrangeDataModel(string Method, string Tooltip);
}
