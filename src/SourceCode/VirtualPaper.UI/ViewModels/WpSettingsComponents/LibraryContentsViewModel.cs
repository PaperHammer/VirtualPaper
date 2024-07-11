using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UI.UserControls;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels.AppSettings;
using VirtualPaper.UI.ViewModels.Utils;
using VirtualPaper.UI.Views.Utils;
using Windows.Storage;
using Windows.System.UserProfile;
using WinUI3Localizer;
using static VirtualPaper.UI.Services.Interfaces.IDialogService;

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
        public string MenuFlyout_Text_ApplyToLockBG { get; set; } = string.Empty;
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
            IDialogService dialogService,
            IUserSettingsClient userSettingsClient,
            IWallpaperControlClient wallpaperControlClient,
            GeneralSettingViewModel generalSettingViewModel)
        {
            _dialogService = dialogService;
            _userSettingsClient = userSettingsClient;
            _wallpaperControlClient = wallpaperControlClient;

            generalSettingViewModel.WallpaperDirChanged += (s, e) => WallpaperDirectoryUpdate(e);

            _wpSettingsViewModel = App.Services.GetRequiredService<WpSettingsViewModel>();
            _onLoading = _wpSettingsViewModel.Loading;
            _onLoaded = _wpSettingsViewModel.Loaded;
            _onUpdatingValue = _wpSettingsViewModel.UpdateProgressbarValue;

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
            MenuFlyout_Text_ApplyToLockBG = _localizer.GetLocalizedString("MenuFlyout_Text_ApplyToLockBG");
            MenuFlyout_Text_ShowOnDisk = _localizer.GetLocalizedString("MenuFlyout_Text_ShowOnDisk");
            MenuFlyout_Text_Export = _localizer.GetLocalizedString("MenuFlyout_Text_Export");
            MenuFlyout_Text_Delete = _localizer.GetLocalizedString("MenuFlyout_Text_Delete");
        }

        private void InitContents()
        {
            try
            {
                Loading(false, false, []);
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
                Loaded([]);
            }
        }

        internal async Task DetailedInfoAsync(IMetaData metaData)
        {
            try
            {
                bool isExists = await CheckFileExistsAsync(metaData);
                if (!isExists) return;

                var detailedInfoViewModel = new DetailedInfoViewModel(metaData, false, true, false);
                var detailedInfoView = new DetailedInfoView(detailedInfoViewModel);
                var dialogRes = await _dialogService.ShowDialogWithoutTitleAsync(
                    detailedInfoView,
                    _localizer.GetLocalizedString("Dialog_Btn_Confirm"));

                if (dialogRes != DialogResult.Primary) return;
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        internal async Task EditInfoAsync(IMetaData metaData)
        {
            try
            {
                bool isExists = await CheckFileExistsAsync(metaData);
                if (!isExists) return;

                var detailedInfoViewModel = new DetailedInfoViewModel(metaData, true, true, false);
                var detailedInfoView = new DetailedInfoView(detailedInfoViewModel);
                var dialogRes = await _dialogService.ShowDialogWithoutTitleAsync(
                    detailedInfoView,
                    _localizer.GetLocalizedString("Dialog_Btn_Confirm"));

                if (dialogRes != DialogResult.Primary) return;

                metaData.Title = detailedInfoViewModel.Title;
                metaData.Desc = detailedInfoViewModel.Desc;
                JsonStorage<IMetaData>.StoreData(Path.Combine(metaData.FolderPath, "MetaData.json"), metaData);
                UpdateLibWallpaper(metaData);
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        internal async Task PreviewAsync(IMetaData metaData)
        {
            bool isExists = await CheckFileExistsAsync(metaData);
            if (!isExists) return;

            await _wallpaperControlClient.PreviewWallpaperAsync(metaData, true);
        }

        internal async Task ImportAsync(IMetaData metaData)
        {
            try
            {
                bool isExists = await CheckFileExistsAsync(metaData);
                if (!isExists) return;

                Loading(false, false, []);

                App.IsNeedReslease = true;
                WpSettingsViewModel wpSettingsViewModel = App.Services.GetRequiredService<WpSettingsViewModel>();
                wpSettingsViewModel.ResetNavSeletedItem();
                await App.SemaphoreSlimForLib.WaitAsync();
                WpConfigViewModel wpConfigViewModel = wpSettingsViewModel.WpConfigViewModel;
                wpConfigViewModel.TryImportFromLib(metaData);
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
            finally
            {
                Loaded([]);
            }
        }

        internal async Task ApplyAsync(IMetaData metaData)
        {
            try
            {
                bool isExists = await CheckFileExistsAsync(metaData);
                if (!isExists) return;

                Loading(true, false, []);

                App.IsNeedReslease = true;
                WpSettingsViewModel wpSettingsViewModel = App.Services.GetRequiredService<WpSettingsViewModel>();
                wpSettingsViewModel.ResetNavSeletedItem();
                await App.SemaphoreSlimForLib.WaitAsync();
                WpConfigViewModel wpConfigViewModel = wpSettingsViewModel.WpConfigViewModel;
                wpConfigViewModel.TryImportFromLib(metaData);
                await wpSettingsViewModel.ApplyAsync();
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
            finally
            {
                Loaded([]);
            }
        }

        internal async Task ApplyToLockBGAsync(IMetaData metaData)
        {
            try
            {
                bool isExists = await CheckFileExistsAsync(metaData);
                if (!isExists) return;

                if (metaData.Type != WallpaperType.picture && metaData.Type != WallpaperType.gif)
                {
                    await _dialogService.ShowDialogAsync(
                        _localizer.GetLocalizedString("Dialog_Content_OnlyPictureAndGif")
                        , _localizer.GetLocalizedString("Dialog_Title_Prompt")
                        , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
                    return;
                }

                StorageFile storageFile = await StorageFile.GetFileFromPathAsync(metaData.FilePath);
                await LockScreen.SetImageFileAsync(storageFile);

                _wpSettingsViewModel.ShowMessge("Msg_LockScreenSet_Successful", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        internal async Task DeleteAsync(IMetaData rightTrappedItem)
        {
            try
            {
                var dialogRes = await _dialogService.ShowDialogAsync(
                    _localizer.GetLocalizedString("Dialog_Content_LibraryDelete")
                    , _localizer.GetLocalizedString("Dialog_Title_Prompt")
                    , _localizer.GetLocalizedString("Dialog_Btn_Confirm")
                    , _localizer.GetLocalizedString("Dialog_Btn_Cancel"));
                if (dialogRes != DialogResult.Primary) return;

                bool isUsing = false;
                await _userSettingsClient.LoadAsync<List<IWallpaperLayout>>();
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
                    await _dialogService.ShowDialogAsync(
                        _localizer.GetLocalizedString("Dialog_Content_WpIsUsing")
                        , _localizer.GetLocalizedString("Dialog_Title_Prompt")
                        , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));

                    return;
                }

                string uid = rightTrappedItem.VirtualPaperUid;
                _uid2idx.Remove(uid, out _);
                LibraryWallpapers.Remove(rightTrappedItem);
                DirectoryInfo di = new(rightTrappedItem.FolderPath);
                di.Delete(true);
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
                if (wallpaper == null) return;

                UpdateLibWallpaper(wallpaper);
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        private void UpdateLibWallpaper(IMetaData wallpaper)
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

        internal async Task TryDropFileAsync(IReadOnlyList<IStorageItem> items)
        {
            try
            {
                _ctsImport = new CancellationTokenSource();
                Loading(true, true, [_ctsImport]);
                List<string> filePaths = await WallpaperUtil.ImportMultipleFileAsync(items);
                await TryImportFromLocalAsync(filePaths);
            }
            finally
            {
                Loaded([_ctsImport]);
            }
        }

        private async Task TryImportFromLocalAsync(List<string> filePaths)
        {
            try
            {
                int finishedCnt = 0;
                _onUpdatingValue?.Invoke(0, filePaths.Count);

                foreach (string filePath in filePaths)
                {
                    try
                    {
                        WallpaperType type;
                        if ((type = FileFilter.GetFileType(filePath)) != (WallpaperType)(-1))
                        {
                            var wpData = await _wallpaperControlClient.CreateWallpaperAsync(
                                Constants.CommonPaths.TempDir,
                                filePath,
                                type,
                                _ctsImport.Token);

                            IMetaData metaData = new MetaData()
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

                            var detailedInfoViewModel = new DetailedInfoViewModel(metaData, true, false, false);
                            var dialogRes = await _dialogService.ShowDialogWithoutTitleAsync(
                                new DetailedInfoView(detailedInfoViewModel)
                                , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
                            if (dialogRes != DialogResult.Primary) return;

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
                        else
                        {
                            await _dialogService.ShowDialogAsync(
                               $"\"{filePath}\"\n" + _localizer.GetLocalizedString("Dialog_Content_ImportFileFailed")
                               , _localizer.GetLocalizedString("Dialog_Title_Prompt")
                               , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
                        }

                        _onUpdatingValue?.Invoke(++finishedCnt, filePaths.Count);

                    }
                    catch (Exception ex)
                    {
                        _wpSettingsViewModel.ErrOccoured(ex);
                    }

                    if (_ctsImport.IsCancellationRequested)
                        throw new OperationCanceledException();
                }
            }
            catch (OperationCanceledException)
            {
                _wpSettingsViewModel.OperationCanceled();
            }
        }

        internal async void ShowErr()
        {
            await _dialogService.ShowDialogAsync(
                _localizer.GetLocalizedString("Dialog_Title_LibraryContentErr")
                , _localizer.GetLocalizedString("Dialog_Title_Prompt")
                , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
        }

        private void Loading(bool cancelEnable, bool progressbarEnable, CancellationTokenSource[] cts)
        {
            _onLoading?.Invoke(cancelEnable, progressbarEnable, cts);
        }

        private void Loaded(CancellationTokenSource[] cts)
        {
            foreach (var item in cts)
            {
                item?.Dispose();
            }
            _onLoaded?.Invoke();
        }

        private void InitCustomize(IMetaData metaData)
        {
            if (metaData == null) return;

            _ = new WpCustomize(metaData);
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

        private async Task<bool> CheckFileExistsAsync(IMetaData metaData)
        {
            if (metaData == null || !File.Exists(metaData.FilePath))
            {
                await _dialogService.ShowDialogAsync(
                    _localizer.GetLocalizedString("Dialog_Content_FileNotExists")
                    , _localizer.GetLocalizedString("Dialog_Title_Prompt")
                    , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
                return false;
            }
            return true;
        }

        private ILocalizer _localizer;
        private IDialogService _dialogService;
        private IWallpaperControlClient _wallpaperControlClient;
        private IUserSettingsClient _userSettingsClient;
        private List<string> _wallpaperScanFolders;
        private Action<bool, bool, CancellationTokenSource[]> _onLoading;
        private Action _onLoaded;
        private Action<int, int> _onUpdatingValue;
        private CancellationTokenSource _ctsImport;
        private WpSettingsViewModel _wpSettingsViewModel;
        private ConcurrentDictionary<string, int> _uid2idx = [];
    }
}
