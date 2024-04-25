using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels.WpSettingsComponents;
using VirtualPaper.UI.Views.Utils;
using VirtualPaper.UI.Views.WpSettingsComponents;
using WinUI3Localizer;

namespace VirtualPaper.UI.ViewModels
{
    public class WpSettingsViewModel : ObservableObject
    {
        public ObservableCollection<IMonitor> Monitors { get; set; } = [];
        public string Text_Title { get; set; } = string.Empty;
        public string Text_Close { get; set; } = string.Empty;
        public string Text_Restore { get; set; } = string.Empty;
        public string Text_Detect { get; set; } = string.Empty;
        public string Text_Identify { get; set; } = string.Empty;
        public string Text_Preview { get; set; } = string.Empty;
        public string Text_Apply { get; set; } = string.Empty;
        public string Text_Cancel { get; set; } = string.Empty;
        public string Text_Loading { get; set; } = string.Empty;

        private int _thuMonitorSelectedIdx = 0;
        public int ThuMonitorSelectedIdx
        {
            get => _thuMonitorSelectedIdx;
            set { _thuMonitorSelectedIdx = value; OnPropertyChanged(); }
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

            NavsMenuItems = [
                new() {
                Content = _localizer.GetLocalizedString("WpSettings_NavHeader_WpConfig"),
                Tag = "WpConfig"
                },
                new(){
                Content = _localizer.GetLocalizedString("WpSettings_NavHeader_LibraryContents"),
                Tag = "LibraryContents"
            }];
        }

        internal void InitNavItems()
        {
            SelectedNav = NavsMenuItems[0];
        }

