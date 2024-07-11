using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UI.UserControls;
using VirtualPaper.UI.ViewModels.Utils;
using VirtualPaper.UI.ViewModels.WpSettingsComponents;
using VirtualPaper.UI.Views.Utils;
using VirtualPaper.UI.Views.WpSettingsComponents;
using WinUI3Localizer;
using static VirtualPaper.UI.Services.Interfaces.IDialogService;
using Monitor = VirtualPaper.Models.Cores.Monitor;

namespace VirtualPaper.UI.ViewModels
{
    public class WpSettingsViewModel : ObservableObject
    {
        public Action ResetNavDefault;

        public ObservableList<IMonitor> Monitors { get; set; } = [];
        public List<NavigationViewItem> NavMenuItems { get; set; } = [];

        public string Text_Title { get; set; } = string.Empty;
        public string Text_Close { get; set; } = string.Empty;
        public string Text_Restore { get; set; } = string.Empty;
        public string Text_Detect { get; set; } = string.Empty;
        public string Text_Identify { get; set; } = string.Empty;
        public string Text_Preview { get; set; } = string.Empty;
        public string Text_Apply { get; set; } = string.Empty;
        //public string Text_Cancel { get; set; } = string.Empty;
        //public string Text_Loading { get; set; } = string.Empty;
        public string SidebarWpConfig { get; set; } = string.Empty;
        public string SidebarLibraryContents { get; set; } = string.Empty;
        public WpConfigViewModel WpConfigViewModel { get; set; } = null;

        private int _monitorSelectedIdx = 0;
        public int MonitorSelectedIdx
        {
            get => _monitorSelectedIdx;
            set
            {
                int newValue = value < 0 ? _monitorSelectedIdx : value;

                if (_monitorSelectedIdx == newValue) return;
                _monitorSelectedIdx = newValue;
                OnPropertyChanged();
                WpConfigView?.InitContent();
            }
        }

        public WpConfig WpConfigView { get; set; }

        private bool _isEnable = false;
        public bool IsEnable
        {
            get { return _isEnable; }
            set { _isEnable = value; OnPropertyChanged(); }
        }

        //private Visibility _cancelVisibility = Visibility.Visible;
        //public Visibility CancelVisibility
        //{
        //    get { return _cancelVisibility; }
        //    set { _cancelVisibility = value; OnPropertyChanged(); }
        //}

        #region loading
        private Visibility _loadingVisibility = Visibility.Visible;
        public Visibility LoadingVisibility
        {
            get { return _loadingVisibility; }
            set { _loadingVisibility = value; OnPropertyChanged(); }
        }

        private int _curValue;
        public int CurValue
        {
            get { return _curValue; }
            set { _curValue = value; OnPropertyChanged(); }
        }

        private int _totalValue;
        public int TotalValue
        {
            get { return _totalValue; }
            set { _totalValue = value; OnPropertyChanged(); }
        }

        private bool _cancelEanble;
        public bool CancelEnable
        {
            get { return _cancelEanble; }
            set { _cancelEanble = value; OnPropertyChanged(); }
        }

        private bool _progressbarEnable;
        public bool ProgressbarEnable
        {
            get { return _progressbarEnable; }
            set { _progressbarEnable = value; OnPropertyChanged(); }
        }

        private CancellationTokenSource[] _ctsTokens = [];
        public CancellationTokenSource[] CtsTokens
        {
            get { return _ctsTokens; }
            set { _ctsTokens = value; OnPropertyChanged(); }
        }
        #endregion

        private bool _infoBarIsOpen = false;
        public bool InfoBarIsOpen
        {
            get => _infoBarIsOpen;
            set { _infoBarIsOpen = value; OnPropertyChanged(); }
        }

        private string _infobarMsg;
        public string InfobarMsg
        {
            get { return _infobarMsg; }
            set { _infobarMsg = value; OnPropertyChanged(); }
        }

        private InfoBarSeverity _infoBarSeverity;
        public InfoBarSeverity InfoBarSeverity
        {
            get { return _infoBarSeverity; }
            set { _infoBarSeverity = value; OnPropertyChanged(); }
        }

        private object _frameContainer;
        public object FrameContainer
        {
            get { return _frameContainer; }
            set { _frameContainer = value; OnPropertyChanged(); }
        }

        public WpSettingsViewModel(
            IDialogService dialogService,
            IMonitorManagerClient monitorManagerClient,
            IWallpaperControlClient wallpaperControlClient,
            IUserSettingsClient userSettingsClient)
        {
            _dialogService = dialogService;
            _monitorManagerClient = monitorManagerClient;
            _wpControl = wallpaperControlClient;
            _userSettingsClient = userSettingsClient;

            App.Services.GetRequiredService<WpNavSettingsViewModel>().InitUpdateLayout = InitUpdateLayout;

            InitText();
        }

