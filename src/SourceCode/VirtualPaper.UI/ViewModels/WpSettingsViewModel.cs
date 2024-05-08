using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels.WpSettingsComponents;
using VirtualPaper.UI.Views.Utils;
using VirtualPaper.UI.Views.WpSettingsComponents;
using WinUI3Localizer;
using Monitor = VirtualPaper.Models.Cores.Monitor;

namespace VirtualPaper.UI.ViewModels
{
    public class WpSettingsViewModel : ObservableObject
    {
        public ObservableList<IMonitor> Monitors { get; set; } = [];

        public string Text_Title { get; set; } = string.Empty;
        public string Text_Close { get; set; } = string.Empty;
        public string Text_Restore { get; set; } = string.Empty;
        public string Text_Detect { get; set; } = string.Empty;
        public string Text_Identify { get; set; } = string.Empty;
        public string Text_Preview { get; set; } = string.Empty;
        public string Text_Apply { get; set; } = string.Empty;
        public string Text_Cancel { get; set; } = string.Empty;
        public string Text_Loading { get; set; } = string.Empty;

        private int _monitorSelectedIdx = 0;
        public int MonitorSelectedIdx
        {
            get => _monitorSelectedIdx;
            set
            {
                if (_monitorSelectedIdx == value) return;

                _monitorSelectedIdx = value;
                OnPropertyChanged();
                _ = UpdateWpConfig();                
            }
        }

        public List<NavigationViewItem> NavsMenuItems { get; set; } = [];

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

        private Page _framePage;
        public Page FramePage
        {
            get { return _framePage; }
            set { _framePage = value; OnPropertyChanged(); }
        }

        private NavigationViewItem _selectedNav;
        public NavigationViewItem SelectedNav
        {
            get { return _selectedNav; }
            set { _selectedNav = value; OnPropertyChanged(); }
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

            InfobarTitle = _localizer.GetLocalizedString("Wpconfig_InfobarTitle");

            NavsMenuItems = [
                new() {
                    Content = _localizer.GetLocalizedString("WpSettings_NavHeader_WpConfig"),
                    Tag = "WpConfig"
                },
                new() {
                    Content = _localizer.GetLocalizedString("WpSettings_NavHeader_LibraryContents"),
                    Tag = "LibraryContents"
                }
            ];
        }

        internal void InitNavItems()
        {
            SelectedNav = NavsMenuItems[0];
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
            }
        }
        #endregion

        #region Control Buttons
        internal void Close()
        {
            _wpConfigViewModel?.Close(Monitors[MonitorSelectedIdx]);
        }

        internal async Task RestoreAsync()
        {
            await _wpConfigViewModel?.RestoreAsync(Monitors[MonitorSelectedIdx]);
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
            await _wpConfigViewModel.PreviewWallpaperAsync();
        }

        internal async Task ApplyAsync(XamlRoot xamlRoot)
        {
            try
            {
                _cancellationTokenSourceForApply = new();
                IsLoading(true);

                #region 合法性检测                
                if (_wpConfigViewModel == null || !_wpConfigViewModel.IsLegal()) return;

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
                string folderName = _wpConfigViewModel.InitUid();

                var detailedInfoViewModel = new DetailedInfoViewModel(_wpConfigViewModel.Wallpaper, true);
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
                _wpConfigViewModel.Apply();
                #endregion

                #region 拷贝入库并更新数据
                _wpConfigViewModel.Wallpaper.Title = detailedInfoViewModel.Title;
                _wpConfigViewModel.Wallpaper.Desc = detailedInfoViewModel.Desc;
                _wpConfigViewModel.Wallpaper.Tags = detailedInfoViewModel.Tags;
                _wpConfigViewModel.UpdateMetaData(folderName);
                #endregion

                TryAddToLibrary();

                #region 执行操作                
                if (_wallpaperControlClient.Wallpapers.FirstOrDefault(x => x.VirtualPaperUid == _wpConfigViewModel.Wallpaper.VirtualPaperUid) != null)
                {
                    _wpConfigViewModel.WpCustomizePage.SendMessage();
                    return;
                }

                SetWallpaperResponse response =
                    await _wallpaperControlClient.SetWallpaperAsync(
                        _wpConfigViewModel.Wallpaper,
                        Monitors[MonitorSelectedIdx],
                        _cancellationTokenSourceForApply.Token);
                if (!response.IsWorked)
                {
                    _ = await new ContentDialog()
                    {
                        XamlRoot = xamlRoot,
                        Title = _localizer.GetLocalizedString("Dialog_Title_Error"),
                        Content = _localizer.GetLocalizedString(response.Msg),
                        PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                        DefaultButton = ContentDialogButton.Primary,
                    }.ShowAsync();
                }
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
            InfobarMsg = _localizer.GetLocalizedString("Wpconfig_InfobarMsg_Err");
            InfoBarIsOpen = true;

            _logger.Error(ex);
        }

        internal void OperationCanceled()
        {
            InfoBarSeverity = InfoBarSeverity.Informational;
            InfobarMsg = _localizer.GetLocalizedString("Wpconfig_InfobarMsg_Cancel");
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

        internal void TryNavPage(string tag)
        {
            IsLoading(true);

            _tag = tag;
            Page page = null;
            if (tag == "WpConfig") _ = UpdateWpConfig();
            else if (tag == "Settings") page = App.Services.GetRequiredService<WpNavSettgins>();
            else page = InstanceUtil<Page>.TryGetInstanceByName(tag, "");

            if (page != null && FramePage != page)
            {
                FramePage = page;
            }

            IsLoading(false);
        }

        internal Page UpdateWpConfig()
        {
            if (_monitorSelectedIdx < 0) return null;

            string idx = (MonitorSelectedIdx + 1).ToString();
            Page page = InstanceUtil<Page>.TryGetInstanceByName("WpConfig", idx, idx);
            _wpConfigViewModel = ((WpConfig)page).DataContext as WpConfigViewModel;

            if (_tag == "WpConfig")
            {
                FramePage = page;
            }

            return page;
        }

        private void TryAddToLibrary()
        {
            var viewModel = App.Services.GetRequiredService<LibraryContentsViewModel>();
            viewModel.AddToLibrary(_wpConfigViewModel.Wallpaper);
        }

        private string _tag;
        private IList<IMonitor> _monitors = [];
        private ILocalizer _localizer;
        private IMonitorManagerClient _monitorManagerClient;
        private IWallpaperControlClient _wallpaperControlClient;
        private IUserSettingsClient _userSettingsClient;
        private WpConfigViewModel _wpConfigViewModel;
        private CancellationTokenSource _cancellationTokenSourceForApply;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
