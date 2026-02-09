using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.PlayerWeb.Core.WebView.Windows;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.Views;
using Windows.Storage;
using WinUIEx;

namespace VirtualPaper.WpSettingsPanel.ViewModels {
    public partial class WpSettingsViewModel : ObservableObject {
        public ObservableCollection<IMonitor> Monitors { get; set; } = [];
        public List<WpArrangeDataModel> WpArrangements { get; set; } = [];

        private int _selectedWpArrangementsIndex = -1;
        public int SelectedWpArrangementsIndex {
            get => _selectedWpArrangementsIndex;
            set {
                if (_selectedWpArrangementsIndex == value) return;

                _selectedWpArrangementsIndex = value;
                OnPropertyChanged();
                UpdateWpArrange(value);
            }
        }

        private int _selectedMonitorIndex = 0;
        public int SelectedMonitorIndex {
            get => _selectedMonitorIndex;
            set {
                if (_selectedMonitorIndex == value) return;

                _selectedMonitorIndex = value;
                OnPropertyChanged();
            }
        }

        public ICommand? AddToLibCommand { get; private set; }
        public ICommand? WpCloseCommand { get; private set; }
        public ICommand? WpDetectCommand { get; private set; }
        public ICommand? WpIdentifyCommand { get; private set; }
        public ICommand? WpAdjustCommand { get; private set; }

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
        internal void InitFlyoutData() {
            InitWpArrangments();
            InitMonitors(); // 打开该页面不会触发绑定值修改，需要手动调用更新
        }

        private void InitCommand() {
            AddToLibCommand = new RelayCommand(async () => {
                await ShowAddToLibDialogAsync();
            });
            WpCloseCommand = new RelayCommand(
                Close,
                () => Interlocked.CompareExchange(ref _canClose, 0, 0) == 1
            );
            WpDetectCommand = new RelayCommand(
                Detect,
                () => Interlocked.CompareExchange(ref _canDetect, 0, 0) == 1
            );
            WpIdentifyCommand = new RelayCommand(
                Identify,
                () => Interlocked.CompareExchange(ref _canIdentify, 0, 0) == 1
            );
            WpAdjustCommand = new RelayCommand(
                Adjust,
                () => Interlocked.CompareExchange(ref _canAdjust, 0, 0) == 1
            );
        }

        private void InitMonitors() {
            int cachedIndex = SelectedMonitorIndex;

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
                        var monitor = _monitorManagerClient.PrimaryMonitor.CloneWithPrimaryInfo();
                        monitor.Content = _userSettingsClient.Settings.WallpaperArrangement.ToString();
                        _monitors.Add(monitor);
                    }
                    break;
            }

