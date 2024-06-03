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
using VirtualPaper.UI.ViewModels.WpSettingsComponents;
using VirtualPaper.UI.Views.Utils;
using VirtualPaper.UI.Views.WpSettingsComponents;
using WinUI3Localizer;
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
        public string Text_Cancel { get; set; } = string.Empty;
        public string Text_Loading { get; set; } = string.Empty;
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

        private Visibility _loadingVisibility = Visibility.Visible;
        public Visibility LoadingVisibility
        {
            get { return _loadingVisibility; }
            set { _loadingVisibility = value; OnPropertyChanged(); }
        }

        private bool _infoBarIsOpen = false;
        public bool InfoBarIsOpen
        {
            get => _infoBarIsOpen;
            set { _infoBarIsOpen = value; OnPropertyChanged(); }
        }

        private string _infobarTitle;
        public string InfobarTitle
        {
            get { return _infobarTitle; }
            set { _infobarTitle = value; OnPropertyChanged(); }
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

        public WpSettingsViewModel(
            IMonitorManagerClient monitorManagerClient,
            IWallpaperControlClient wallpaperControlClient,
            IUserSettingsClient userSettingsClient)
        {
            _monitorManagerClient = monitorManagerClient;
            _wallpaperControlClient = wallpaperControlClient;
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
            Text_Cancel = _localizer.GetLocalizedString("WpSettings_Text_Cancel");
            Text_Loading = _localizer.GetLocalizedString("WpSettings_Text_Loading");

            InfobarTitle = _localizer.GetLocalizedString("WpConfig_InfobarTitle");

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
            Monitors[MonitorSelectedIdx].ThumbnailPath = string.Empty;
        }

        internal async Task RestoreAsync()
        {
            await WpConfigViewModel?.RestoreAsync(Monitors[MonitorSelectedIdx]);
        }

        internal async Task DetectAsync(XamlRoot xamlRoot)
        {
            InitUpdateLayout();

            await new ContentDialog()
            {
                // XamlRoot must be set in the case of a ContentDialog running in a Desktop app
                // ref: https://learn.microsoft.com/zh-cn/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.contentdialog?
                XamlRoot = xamlRoot,
                Title = _localizer.GetLocalizedString("Dialog_Title_Prompt"),
                Content = _localizer.GetLocalizedString("Dialog_Content_GetMonitorsAsync") + Monitors.Count,
                PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm")
            }.ShowAsync();
        }

        internal async Task IdentifyAsync()
        {
            await _monitorManagerClient.IdentifyMonitorsAsync();
        }

        internal async Task PreviewAsync()
        {
            await WpConfigViewModel.PreviewWallpaperAsync();
        }

        internal async Task ApplyAsync(XamlRoot xamlRoot)
        {
            try
            {
                _cancellationTokenSourceForApply = new();
                IsLoading(true);

                #region 合法性检测                
                if (WpConfigViewModel == null || !WpConfigViewModel.IsLegal()) return;

                if (MonitorSelectedIdx >= Monitors.Count)
                {
                    _ = await new ContentDialog()
                    {
                        XamlRoot = xamlRoot,
                        Title = _localizer.GetLocalizedString("Dialog_Title_Error"),
                        Content = _localizer.GetLocalizedString("Dialog_Content_MinotorUninstall"),
                        PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                        DefaultButton = ContentDialogButton.Primary,
                    }.ShowAsync();

                    return;
                }
                #endregion

                #region 本地导入时录入信息
                string folderName = WpConfigViewModel.InitUid();

                var detailedInfoViewModel = new DetailedInfoViewModel(WpConfigViewModel.Wallpaper, true);
                var cd = new ContentDialog()
                {
                    XamlRoot = xamlRoot,
                    Title = _localizer.GetLocalizedString("Dialog_Title_Edit"),
                    Content = new DetailedInfoView() { DataContext = detailedInfoViewModel },
                    PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                    DefaultButton = ContentDialogButton.Primary,
                };
                var dialogResult = await cd.ShowAsync();
                if (dialogResult != ContentDialogResult.Primary) return;
                #endregion

                #region 应用修改（避免第一次导入时的修改无法生效）
                WpConfigViewModel.Apply();
                #endregion

                #region 拷贝入库并更新数据
                WpConfigViewModel.Wallpaper.Title = detailedInfoViewModel.Title;
                WpConfigViewModel.Wallpaper.Desc = detailedInfoViewModel.Desc;
                WpConfigViewModel.Wallpaper.Tags = detailedInfoViewModel.Tags;
                WpConfigViewModel.UpdateMetaData(folderName);
                #endregion

                TryAddToLibrary();

                #region 执行操作                
                if (_wallpaperControlClient.Wallpapers.FirstOrDefault(x => x.VirtualPaperUid == WpConfigViewModel.Wallpaper.VirtualPaperUid) != null)
                {
                    WpConfigViewModel.WpCustomizePage.SendMessage();
                    return;
                }

                SetWallpaperResponse response =
                    await _wallpaperControlClient.SetWallpaperAsync(
                        WpConfigViewModel.Wallpaper,
                        Monitors[MonitorSelectedIdx],
                        _cancellationTokenSourceForApply.Token);
                if (!response.IsFinished)
                {
                    _ = await new ContentDialog()
                    {
                        XamlRoot = xamlRoot,
                        Title = _localizer.GetLocalizedString("Dialog_Title_Error"),
                        Content = response.Msg,
                        PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                        DefaultButton = ContentDialogButton.Primary,
                    }.ShowAsync();
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
                IsLoading(false);
                _cancellationTokenSourceForApply?.Dispose();
                _cancellationTokenSourceForApply = null;
            }
        }
        #endregion

        internal void ErrOccoured(Exception ex)
        {
            InfoBarSeverity = InfoBarSeverity.Error;
            InfobarMsg = _localizer.GetLocalizedString("WpConfig_InfobarMsg_Err");
            InfoBarIsOpen = true;

            _logger.Error(ex);
        }

        internal void OperationCanceled()
        {
            InfoBarSeverity = InfoBarSeverity.Informational;
            InfobarMsg = _localizer.GetLocalizedString("WpConfig_InfobarMsg_Cancel");
            InfoBarIsOpen = true;

            _logger.Info(InfobarMsg);
        }

        internal void IsLoading(bool isLoading)
        {
            if (isLoading)
            {
                IsEnable = false;
                LoadingVisibility = Visibility.Visible;
            }
            else
            {
                IsEnable = true;
                LoadingVisibility = Visibility.Collapsed;
            }
        }

        private void TryAddToLibrary()
        {
            var viewModel = App.Services.GetRequiredService<LibraryContentsViewModel>();
            viewModel.AddToLibrary(WpConfigViewModel.Wallpaper);
        }

        private IList<IMonitor> _monitors = [];
        private ILocalizer _localizer;
        private IMonitorManagerClient _monitorManagerClient;
        private IWallpaperControlClient _wallpaperControlClient;
        private IUserSettingsClient _userSettingsClient;
        private CancellationTokenSource _cancellationTokenSourceForApply;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
