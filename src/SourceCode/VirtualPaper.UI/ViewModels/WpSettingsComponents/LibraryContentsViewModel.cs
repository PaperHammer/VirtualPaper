using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Grpc.Client;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels.AppSettings;
using VirtualPaper.UI.Views;
using VirtualPaper.UI.Views.Utils;
using VirtualPaper.UI.Views.WpSettingsComponents;
using Windows.ApplicationModel.Resources;
using WinUI3Localizer;

namespace VirtualPaper.UI.ViewModels.WpSettingsComponents
{
    public class LibraryContentsViewModel : ObservableObject
    {
        public ObservableCollection<IMetaData> LibraryWallpapers { get; set; }
        public string Text_Loading { get; set; } = string.Empty;
        public string MenuFlyout_Text_DetailedInfo { get; set; } = string.Empty;
        public string MenuFlyout_Text_EditInfo { get; set; } = string.Empty;
        public string MenuFlyout_Text_Preview { get; set; } = string.Empty;
        public string MenuFlyout_Text_Import { get; set; } = string.Empty;
        public string MenuFlyout_Text_Apply { get; set; } = string.Empty;
        public string MenuFlyout_Text_ShowOnDisk { get; set; } = string.Empty;
        public string MenuFlyout_Text_Export { get; set; } = string.Empty;
        public string MenuFlyout_Text_Delete { get; set; } = string.Empty;

        private Visibility _loadingVisibility = Visibility.Collapsed;
        public Visibility LoadingVisibility
        {
            get { return _loadingVisibility; }
            set { _loadingVisibility = value; OnPropertyChanged(); }
        }

        public LibraryContentsViewModel(
            IUserSettingsClient userSettingsClient,
            IWallpaperControlClient wallpaperControlClient,
            GeneralSettingViewModel generalSettingViewModel)
        {
            _userSettingsClient = userSettingsClient;

            _wallpaperControlClient = wallpaperControlClient;

            generalSettingViewModel.WallpaperDirChanged += (s, e) => WallpaperDirectoryUpdate(e);

            InitColletions();
            InitText();
            InitContents();
        }

        private void InitColletions()
        {
            LibraryWallpapers = [];
            _wallpaperScanFolders =
            [
                Path.Combine(_userSettingsClient.Settings.WallpaperDir, Constants.CommonPartialPaths.WallpaperInstallDir),
            ];
        }

        private void InitText()
        {
            _localizer = Localizer.Get();

            Text_Loading = _localizer.GetLocalizedString("WpSettings_Text_Loading");
            MenuFlyout_Text_DetailedInfo = _localizer.GetLocalizedString("MenuFlyout_Text_DetailedInfo");
            MenuFlyout_Text_EditInfo = _localizer.GetLocalizedString("MenuFlyout_Text_EditInfo");
            MenuFlyout_Text_Preview = _localizer.GetLocalizedString("MenuFlyout_Text_Preview");
            MenuFlyout_Text_Import = _localizer.GetLocalizedString("MenuFlyout_Text_Import");
            MenuFlyout_Text_Apply = _localizer.GetLocalizedString("MenuFlyout_Text_Apply");
            MenuFlyout_Text_ShowOnDisk = _localizer.GetLocalizedString("MenuFlyout_Text_ShowOnDisk");
            MenuFlyout_Text_Export = _localizer.GetLocalizedString("MenuFlyout_Text_Export");
            MenuFlyout_Text_Delete = _localizer.GetLocalizedString("MenuFlyout_Text_Delete");
        }

        private void InitContents()
        {
            try
            {
                IsLoading(true);
                LibraryWallpapers.Clear();

                foreach (var item in ScanWallpaperFolders(_wallpaperScanFolders))
                {
                    LibraryWallpapers.Add(item.Item2);
                }
            }
            catch (Exception ex)
            {
                LibraryWallpapers.Clear();
                _logger.Error(ex);
            }
            finally
            {
                IsLoading(false);
            }
        }