        internal void InitMonitors()
        {
            Monitors.Clear();
            foreach (var monitor in _monitorManagerClient.Monitors)
            {
                Monitors.Add(monitor);
            }

            if (Monitors.Count > 0) ThuMonitorSelectedIdx = 0;
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

        internal void Close()
        {
            _wpConfigViewModel?.Close(Monitors[ThuMonitorSelectedIdx]);
        }

        internal async Task Restore()
        {
            await _wpConfigViewModel?.RestoreAsync(Monitors[ThuMonitorSelectedIdx]);
        }

        internal async Task DetectAsync(XamlRoot xamlRoot)
        {
            InitMonitors();
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
            if (_wpConfigViewModel == null
                || _wpConfigViewModel.Wallpaper == null
                || _wpConfigViewModel.Wallpaper.FolderPath == string.Empty
                || _wpConfigViewModel.Wallpaper.WpCustomizePathTmp == string.Empty) return;

            await _wallpaperControlClient.PreviewWallpaperAsync(_wpConfigViewModel.Wallpaper);
        }

        internal async Task ApplyAsync(XamlRoot xamlRoot)
        {
            try
            {
                _cancellationTokenSourceForApply = new();
                IsLoading(true);

                #region 合法性检测
                if (_wpConfigViewModel == null
                || _wpConfigViewModel.Wallpaper == null
                || _wpConfigViewModel.Wallpaper.FolderPath == string.Empty) return;

                if (ThuMonitorSelectedIdx >= Monitors.Count)
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
                var dialogResult = await new ContentDialog()
                {
                    XamlRoot = xamlRoot,
                    Title = _localizer.GetLocalizedString("Dialog_Title_Edit"),
                    Content = new DetailedInfoView()
                    {
                        DataContext = new DetailedInfoViewModel(_wpConfigViewModel.Wallpaper, true),
                    },
                    PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                    DefaultButton = ContentDialogButton.Primary,
                }.ShowAsync();
                if (dialogResult != ContentDialogResult.Primary) return;
                #endregion

                #region 拷贝入库并更新数据
                DirectoryInfo info = new(_wpConfigViewModel.Wallpaper.FolderPath);
                string tagetFolder = Path.Combine(_userSettingsClient.Settings.WallpaperDir, Constants.CommonPartialPaths.WallpaperInstallDir, info.Name);
                if (!Directory.Exists(tagetFolder))
                {
                    FileUtil.DirectoryCopy(
                        _wpConfigViewModel.Wallpaper.FolderPath,
                        tagetFolder,
                        true);
                }
                string oldFolderPath = _wpConfigViewModel.Wallpaper.FolderPath;

                _wpConfigViewModel.Wallpaper.FolderPath = _wpConfigViewModel.Wallpaper.FolderPath.Replace(oldFolderPath, tagetFolder);
                _wpConfigViewModel.Wallpaper.ThumbnailPath = _wpConfigViewModel.Wallpaper.ThumbnailPath.Replace(oldFolderPath, tagetFolder);
                _wpConfigViewModel.Wallpaper.WpCustomizePath = _wpConfigViewModel.Wallpaper.WpCustomizePath.Replace(oldFolderPath, tagetFolder);
                _wpConfigViewModel.Wallpaper.WpCustomizePathUsing = _wpConfigViewModel.Wallpaper.WpCustomizePathUsing.Replace(oldFolderPath, tagetFolder);
                _wpConfigViewModel.Wallpaper.WpCustomizePathTmp = _wpConfigViewModel.Wallpaper.WpCustomizePathTmp.Replace(oldFolderPath, tagetFolder);

                //_wpConfigViewModel.Wallpaper.FolderPath = tagetFolder;
                //_wpConfigViewModel.Wallpaper.ThumbnailPath = Path.Combine(tagetFolder, Path.GetFileName(_wpConfigViewModel.Wallpaper.ThumbnailPath));
                //_wpConfigViewModel.Wallpaper.WpCustomizePath = Path.Combine(tagetFolder, "WpCustomize.json");

                JsonStorage<IMetaData>.StoreData(Path.Combine(_wpConfigViewModel.Wallpaper.FolderPath, "MetaData.json"), _wpConfigViewModel.Wallpaper);
                #endregion

                #region 执行操作
                _wpConfigViewModel.Apply();

                SetWallpaperResponse response =
                    await _wallpaperControlClient.SetWallpaperAsync(
                        _wpConfigViewModel.Wallpaper,
                        Monitors[ThuMonitorSelectedIdx],
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
                _wpConfigViewModel?.OperationCanceled();
            }
            catch (RpcException ex)
            {
                if (ex.StatusCode == StatusCode.Cancelled)
                    _wpConfigViewModel?.OperationCanceled();
                else _wpConfigViewModel?.ErrOccoured(ex);
            }
            catch (Exception ex)
            {
                _wpConfigViewModel?.ErrOccoured(ex);
            }
            finally
            {
                IsLoading(false);
                _cancellationTokenSourceForApply?.Dispose();
                _cancellationTokenSourceForApply = null;
            }
        }



        internal void TryNavPage(string tag)
        {
            IsLoading(true);

            Page page;
            if (tag == "WpConfig") page = GetTargetMonitorWallpaperInfo();
            else if (tag == "Settings") page = InstanceUtil<Page>.TryGetInstanceByName("WpNavSettgins", "");
            else page = InstanceUtil<Page>.TryGetInstanceByName(tag, "");

            if (FramePage != page)
            {
                FramePage = page;
            }            

            IsLoading(false);
        }

        internal Page GetTargetMonitorWallpaperInfo()
        {
            string idx = (ThuMonitorSelectedIdx + 1).ToString();
            Page page = InstanceUtil<Page>.TryGetInstanceByName("WpConfig", idx, idx);
            _wpConfigViewModel = ((WpConfig)page).DataContext as WpConfigViewModel;
            _wpConfigViewModel._onIsLoading += IsLoading;            

            return page;
        }

        internal bool Delete(string folderPath)
        {
            try
            {
                DirectoryInfo di = new(folderPath);
                di.Delete(true);

                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        internal void AddToLibrary()
        {
            if (_wpConfigViewModel == null
                || _wpConfigViewModel.Wallpaper == null
                || _wpConfigViewModel.Wallpaper.FolderPath == string.Empty) return;

            var viewModel = App.Services.GetRequiredService<LibraryContentsViewModel>();
            viewModel.LibraryWallpapers.Add(_wpConfigViewModel.Wallpaper);
        }

        private ILocalizer _localizer;
        private IMonitorManagerClient _monitorManagerClient;
        private IWallpaperControlClient _wallpaperControlClient;
        private IUserSettingsClient _userSettingsClient;
        private WpConfigViewModel _wpConfigViewModel;
        private CancellationTokenSource _cancellationTokenSourceForApply;
    }
}
