using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UI.Utils;
using Monitor = VirtualPaper.Models.Cores.Monitor;

namespace VirtualPaper.UI.ViewModels {
    public partial class WpSettingsViewModel : ObservableObject {
        public ObservableList<IMonitor> Monitors { get; set; } = [];
        public List<NavigationViewItem> NavMenuItems { get; set; } = [];
        public List<WpArrangeDataModel> WpArrangements { get; set; } = [];

        public string SelBarItem1 { get; set; } = string.Empty;
        public string SelBarItem2 { get; set; } = string.Empty;

        public string Text_Title { get; set; } = string.Empty;
        public string Text_Close { get; set; } = string.Empty;
        public string Text_Restore { get; set; } = string.Empty;
        public string Text_Detect { get; set; } = string.Empty;
        public string Text_Identify { get; set; } = string.Empty;
        public string Text_Adjust { get; set; } = string.Empty;
        public string Text_Preview { get; set; } = string.Empty;
        public string Text_Apply { get; set; } = string.Empty;
        public string SidebarWpConfig { get; set; } = string.Empty;
        public string SidebarLibraryContents { get; set; } = string.Empty;

        private int _selectedWpArrangementsIndex;
        public int SelectedWpArrangementsIndex {
            get => _selectedWpArrangementsIndex;
            set { _selectedWpArrangementsIndex = value; OnPropertyChanged(); }
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
            InitMonitorsBg();
            InitWpArrangments();
        }

        private void InitWpArrangments() {
            WpArrangements.Add(new() {
                Method = "aa",
                Tooltip = "xxxx",
            }); WpArrangements.Add(new() {
                Method = "aa",
                Tooltip = "xxxx",
            }); WpArrangements.Add(new() {
                Method = "aa",
                Tooltip = "xxxx",
            }); WpArrangements.Add(new() {
                Method = "aa",
                Tooltip = "xxxx",
            });
        }

        #region Init
        private void InitText() {
            Text_Title = App.Localizer.GetLocalizedString(Constants.LocalText.WpSettings_Text_Title);
            Text_Close = App.Localizer.GetLocalizedString(Constants.LocalText.Text_Close);
            Text_Detect = App.Localizer.GetLocalizedString(Constants.LocalText.Text_Detect);
            Text_Identify = App.Localizer.GetLocalizedString(Constants.LocalText.Text_Identify);
            Text_Adjust = App.Localizer.GetLocalizedString(Constants.LocalText.Text_Adjust);
            Text_Preview = App.Localizer.GetLocalizedString(Constants.LocalText.Text_Preview);

            SelBarItem1 = App.Localizer.GetLocalizedString(Constants.LocalText.WpSettings_SidebarLibraryContents);
            SelBarItem2 = App.Localizer.GetLocalizedString(Constants.LocalText.WpSettings_SidebarSettings);
        }

        internal void InitMonitors() {
            Monitors.Clear();
            _monitors.Clear();
            switch (_userSettingsClient.Settings.WallpaperArrangement) {
                case WallpaperArrangement.Per: {
                        foreach (var monitor in _monitorManagerClient.Monitors) {
                            _monitors.Add(monitor);
                        }
                    }
                    break;
                case WallpaperArrangement.Expand: {
                        _monitors.Add(new Monitor("Expand"));
                    }
                    break;
                case WallpaperArrangement.Duplicate: {
                        _monitors.Add(new Monitor("Duplicate"));
                    }
                    break;
            }

            Monitors.SetRange(_monitors);
            if (Monitors.Count > 0) {
                SelectedMonitor = Monitors[0];
            }
        }

        private void InitMonitorsBg() {
            foreach (var layout in _userSettingsClient.WallpaperLayouts) {
                string monitorDeviceId = layout.MonitorDeviceId;
                int idx = Monitors.FindIndex(x => x.DeviceId == monitorDeviceId);
                string fileName = Path.GetFileName(layout.FolderPath);
                Monitors[idx].ThumbnailPath = Path.Combine(layout.FolderPath, fileName + Constants.Field.ThumGifSuff);
            }
        }
        #endregion

        #region Control Buttons
        internal async void Close() {
            await _wpControlClient.CloseWallpaperAsync(SelectedMonitor);
            SelectedMonitor.ThumbnailPath = string.Empty;
        }

        internal async Task DetectAsync() {
            InitMonitors();

            await _dialogService.ShowDialogAsync(
                App.Localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_GetMonitorsAsync) + Monitors.Count
                , App.Localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Prompt)
                , App.Localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
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

        internal async Task PreviewAsync() {
            try {
                await _previewSemaphoreSlim.WaitAsync();

                _ctsPreview = new CancellationTokenSource();
                BasicUIComponentUtil.Loading(true, false, [_ctsPreview]);

                if (SelectedMonitor.ThumbnailPath == string.Empty) {
                    return;
                }

                bool isOk = await _wpControlClient.PreviewWallpaperAsync(SelectedMonitor.DeviceId, _ctsPreview.Token);
                if (!isOk) {
                    throw new Exception("Preview Failed.");
                }
            }
            catch (OperationCanceledException) {
                BasicUIComponentUtil.ShowCanceled();
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
            finally {
                BasicUIComponentUtil.Loaded([_ctsPreview]);
                _previewSemaphoreSlim.Release();
            }
        }
        #endregion

        private readonly IList<IMonitor> _monitors = [];
        private readonly IDialogService _dialogService;
        private readonly IMonitorManagerClient _monitorManagerClient;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly SemaphoreSlim _previewSemaphoreSlim = new(1, 1);
        private readonly SemaphoreSlim _adjustSemaphoreSlim = new(1, 1);
        private CancellationTokenSource _ctsPreview, _ctsAdjust;
     
        public class WpArrangeDataModel {
            public string Method { get; set; }
            public string Tooltip { get; set; }
        }
    }
}