        internal void IsLoading(bool isLoading)
        {
            if (isLoading)
            {
                LoadingVisibility = Visibility.Visible;
            }
            else
            {
                LoadingVisibility = Visibility.Collapsed;
            }
        }

        internal async Task Preview(string folderPath, XamlRoot xamlRoot)
        {
            var metaData = LibraryWallpapers.FirstOrDefault(x => x.FolderPath == folderPath, null);
            if (metaData == null)
            {
                _ = await new ContentDialog()
                {
                    XamlRoot = xamlRoot,
                    Title = _localizer.GetLocalizedString("Dialog_Title_Prompt"),
                    Content = _localizer.GetLocalizedString("Dialog_Content_PreviewLibraryContentFailed"),
                    PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm")
                }.ShowAsync();
                return;
            }

            await _wallpaperControlClient.PreviewWallpaperAsync(metaData);
        }

        internal async void DetailedInfo(IMetaData rightTrappedItem, XamlRoot xamlRoot)
        {
            await ShowDetailedInfoPop(true, rightTrappedItem, xamlRoot, false);
        }

        internal async void EditInfo(IMetaData rightTrappedItem, XamlRoot xamlRoot)
        {
            bool res = await ShowDetailedInfoPop(false, rightTrappedItem, xamlRoot, true);
            if (!res) return;

            JsonStorage<IMetaData>.StoreData(Path.Combine(rightTrappedItem.FolderPath, "MetaData.json"), rightTrappedItem);
        }

        internal async Task PreviewAsync(IMetaData metaData)
        {
            await _wallpaperControlClient.PreviewWallpaperAsync(metaData);
        }

        internal async Task ImportAsync(string filePath)
        {
            GetViewModels(out WpSettingsViewModel wpSettingsViewModel, out WpConfigViewModel wpConfigViewModel);
            wpSettingsViewModel.SelectedNav = wpSettingsViewModel.NavsMenuItems[0];
            await wpConfigViewModel.TryImportFromLocalAsync(filePath);
        }

        internal async Task ApplyAsync(XamlRoot xamlRoot)
        {
            GetViewModels(out WpSettingsViewModel wpSettingsViewModel, out _);
            await wpSettingsViewModel.ApplyAsync(xamlRoot);
        }