        #region Init
        private void InitText()
        {
            _localizer = Localizer.Get();

            Text_Title = _localizer.GetLocalizedString("WpSettings_Text_Title");
            Text_Close = _localizer.GetLocalizedString("WpSettings_Text_Close");
            Text_Restore = _localizer.GetLocalizedString("WpSettings_Text_Restore");
            Text_Detect = _localizer.GetLocalizedString("WpSettings_Text_Detect");
            Text_Identify = _localizer.GetLocalizedString("WpSettings_Text_Identify");
            Text_Preview = _localizer.GetLocalizedString("WpSettings_Text_Preview");
            Text_Apply = _localizer.GetLocalizedString("WpSettings_Text_Apply");
            //Text_Cancel = _localizer.GetLocalizedString("WpSettings_Text_Cancel");
            //Text_Loading = _localizer.GetLocalizedString("WpSettings_Text_Loading");

            SidebarWpConfig = _localizer.GetLocalizedString("WpSettings_SidebarWpConfig");
            SidebarLibraryContents = _localizer.GetLocalizedString("WpSettings_SidebarLibraryContents");
        }

        internal void ResetNavSeletedItem()
        {
            ResetNavDefault?.Invoke();
        }

        internal void InitUpdateLayout()
        {
            _monitors.Clear();
            switch (_userSettingsClient.Settings.WallpaperArrangement)
            {
                case WallpaperArrangement.Per:
                    {
                        foreach (var monitor in _monitorManagerClient.Monitors)
                        {
                            _monitors.Add(monitor);
                        }
                    }
                    break;
                case WallpaperArrangement.Expand:
                    {
                        _monitors.Add(new Monitor(null, "Expand"));
                    }
                    break;
                case WallpaperArrangement.Duplicate:
                    {
                        _monitors.Add(new Monitor(null, "Duplicate"));
                    }
                    break;
            }

            Monitors.SetRange(_monitors);
            if (Monitors.Count > 0)
            {
                MonitorSelectedIdx = 0;
                OnPropertyChanged(nameof(MonitorSelectedIdx));
            }
        }
        #endregion

        #region Control Buttons
        internal void Close()
        {
            WpConfigViewModel?.Close(Monitors[MonitorSelectedIdx]);
            //Monitors[MonitorSelectedIdx].ThumbnailPath = string.Empty;
        }

        internal async Task RestoreAsync()
        {
            await WpConfigViewModel?.RestoreAsync(Monitors[MonitorSelectedIdx]);
        }

