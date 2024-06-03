using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Models;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.UI.UserControls;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.Views.Utils;
using Windows.Storage;
using WinUI3Localizer;
using WallpaperType = VirtualPaper.Common.WallpaperType;

namespace VirtualPaper.UI.ViewModels.WpSettingsComponents
{
    public class WpConfigViewModel : ObservableObject
    {
        public event EventHandler<DoubleValueChangedEventArgs> DoubleValueChanged;
        public event EventHandler<BoolValueChangedEventArgs> BoolValueChanged;
        public event EventHandler<StringValueChangedEventArgs> StringValueChanged;

        public string TextUpdateWallpaper { get; set; } = string.Empty;
        public string TextWpConfigCustomize { get; set; } = string.Empty;
        public string TextResolution { get; set; } = string.Empty;
        public string TextAspectRatio { get; set; } = string.Empty;
        public string TextType { get; set; } = string.Empty;
        public string TextFileExtension { get; set; } = string.Empty;
        public string TextFileSize { get; set; } = string.Empty;
        public string TextDetailedInfo { get; set; } = string.Empty;

        private IMetaData _wallpaper;
        public IMetaData Wallpaper
        {
            get => _wallpaper;
            set { _wallpaper = value; OnPropertyChanged(); }
        }

        private WpCustomize _wpCustomizePage;
        public WpCustomize WpCustomizePage
        {
            get { return _wpCustomizePage; }
            set { _wpCustomizePage = value; OnPropertyChanged(); }
        }

        public WpConfigViewModel(Func<WallpaperType, string, string, Task> initWebviewContent)
        {
            _localizer = Localizer.Get();
            _userSettingsClient = App.Services.GetRequiredService<IUserSettingsClient>();
            _wallpaperControlClient = App.Services.GetRequiredService<IWallpaperControlClient>();
            _wpSettingsViewModel = App.Services.GetRequiredService<WpSettingsViewModel>();

            _onIsLoading = App.Services.GetRequiredService<WpSettingsViewModel>().IsLoading;
            _onUpdateSource = initWebviewContent;

            InitText();
        }

        private void InitText()
        {
            TextUpdateWallpaper = _localizer.GetLocalizedString("WpConfigViewMdoel_TextUpdateWallpaper");
            TextWpConfigCustomize = _localizer.GetLocalizedString("WpConfigViewMdoel_TextWpConfigCustomize");

            TextResolution = _localizer.GetLocalizedString("WpConfigViewMdoel_TextResolution");
            TextAspectRatio = _localizer.GetLocalizedString("WpConfigViewMdoel_TextAspectRatio");
            TextType = _localizer.GetLocalizedString("WpConfigViewMdoel_TextType");
            TextFileExtension = _localizer.GetLocalizedString("WpConfigViewMdoel_TextFileExtension");
            TextFileSize = _localizer.GetLocalizedString("WpConfigViewMdoel_TextFileSize");
            TextDetailedInfo = _localizer.GetLocalizedString("WpConfigViewMdoel_TextDetailedInfo");
        }