        internal async Task DeleteAsync(IMetaData rightTrappedItem, XamlRoot xamlRoot)
        {
            GetViewModels(out WpSettingsViewModel wpSettingsViewModel, out WpConfigViewModel wpConfigViewModel);
            try
            {
                var dialogResult = await new ContentDialog()
                {
                    XamlRoot = xamlRoot,
                    Title = _localizer.GetLocalizedString("Dialog_Title_Prompt"),
                    Content = _localizer.GetLocalizedString("Dialog_Content_LibraryDelete"),
                    PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                    SecondaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Cancel"),
                    DefaultButton = ContentDialogButton.Primary,
                }.ShowAsync();

                if (dialogResult != ContentDialogResult.Primary) return;

                bool isUsing = wpConfigViewModel.Wallpaper.FolderPath == rightTrappedItem.FolderPath;
                if (isUsing)
                {
                    _ = await new ContentDialog()
                    {
                        XamlRoot = xamlRoot,
                        Title = _localizer.GetLocalizedString("Dialog_Title_Prompt"),
                        Content = _localizer.GetLocalizedString("Dialog_Content_WpIsUsing"),
                        PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                        DefaultButton = ContentDialogButton.Primary,
                    }.ShowAsync();

                    return;
                }

                bool resDel = wpSettingsViewModel.Delete(rightTrappedItem.FolderPath);
                if (resDel)
                {
                    LibraryWallpapers.Remove(rightTrappedItem);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        #region utils
        private async Task<bool> ShowDetailedInfoPop(bool fromLocal, IMetaData rightTrappedItem, XamlRoot xamlRoot, bool isEditable)
        {
            if (fromLocal)
                rightTrappedItem = JsonStorage<MetaData>.LoadData(Path.Combine(rightTrappedItem.FolderPath, "MetaData.json"));

            var dialogResult = await new ContentDialog()
            {
                XamlRoot = xamlRoot,
                Title = _localizer.GetLocalizedString("Dialog_Title_About"),
                Content = new DetailedInfoView()
                {
                    DataContext = new DetailedInfoViewModel(rightTrappedItem, isEditable),
                },
                PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                DefaultButton = ContentDialogButton.Primary,
            }.ShowAsync();

            return dialogResult == ContentDialogResult.Primary;
        }

        private void GetViewModels(out WpSettingsViewModel wpSettingsViewModel, out WpConfigViewModel wpConfigViewModel)
        {
            wpSettingsViewModel = App.Services.GetRequiredService<WpSettingsViewModel>();
            WpConfig wpConfig = wpSettingsViewModel.GetTargetMonitorWallpaperInfo() as WpConfig;

            wpConfigViewModel = wpConfig.DataContext as WpConfigViewModel;
        }

        internal async void ShowErr(XamlRoot xamlRoot)
        {
            _ = await new ContentDialog()
            {
                XamlRoot = xamlRoot,
                Title = _localizer.GetLocalizedString("Dialog_Title_Prompt"),
                Content = _localizer.GetLocalizedString("Dialog_Title_LibraryContentErr"),
                PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                DefaultButton = ContentDialogButton.Primary,
            }.ShowAsync();
        }

        /// <summary>
        /// Rescans wallpaper directory and update library.
        /// </summary>
        private void WallpaperDirectoryUpdate(string[] dirs)
        {
            string preDir = dirs[0], newDir = dirs[1];

            LibraryWallpapers.Clear();

            _wallpaperScanFolders.Clear();
            _wallpaperScanFolders.Add(Path.Combine(newDir, Constants.CommonPartialPaths.WallpaperInstallDir));

            foreach (var item in ScanWallpaperFolders(_wallpaperScanFolders))
            {
                string filePath = item.Item1;
                var md = item.Item2;

                md.FolderPath = md.FolderPath.Replace(preDir, newDir);
                md.ThumbnailPath = md.ThumbnailPath.Replace(preDir, newDir);
                md.WpCustomizePath = md.WpCustomizePath.Replace(preDir, newDir);
                md.WpCustomizePathUsing = md.WpCustomizePathUsing.Replace(preDir, newDir);
                md.WpCustomizePathTmp = md.WpCustomizePathTmp.Replace(preDir, newDir);

                JsonStorage<IMetaData>.StoreData(filePath, md);

                LibraryWallpapers.Add(md);
            }
        }

        /// <summary>
        /// Load wallpapers from the given parent folder(), only top directory is scanned.
        /// </summary>
        /// <param name="folderPaths">Parent folders to search for subdirectories.</param>
        /// <returns>Sorted(based on Title) wallpaper data.</returns>
        private IEnumerable<(string, IMetaData)> ScanWallpaperFolders(List<string> folderPaths)
        {
            foreach (string storeDir in folderPaths)
            {
                DirectoryInfo root = new(storeDir);
                DirectoryInfo[] folders = root.GetDirectories();

                foreach (DirectoryInfo folder in folders)
                {
                    string[] files = Directory.GetFiles(folder.FullName);
                    foreach (string file in files)
                    {
                        if (Path.GetFileName(file) == "MetaData.json")
                        {
                            var md = JsonStorage<MetaData>.LoadData(file);
                            yield return (file, md);
                            break;
                        }
                    }
                }
            }
        }
        #endregion

        private ILocalizer _localizer;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private IWallpaperControlClient _wallpaperControlClient;
        private IUserSettingsClient _userSettingsClient;
        private List<string> _wallpaperScanFolders;
    }
}