        internal async Task DetectAsync(XamlRoot xamlRoot)
        {
            InitUpdateLayout();

            await _dialogService.ShowDialogAsync(
                _localizer.GetLocalizedString("Dialog_Content_GetMonitorsAsync") + Monitors.Count
                , _localizer.GetLocalizedString("Dialog_Title_Prompt")
                , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
        }

        internal async Task IdentifyAsync()
        {
            await _monitorManagerClient.IdentifyMonitorsAsync();
        }

        internal async Task PreviewAsync()
        {            
            await WpConfigViewModel.PreviewWallpaperAsync();
        }

        internal async Task ApplyAsync()
        {
            try
            {
                _ctsApply = new();
                Loading(true, false, [_ctsApply]);

                #region 合法性检测                
                if (WpConfigViewModel == null || !WpConfigViewModel.IsLegal()) return;

                if (MonitorSelectedIdx >= Monitors.Count)
                {
                    await _dialogService.ShowDialogAsync(
                        _localizer.GetLocalizedString("Dialog_Content_MinotorUninstall")
                        , _localizer.GetLocalizedString("Dialog_Title_Error")
                        , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));

                    return;
                }
                #endregion

                #region 本地导入时录入信息
                string folderName = WpConfigViewModel.InitUid();

                var detailedInfoViewModel = new DetailedInfoViewModel(WpConfigViewModel.Wallpaper, true, false, false);
                var dialogRes = await _dialogService.ShowDialogWithoutTitleAsync(
                    new DetailedInfoView(detailedInfoViewModel)
                    , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
                if (dialogRes != DialogResult.Primary) return;
                #endregion

                #region 应用修改（避免第一次导入时的修改无法生效）
                WpConfigViewModel.Apply();
                #endregion

                #region 拷贝入库并更新数据
                WpConfigViewModel.Wallpaper.Title = detailedInfoViewModel.Title;
                WpConfigViewModel.Wallpaper.Desc = detailedInfoViewModel.Desc;
                WpConfigViewModel.Wallpaper.Tags = detailedInfoViewModel.Tags;
                WpConfigViewModel.UpdateMetaData(folderName);
                AddToLibrary();
                #endregion

                #region 执行操作             
                // 同一显示器 同一壁纸 更改自定义设置
                if (_wpControl.Wallpapers.FirstOrDefault(x => x.VirtualPaperUid == WpConfigViewModel.Wallpaper.VirtualPaperUid) != null)
                {
                    WpConfigViewModel.WpCustomizePage.SendMessage();
                    return;
                }

                // 同一显示器 更换壁纸
                UpdateWpResponse updateResponse = await _wpControl.UpdateWpAsync(
                    Monitors[MonitorSelectedIdx],
                    WpConfigViewModel.Wallpaper,
                    _ctsApply.Token);
                if (updateResponse.IsFinished) return;

                // 对某一显示器第一次应用壁纸
                SetWallpaperResponse setUesponse = await _wpControl.SetWallpaperAsync(
                   Monitors[MonitorSelectedIdx],
                   WpConfigViewModel.Wallpaper,
                    _ctsApply.Token);
                if (!setUesponse.IsFinished)
                {
                    await _dialogService.ShowDialogAsync(
                        _localizer.GetLocalizedString("Dialog_Content_ApplyError")
                        , _localizer.GetLocalizedString("Dialog_Title_Error")
                        , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
                }

                //Monitors[MonitorSelectedIdx].ThumbnailPath = WpConfigViewModel.Wallpaper.ThumbnailPath;
                #endregion
            }
            catch (OperationCanceledException)
            {
                OperationCanceled();
            }
            catch (RpcException ex)
            {
                if (ex.StatusCode == StatusCode.Cancelled) OperationCanceled();
                else ErrOccoured(ex);
            }
            catch (Exception ex)
            {
                ErrOccoured(ex);
            }
            finally
            {
                Loaded();
                _ctsApply?.Dispose();
                _ctsApply = null;
            }
        }
        #endregion

        internal void ErrOccoured(Exception ex)
        {
            _logger.Error(ex.Message);
            InfoBarSeverity = InfoBarSeverity.Error;
            InfobarMsg = _localizer.GetLocalizedString("WpConfig_InfobarMsg_Err");
            InfoBarIsOpen = true;
        }

        internal void OperationCanceled()
        {
            _logger.Info(InfobarMsg);
            InfoBarSeverity = InfoBarSeverity.Informational;
            InfobarMsg = _localizer.GetLocalizedString("WpConfig_InfobarMsg_Cancel");
            InfoBarIsOpen = true;
        }

        internal void ShowMessge(string msg, InfoBarSeverity infoBarSeverity)
        {
            _logger.Info(InfobarMsg);
            InfoBarSeverity = infoBarSeverity;
            InfobarMsg = _localizer.GetLocalizedString(msg);
            InfoBarIsOpen = true;
        }

        internal void Loading(
            bool cancelEnable,
            bool progressbarEnable,
            CancellationTokenSource[] cts)
        {
            IsEnable = false;
            CtsTokens = cts;
            CancelEnable = cancelEnable;
            ProgressbarEnable = progressbarEnable;
            LoadingVisibility = Visibility.Visible;

            //_loadingUsrctrlViewModel = new LoadingUsrctrlViewModel(cancelEnable, progressbarEnable, cts);
            //_loadingUsrctrl = new() { DataContext = _loadingUsrctrlViewModel };

            //IsEnable = false;
            //_updateLoadingprogressbar = _loadingUsrctrlViewModel.UpdateProgressbarValue;
            //FrameContainer ??= _loadingUsrctrl;
        }

        internal void Loaded()
        {
            IsEnable = true;
            LoadingVisibility = Visibility.Collapsed;
            //_loadingUsrctrlViewModel = null;
            //_updateLoadingprogressbar = null;
        }

        internal void UpdateProgressbarValue(int curValue, int toltalValue)
        {
            //_updateLoadingprogressbar?.Invoke(curValue, toltalValue);
            TotalValue = toltalValue;
            CurValue = curValue;
        }

        //internal void Cancel()
        //{
        //    WpConfigViewModel.Cancel();
        //    App.Services.GetRequiredService<LibraryContentsViewModel>().Cancel();
        //}

        private void AddToLibrary()
        {
            var viewModel = App.Services.GetRequiredService<LibraryContentsViewModel>();
            viewModel.AddToLibrary(WpConfigViewModel.Wallpaper);
        }

        private IList<IMonitor> _monitors = [];
        private ILocalizer _localizer;
        private IDialogService _dialogService;
        private IMonitorManagerClient _monitorManagerClient;
        private IWallpaperControlClient _wpControl;
        private IUserSettingsClient _userSettingsClient;
        //private Action<int, int> _updateLoadingprogressbar;
        private CancellationTokenSource _ctsApply;
        //private LoadingUsrctrl _loadingUsrctrl;
        //private LoadingUsrctrlViewModel _loadingUsrctrlViewModel;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
