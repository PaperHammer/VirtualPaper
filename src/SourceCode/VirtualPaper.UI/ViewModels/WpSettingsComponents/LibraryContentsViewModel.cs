using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.UI.UserControls;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels.AppSettings;
using VirtualPaper.UI.Views.Utils;
using VirtualPaper.UI.Views.WpSettingsComponents;
using Windows.Storage;
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
                Loading();
                LibraryWallpapers.Clear();

                foreach (var item in ScanWallpaperFolders(_wallpaperScanFolders))
                {
                    _uid2idx[item.Item3.VirtualPaperUid] = item.Item1;
                    LibraryWallpapers.Add(item.Item3);
                }
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
                LibraryWallpapers.Clear();
            }
            finally
            {
                Loaded();
            }
        }

        internal async Task Preview(string folderPath, XamlRoot xamlRoot)
        {
            try
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

                await _wallpaperControlClient.PreviewWallpaperAsync(metaData, true);
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        internal async void DetailedInfo(IMetaData rightTrappedItem, XamlRoot xamlRoot)
        {
            try
            {
                var detailedInfoViewModel = new DetailedInfoViewModel(rightTrappedItem, false);
                await ShowDetailedInfoPop(detailedInfoViewModel, xamlRoot);
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        internal async void EditInfo(IMetaData rightTrappedItem, XamlRoot xamlRoot)
        {
            try
            {
                var detailedInfoViewModel = new DetailedInfoViewModel(rightTrappedItem, true);
                bool res = await ShowDetailedInfoPop(detailedInfoViewModel, xamlRoot);
                if (!res) return;

                rightTrappedItem.Title = detailedInfoViewModel.Title;
                rightTrappedItem.Desc = detailedInfoViewModel.Desc;
                rightTrappedItem.Tags = detailedInfoViewModel.Tags;
                JsonStorage<IMetaData>.StoreData(Path.Combine(rightTrappedItem.FolderPath, "MetaData.json"), rightTrappedItem);
                UpdateWallpaper(rightTrappedItem);
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        internal async Task PreviewAsync(IMetaData metaData)
        {
            await _wallpaperControlClient.PreviewWallpaperAsync(metaData, true);
        }

        internal async void Import(IMetaData metaData)
        {
            try
            {
                App._isNeedReslease = true;
                WpSettingsViewModel wpSettingsViewModel = App.Services.GetRequiredService<WpSettingsViewModel>();
                wpSettingsViewModel.ResetNavSeletedItem();
                await App._semaphoreSlimForLib.WaitAsync();
                WpConfigViewModel wpConfigViewModel = wpSettingsViewModel.WpConfigViewModel;
                //var tup = await GetViewModelsAsync();
                wpConfigViewModel.TryImportFromLocal(metaData);
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        internal async Task ApplyAsync(IMetaData metaData, XamlRoot xamlRoot)
        {
            try
            {
                App._isNeedReslease = true;
                WpSettingsViewModel wpSettingsViewModel = App.Services.GetRequiredService<WpSettingsViewModel>();
                wpSettingsViewModel.ResetNavSeletedItem();
                await App._semaphoreSlimForLib.WaitAsync();
                WpConfigViewModel wpConfigViewModel = wpSettingsViewModel.WpConfigViewModel;
                wpConfigViewModel.TryImportFromLocal(metaData);
                await wpSettingsViewModel.ApplyAsync(xamlRoot);
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        internal async Task DeleteAsync(IMetaData rightTrappedItem, XamlRoot xamlRoot)
        {
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

                bool isUsing = false;
                foreach (var wl in _userSettingsClient.WallpaperLayouts)
                {
                    if (wl.FolderPath == rightTrappedItem.FolderPath)
                    {
                        isUsing = true;
                        break;
                    }
                }
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

                DirectoryInfo di = new(rightTrappedItem.FolderPath);
                di.Delete(true);
                string uid = rightTrappedItem.VirtualPaperUid;
                _uid2idx.Remove(uid, out _);
                LibraryWallpapers.Remove(rightTrappedItem);
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        internal void AddToLibrary(IMetaData wallpaper)
        {
            try
            {
                if (wallpaper == null)
                    return;
                UpdateWallpaper(wallpaper);
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        private void UpdateWallpaper(IMetaData wallpaper)
        {
            if (_uid2idx.TryGetValue(wallpaper.VirtualPaperUid, out int idx))
            {
                LibraryWallpapers[idx] = wallpaper;
            }
            else
            {
                _uid2idx[wallpaper.VirtualPaperUid] = LibraryWallpapers.Count;
                LibraryWallpapers.Add(wallpaper);
            }
        }

        internal async Task TryDropFileAsync(IReadOnlyList<IStorageItem> items, XamlRoot xamlRoot)
        {
            try
            {
                var res = WallpaperUtil.TrytoDropFile(items);
                bool statu = res.Item1;
                string content = res.Item2;

                if (!statu)
                {
                    _ = await new ContentDialog()
                    {
                        XamlRoot = xamlRoot,
                        Title = _localizer.GetLocalizedString("Dialog_Title_Prompt"),
                        Content = content + "\nInVailid",
                        PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm")
                    }.ShowAsync();
                }
                else
                {
                    await TryImportFromLocalAsync(content, xamlRoot);
                }
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        private async Task TryImportFromLocalAsync(string filePath, XamlRoot xamlRoot)
        {
            try
            {
                Loading();
                _cancellationTokenSourceForImport = new CancellationTokenSource();

                WallpaperType type;
                if ((type = FileFilter.GetFileType(filePath)) != (WallpaperType)(-1))
                {
                    var wpData = await _wallpaperControlClient.CreateWallpaperAsync(
                        Constants.CommonPaths.TempDir,
                        filePath,
                        type);

                    MetaData metaData = new()
                    {
                        Type = (WallpaperType)wpData.Type,
                        FolderPath = wpData.FolderPath,
                        FilePath = wpData.FilePath,
                        ThumbnailPath = wpData.ThumbnailPath,
                        WpCustomizePath = wpData.WpCustomizePath,
                        State = MetaData.RunningState.ready,
                        IsSubscribed = true,

                        Resolution = wpData.Resolution,
                        AspectRatio = wpData.AspectRatio,
                        FileExtension = wpData.FileExtension,
                        FileSize = wpData.FileSize,
                    };

                    string folderName = WallpaperUtil.InitUid(metaData);

                    var detailedInfoViewModel = new DetailedInfoViewModel(metaData, true);
                    var dialogResult = await new ContentDialog()
                    {
                        XamlRoot = xamlRoot,
                        Title = _localizer.GetLocalizedString("Dialog_Title_Edit"),
                        Content = new DetailedInfoView() { DataContext = detailedInfoViewModel },
                        PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                        DefaultButton = ContentDialogButton.Primary,
                    }.ShowAsync();
                    if (dialogResult != ContentDialogResult.Primary) return;

                    InitCustomize(metaData);

                    string tagetFolder = Path.Combine(
                        _userSettingsClient.Settings.WallpaperDir,
                        Constants.CommonPartialPaths.WallpaperInstallDir,
                        folderName);
                    if (!Directory.Exists(tagetFolder))
                    {
                        FileUtil.DirectoryCopy(
                            metaData.FolderPath,
                            tagetFolder,
                            true);
                    }
                    string oldFolderPath = metaData.FolderPath;

                    if (oldFolderPath != tagetFolder)
                    {
                        metaData.FolderPath = metaData.FolderPath.Replace(oldFolderPath, tagetFolder);
                        metaData.ThumbnailPath = metaData.ThumbnailPath.Replace(oldFolderPath, tagetFolder);
                        metaData.WpCustomizePath = metaData.WpCustomizePath.Replace(oldFolderPath, tagetFolder);
                        metaData.WpCustomizePathUsing = metaData.WpCustomizePathUsing.Replace(oldFolderPath, tagetFolder);
                        metaData.WpCustomizePathTmp = metaData.WpCustomizePathTmp.Replace(oldFolderPath, tagetFolder);
                    }

                    metaData.Title = detailedInfoViewModel.Title;
                    metaData.Desc = detailedInfoViewModel.Desc;
                    metaData.Tags = detailedInfoViewModel.Tags;
                    JsonStorage<IMetaData>.StoreData(Path.Combine(metaData.FolderPath, "MetaData.json"), metaData);

                    AddToLibrary(metaData);
                }
            }
            catch (OperationCanceledException)
            {
                _wpSettingsViewModel.OperationCanceled();
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
            finally
            {
                Loaded();
                _cancellationTokenSourceForImport?.Dispose();
                _cancellationTokenSourceForImport = null;
            }
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

        #region utils
        private void Loading()
        {
            Check();
            _onIsLoading?.Invoke(true);
        }

        private void Loaded()
        {
            Check();
            _onIsLoading?.Invoke(false);
        }

        private void Check()
        {
            if (_onIsLoading == null)
            {
                _wpSettingsViewModel = App.Services.GetRequiredService<WpSettingsViewModel>();
                _onIsLoading = _wpSettingsViewModel.IsLoading;
            }
        }

        private void InitCustomize(IMetaData metaData)
        {
            if (metaData == null) return;

            _ = new WpCustomize(metaData);
        }

        private async Task<bool> ShowDetailedInfoPop(
            //bool fromLocal,
            //IMetaData rightTrappedItem,
            DetailedInfoViewModel detailedInfoViewModel,
            XamlRoot xamlRoot)
        {
            //if (fromLocal)
            //    rightTrappedItem = JsonStorage<MetaData>.LoadData(Path.Combine(rightTrappedItem.FolderPath, "MetaData.json"));

            var dialogResult = await new ContentDialog()
            {
                XamlRoot = xamlRoot,
                Title = _localizer.GetLocalizedString("Dialog_Title_About"),
                Content = new DetailedInfoView() { DataContext = detailedInfoViewModel },
                PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                DefaultButton = ContentDialogButton.Primary,
            }.ShowAsync();

            return dialogResult == ContentDialogResult.Primary;
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
                string filePath = item.Item2;
                var md = item.Item3;

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
        private IEnumerable<(int, string, MetaData)> ScanWallpaperFolders(List<string> folderPaths)
        {
            int idx = 0;
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
                            yield return (idx++, file, md);
                            break;
                        }
                    }
                }
            }
        }
        #endregion

        private ILocalizer _localizer;
        private IWallpaperControlClient _wallpaperControlClient;
        private IUserSettingsClient _userSettingsClient;
        private List<string> _wallpaperScanFolders;
        private Action<bool> _onIsLoading;
        private CancellationTokenSource _cancellationTokenSourceForImport;
        private WpSettingsViewModel _wpSettingsViewModel;
        private ConcurrentDictionary<string, int> _uid2idx = [];
    }
}
