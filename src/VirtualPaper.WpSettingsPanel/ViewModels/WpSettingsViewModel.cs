using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Grpc.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.Views;
using Windows.Storage;

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

        public ICommand? AddToLibCommand { get; private set; }

        public WpSettingsViewModel(
            IMonitorManagerClient monitorManagerClient,
            IWallpaperControlClient wallpaperControlClient,
            IUserSettingsClient userSettingsClient) {
            _monitorManagerClient = monitorManagerClient;
            _wpControlClient = wallpaperControlClient;
            _userSettingsClient = userSettingsClient;

            InitMonitors();
            InitCommand();
        }

        #region Init
        private void InitCommand() {
            AddToLibCommand = new RelayCommand(async () => {
                await ShowAddToLibDialogAsync();
            });
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

        private async Task ShowAddToLibDialogAsync() {
            IReadOnlyList<IStorageItem> files = [];

            var addToLibViewModel = new AddToLibViewModel();
            var dialog = GlobalDialogUtils.CreateDialogWithoutTitle(
                new AddToLib(addToLibViewModel),
                LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));

            if (dialog == null) return;

            addToLibViewModel.OnRequestAddFile += (_, e) => {
                files = e;
                dialog?.Hide();
            };
            addToLibViewModel.OnRequestAddFolder += async (_, e) => {
                var items =  await e.GetItemsAsync();
                files = items;

                dialog?.Hide();
            };

            await dialog.ShowAsync();

            var libViewModel = ObjectProvider.GetRequiredService<LibraryContentsViewModel>(ObjectLifetime.Singleton);
            await libViewModel.DropFilesAsync(files);
        }

        internal async void UpdateWpArrange(int tag) {
            var ctx = ArcPageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null) return;

            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        var type = (WallpaperArrangement)tag;
                        if (type == _userSettingsClient.Settings.WallpaperArrangement) return;
                        var oldType = _userSettingsClient.Settings.WallpaperArrangement;
                        _userSettingsClient.Settings.WallpaperArrangement = type;
                        await _userSettingsClient.SaveAsync<ISettings>();

                        var response = await _wpControlClient.RestartAllWallpapersAsync();
                        if (response.IsFinished != true) {
                            GlobalMessageUtil.ShowError(
                                ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)),
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
                    }
                });
        }

        #region Buttons Command
        internal async void Close() {
            await _wpControlClient.CloseWallpaperAsync(SelectedMonitor);
            SelectedMonitor.ThumbnailPath = string.Empty;
        }

        internal void Detect() {
            InitMonitors();
            GlobalMessageUtil.ShowInfo(
                ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)),
                message: Constants.I18n.Dialog_Content_GetMonitorsAsync,
                isNeedLocalizer: true,
                extraMsg: $" {MonitorThus.Count}");
        }

        internal async Task IdentifyAsync() {
            await _monitorManagerClient.IdentifyMonitorsAsync();
        }

        internal async Task AdjustAsync() {
            var ctx = ArcPageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null) return;

            var ctsAdjust = new CancellationTokenSource();
            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        await _adjustSemaphoreSlim.WaitAsync(token);

                        if (SelectedMonitor.ThumbnailPath == string.Empty) {
                            return;
                        }

                        bool isOk = await _wpControlClient.AdjustWallpaperAsync(SelectedMonitor.DeviceId, token);
                        if (!isOk) {
                            throw new Exception("Failed to evoke custom adjustment window.");
                        }
                    }
                    catch (Exception ex) when (
                            ex is OperationCanceledException ||
                            (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)) {
                        GlobalMessageUtil.ShowCanceled(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)));
                        return;
                    }
                    catch (Exception ex) {
                        GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
                    }
                    finally {
                        _adjustSemaphoreSlim.Release();
                    }
                }, cts: ctsAdjust);
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