            Monitors.SetRange(_monitors.OrderBy(m => m.SystemIndex));
            SelectedMonitorIndex = (cachedIndex >= 0 && cachedIndex < _monitors.Count) ? cachedIndex : 0;
        }

        private void InitWpArrangments() {
            WpArrangements.Clear();

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

        public void RegisterLibraryContents(IFilterable filterable) {
            // 清理已死亡的引用
            _filterables.RemoveAll(x => !x.TryGetTarget(out _));

            // 检查是否已存在存活的引用
            if (_filterables.Any(x => x.TryGetTarget(out var target) && target == filterable)) return;

            // 添加弱引用
            _filterables.Add(new WeakReference<IFilterable>(filterable));
        }

        internal void OnFilterChanged(FilterKey fk, string text) {
            // 遍历并清理死亡引用
            for (int i = _filterables.Count - 1; i >= 0; i--) {
                if (_filterables[i].TryGetTarget(out var target)) {
                    if (target.FilterKeyword == fk) {
                        target.ApplyFilter(text);
                    }
                }
                else {
                    _filterables.RemoveAt(i); // 移除已回收的对象
                }
            }
        }

        private async Task ShowAddToLibDialogAsync() {
            IReadOnlyList<IStorageItem> files = [];

            var addToLibViewModel = new AddToLibViewModel();
            var dialog = GlobalDialogUtils.CreateDialogWithoutTitle(
                new AddToLib(addToLibViewModel),
                LanguageUtil.GetI18n(Constants.I18n.Text_Confirm));
            if (dialog == null) return;

            addToLibViewModel.OnRequestAddFile += (_, e) => {
                files = e;
                dialog?.Hide();
            };
            addToLibViewModel.OnRequestAddFolder += async (_, e) => {
                var items = await e.GetItemsAsync();
                files = items;

                dialog?.Hide();
            };

            await dialog.ShowAsync();

            var libViewModel = AppServiceLocator.Services.GetRequiredService<LibraryContentsViewModel>();
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
            if (Interlocked.Exchange(ref _canClose, 0) != 1) return;
            (WpCloseCommand as RelayCommand)?.RaiseCanExecuteChanged();

            await _wpControlClient.CloseWallpaperAsync(Monitors[SelectedMonitorIndex]);
            Monitors[SelectedMonitorIndex].ThumbnailPath = string.Empty;

            Interlocked.Exchange(ref _canClose, 1);
            (WpCloseCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        internal async void Detect() {
            if (Interlocked.Exchange(ref _canDetect, 0) != 1) return;
            (WpDetectCommand as RelayCommand)?.RaiseCanExecuteChanged();

            InitMonitors();
            GlobalMessageUtil.ShowInfo(
                ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)),
                message: nameof(Constants.I18n.Dialog_Content_GetMonitorsAsync),
                key: nameof(Constants.I18n.Dialog_Content_GetMonitorsAsync),
                isNeedLocalizer: true,
                extraMsg: $" {Monitors.Count}");
            await Task.Delay(3000);

            Interlocked.Exchange(ref _canDetect, 1);
            (WpDetectCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        internal async void Identify() {
            if (Interlocked.Exchange(ref _canIdentify, 0) != 1) return;
            (WpIdentifyCommand as RelayCommand)?.RaiseCanExecuteChanged();

            await _monitorManagerClient.IdentifyMonitorsAsync();
            await Task.Delay(3000);

            Interlocked.Exchange(ref _canIdentify, 1);
            (WpIdentifyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        internal async void Adjust() {
            if (Interlocked.Exchange(ref _canAdjust, 0) != 1) return;
            (WpAdjustCommand as RelayCommand)?.RaiseCanExecuteChanged();

            var ctx = ArcPageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null) return;

            var ctsAdjust = new CancellationTokenSource();
            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        if (Monitors[SelectedMonitorIndex].ThumbnailPath == string.Empty) {
                            return;
                        }

                        if (_adjusts.TryGetValue(Monitors[SelectedMonitorIndex].DeviceId, out var adjust)) {
                            adjust.Activate();
                            return;
                        }

                        string monitorId = Monitors[SelectedMonitorIndex].DeviceId;
                        var jsonString = await _wpControlClient.GetPlayerStartArgsByMonitorIdAsync(monitorId, token);
                        var adjustWindow = new AdjustConfig(jsonString);
                        adjustWindow.Closed += (sender, args) => {
                            var ipcMessage = new VirtualPaperReloadEffectCmd();
                            _wpControlClient.SendMessageWallpaperAsync(monitorId, ipcMessage);
                            _adjusts.Remove(monitorId);
                        };
                        adjustWindow.Applied += (sender, context) => {
                            adjustWindow.Close();
                        };
                        adjustWindow.EffectChanged += (sender, e) => {
                            var ipcMessage = new VirtualPaperGeneralEffect() {
                                EffectValue = e,
                            };
                            _wpControlClient.SendMessageWallpaperAsync(monitorId, ipcMessage);
                        };
                        _adjusts.Add(monitorId, adjustWindow);
                        adjustWindow.Show();
                        adjustWindow.Activate();
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
                    }
                }, cts: ctsAdjust);


            Interlocked.Exchange(ref _canAdjust, 1);
            (WpAdjustCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        private readonly IList<IMonitor> _monitors = [];
        private readonly IMonitorManagerClient _monitorManagerClient;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly List<WeakReference<IFilterable>> _filterables = [];
        private readonly Dictionary<string, ArcWindow> _adjusts = [];

        private volatile int _canClose = 1;
        private volatile int _canDetect = 1;
        private volatile int _canIdentify = 1;
        private volatile int _canAdjust = 1;
    }

    public record WpArrangeDataModel(string Method, string Tooltip);
}