        public async Task InitWp(string content)
        {
            try
            {
                App.Services.GetRequiredService<WpSettingsViewModel>().WpConfigViewModel = this;

                Loading();
                _cancellationTokenSourceForInitWpUI = new CancellationTokenSource();

                WallpaperBasicData wpBasicData;
                if (content == "Expand" || content == "Duplicate")
                {
                    _monitor = App.Services.GetRequiredService<WpSettingsViewModel>().Monitors[0];
                    wpBasicData = _wallpaperControlClient.Wallpapers.FirstOrDefault();
                }
                else
                {
                    _monitor = App.Services.GetRequiredService<WpSettingsViewModel>().Monitors[int.Parse(content) - 1];
                    wpBasicData = _wallpaperControlClient.Wallpapers.FirstOrDefault(x => x.Monitor.Content == content);
                }

                WpMetaData data = wpBasicData == null ? null : await _wallpaperControlClient.GetWallpaperAsync(wpBasicData.FolderPath);

                if (data != null)
                {
                    Wallpaper = new MetaData()
                    {
                        VirtualPaperUid = data.VirtualPaperUid,
                        AppInfo = new()
                        {
                            AppName = data.AppInfo.AppName,
                            AppVersion = data.AppInfo.AppVersion,
                        },
                        Title = data.Title,
                        Desc = data.Desc,
                        Authors = data.Authors,
                        PublishDate = data.PublishDate,
                        Type = (WallpaperType)data.Type,
                        Partition = data.Partition,
                        Tags = data.Tags,
                        FolderPath = data.FolderPath,
                        FilePath = data.FilePath,
                        ThumbnailPath = data.ThumbnailPath,
                        WpCustomizePath = data.WpCustomizePath,
                        WpCustomizePathTmp = data.WpCustomizePathTmp,
                        WpCustomizePathUsing = data.WpCustomizePathUsing,

                        Resolution = data.Resolution,
                        AspectRatio = data.AspectRatio,
                        FileExtension = data.FileExtension,
                        FileSize = data.FileSize,
                    };

                    File.Copy(Wallpaper.WpCustomizePathUsing, Wallpaper.WpCustomizePathTmp, true);
                }
                else
                {
                    Wallpaper = null;
                    WpCustomizePage = null;
                }

                InitBasicInfo();
                InitCustomize();
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
            finally
            {
                Loaded();
                if (App._isNeedReslease)
                {
                    App._semaphoreSlimForLib.Release();
                    App._isNeedReslease = false;
                }
                _cancellationTokenSourceForInitWpUI?.Dispose();
                _cancellationTokenSourceForInitWpUI = null;
            }
        }

        internal async Task TryDropFileAsync(IReadOnlyList<IStorageItem> items, XamlRoot xamlRoot)
        {
            Loading();

            var res = WallpaperUtil.TrytoDropFile(items);
            bool statu = res.Item1;
            string content = res.Item2;

            if (!statu)
            {
                Loaded();
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

        internal async Task TryImportFromLocalAsync(string filePath, XamlRoot xamlRoot)
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
                        type,
                        _cancellationTokenSourceForImport.Token);

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

                    JsonStorage<IMetaData>.StoreData(Path.Combine(wpData.FolderPath, "MetaData.json"), metaData);

                    Wallpaper = metaData;

                    InitBasicInfo();
                    InitCustomize(metaData);
                }
                else
                {
                    _ = await new ContentDialog()
                    {
                        XamlRoot = xamlRoot,
                        Title = _localizer.GetLocalizedString("Dialog_Title_Prompt"),
                        Content = filePath + "\nInVailid",
                        PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm")
                    }.ShowAsync();
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

        internal void TryImportFromLocal(IMetaData metaData)
        {
            try
            {
                Loading();

                File.Copy(metaData.WpCustomizePath, metaData.WpCustomizePathUsing, true);
                File.Copy(metaData.WpCustomizePath, metaData.WpCustomizePathTmp, true);
                Wallpaper = metaData;

                InitBasicInfo();
                InitCustomize(metaData);
                Wallpaper.State = MetaData.RunningState.ready;
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
            finally
            {
                Loaded();
            }
        }

        //internal void Cancel()
        //{
        //    _cancellationTokenSourceForImport?.Cancel();
        //    _cancellationTokenSourceForInitWpUI?.Cancel();
        //}

        internal async Task RestoreAsync(IMonitor monitor)
        {
            Wallpaper = null;
            WpCustomizePage = null;

            await InitWp(monitor.Content);
        }

        internal async void Close(IMonitor monitor)
        {
            Wallpaper = null;
            WpCustomizePage = null;

            await _wallpaperControlClient.CloseWallpaperAsync(monitor);
            await _onUpdateSource?.Invoke(WallpaperType.unknown, null, null);
        }

        internal async Task PreviewWallpaperAsync()
        {
            if (Wallpaper == null || Wallpaper.FolderPath == null || Wallpaper.FolderPath.Length == 0) return;

            await _wallpaperControlClient.PreviewWallpaperAsync(Wallpaper, false);
        }

        internal async Task ModifyPreviewAsync(string controlName, string propertyName, string val)
        {
            await _wallpaperControlClient.ModifyPreviewAsync(controlName, propertyName, val);
        }

        internal void Apply()
        {
            WpCustomizePage?.UpdatePropertyFile(true);
        }

        #region helpers
        internal async Task ShowDetailedInfoPop(XamlRoot xamlRoot)
        {
            try
            {
                if (Wallpaper == null) return;

                var metaData = JsonStorage<MetaData>.LoadData(Path.Combine(Wallpaper.FolderPath, "MetaData.json"));
                if (metaData == null)
                {
                    _ = await new ContentDialog()
                    {
                        XamlRoot = xamlRoot,
                        Title = _localizer.GetLocalizedString("Dialog_Title_Prompt"),
                        Content = _localizer.GetLocalizedString("Dialog_Content_ReadMetaDataFialed"),
                        PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm")
                    }.ShowAsync();
                    return;
                }

                Wallpaper.Title = metaData.Title;
                Wallpaper.Desc = metaData.Desc;
                Wallpaper.Tags = metaData.Tags;

                _ = await new ContentDialog()
                {
                    XamlRoot = xamlRoot,
                    Title = _localizer.GetLocalizedString("Dialog_Title_About"),
                    Content = new DetailedInfoView()
                    {
                        DataContext = new DetailedInfoViewModel(Wallpaper),
                    },
                    PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                    DefaultButton = ContentDialogButton.Primary,
                }.ShowAsync();
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        private void Loading()
        {
            _onIsLoading?.Invoke(true);
        }

        private void Loaded()
        {
            _onIsLoading?.Invoke(false);
        }

        private async void InitBasicInfo()
        {
            if (Wallpaper == null) await _onUpdateSource?.Invoke(WallpaperType.unknown, null, null);
            else await _onUpdateSource?.Invoke(Wallpaper.Type, Wallpaper.FilePath, Wallpaper.WpCustomizePathUsing);
        }

        private void InitCustomize()
        {
            if (Wallpaper == null) return;

            WpCustomizePage = new(_monitor, Wallpaper, DoubleValueChanged, BoolValueChanged, StringValueChanged);
        }

        private void InitCustomize(IMetaData metaData)
        {
            if (metaData == null) return;

            WpCustomizePage = new(_monitor, metaData, DoubleValueChanged, BoolValueChanged, StringValueChanged);
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

        internal bool IsLegal()
        {
            if (Wallpaper == null || Wallpaper.FolderPath == string.Empty) return false;
            return true;
        }

        internal string InitUid()
        {
            string folderName = WallpaperUtil.InitUid(Wallpaper);

            return folderName;
        }

        internal void UpdateMetaData(string folderName)
        {
            string tagetFolder = Path.Combine(
                    _userSettingsClient.Settings.WallpaperDir,
                    Constants.CommonPartialPaths.WallpaperInstallDir,
                    folderName);
            if (!Directory.Exists(tagetFolder))
            {
                FileUtil.DirectoryCopy(
                    Wallpaper.FolderPath,
                    tagetFolder,
                    true);
            }
            string oldFolderPath = Wallpaper.FolderPath;

            if (oldFolderPath != tagetFolder)
            {
                Wallpaper.FolderPath = Wallpaper.FolderPath.Replace(oldFolderPath, tagetFolder);
                Wallpaper.ThumbnailPath = Wallpaper.ThumbnailPath.Replace(oldFolderPath, tagetFolder);
                Wallpaper.WpCustomizePath = Wallpaper.WpCustomizePath.Replace(oldFolderPath, tagetFolder);
                Wallpaper.WpCustomizePathUsing = Wallpaper.WpCustomizePathUsing.Replace(oldFolderPath, tagetFolder);
                Wallpaper.WpCustomizePathTmp = Wallpaper.WpCustomizePathTmp.Replace(oldFolderPath, tagetFolder);
            }

            JsonStorage<IMetaData>.StoreData(Path.Combine(Wallpaper.FolderPath, "MetaData.json"), Wallpaper);
        }
        #endregion

        private ILocalizer _localizer;
        private IMonitor _monitor;
        private IUserSettingsClient _userSettingsClient;
        private IWallpaperControlClient _wallpaperControlClient;
        private CancellationTokenSource _cancellationTokenSourceForImport;
        private CancellationTokenSource _cancellationTokenSourceForInitWpUI;
        private Action<bool> _onIsLoading;
        private Func<WallpaperType, string, string, Task> _onUpdateSource;
        private WpSettingsViewModel _wpSettingsViewModel;
    }
}
