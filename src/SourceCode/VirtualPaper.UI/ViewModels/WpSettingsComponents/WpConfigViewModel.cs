using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NLog;
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
using VirtualPaper.UI.Views.Utils;
using Windows.Storage;
using WinUI3Localizer;
using WallpaperType = VirtualPaper.Common.WallpaperType;

namespace VirtualPaper.UI.ViewModels.WpSettingsComponents
{
    //public delegate Task LoadSourceDelegate(WallpaperType type, string filePath);

    internal class WpConfigViewModel : ObservableObject
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

        private WpCustomize _wpCustomizePage;
        public WpCustomize WpCustomizePage
        {
            get { return _wpCustomizePage; }
            set { _wpCustomizePage = value; OnPropertyChanged(); }
        }

        public WpConfigViewModel(Func<WallpaperType, string, string, Task> initWebviewContent)
        {
            _localizer = Localizer.Get();
            _wallpaperControlClient = App.Services.GetRequiredService<IWallpaperControlClient>();

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
            InfobarTitle = _localizer.GetLocalizedString("Wpconfig_InfobarTitle");
        }

        internal async Task InitWp(int idx)
        {
            try
            {
                Loading();
                _cancellationTokenSourceForInitWpUI = new CancellationTokenSource();

                var wpBasicData = _wallpaperControlClient.Wallpapers.FirstOrDefault(x => x.Monitor.Index == idx);
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

                        Resolution = data.Resolution,
                        AspectRatio = data.AspectRatio,
                        FileExtension = data.FileExtension,
                        FileSize = data.FileSize,

                        Arguments = [.. data.Arguments],
                    };
                    if (Wallpaper.Arguments.Count < 2)
                    {
                        Wallpaper.Arguments.Add("");
                        Wallpaper.Arguments.Add("");
                    }
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
                InfoBarSeverity = InfoBarSeverity.Error;
                InfobarMsg = _localizer.GetLocalizedString("Wpconfig_InfobarMsg_Err");
                InfoBarIsOpen = true;

                _logger.Error(ex);
            }
            finally
            {
                Loaded();
                _cancellationTokenSourceForInitWpUI?.Dispose();
                _cancellationTokenSourceForInitWpUI = null;
            }
        }

        internal async Task TryDropFileAsync(IReadOnlyList<IStorageItem> items, XamlRoot xamlRoot)
        {
            if (items.Count > 1)
            {
                _ = await new ContentDialog()
                {
                    XamlRoot = xamlRoot,
                    Title = _localizer.GetLocalizedString("Dialog_Title_Prompt"),
                    Content = _localizer.GetLocalizedString("Dialog_Content_TryDropMuchFile"),
                    PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm")
                }.ShowAsync();
                return;
            }

            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    WallpaperType fileType = GetFileTypeFromPath(file.Path);
                    if (fileType == WallpaperType.unknown)
                    {
                        _ = await new ContentDialog()
                        {
                            XamlRoot = xamlRoot,
                            Title = _localizer.GetLocalizedString("Dialog_Title_Prompt"),
                            Content = _localizer.GetLocalizedString("Dialog_Content_TryDropFileErr"),
                            PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm")
                        }.ShowAsync();
                    }
                    else await TryImportFromLocalAsync(file.Path);
                }
            }
        }

        internal async Task TryImportFromLocalAsync(string filePath)
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

                        Arguments = [.. wpData.Arguments],
                    };

                    _cancellationTokenSourceForImport.Token.ThrowIfCancellationRequested();
                    JsonStorage<IMetaData>.StoreData(Path.Combine(wpData.FolderPath, "MetaData.json"), metaData);

                    _cancellationTokenSourceForImport.Token.ThrowIfCancellationRequested();

                    Wallpaper = metaData;

                    InitBasicInfo();
                    InitCustomize(metaData);
                }
            }
            catch (OperationCanceledException)
            {
                OperationCanceled();
            }
            catch (Exception ex)
            {
                ErrOccoured(ex);
            }
            finally
            {
                Loaded();
                _cancellationTokenSourceForImport?.Dispose();
                _cancellationTokenSourceForImport = null;
            }
        }

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
        }

        internal void Cancel()
        {
            _cancellationTokenSourceForImport?.Cancel();
            _cancellationTokenSourceForInitWpUI?.Cancel();
        }

        internal async Task RestoreAsync(IMonitor monitor)
        {
            Wallpaper = null;
            WpCustomizePage = null;

            await InitWp(monitor.Index);

            //return Task.CompletedTask;
        }

        internal async void Close(IMonitor monitor)
        {
            Wallpaper = null;
            WpCustomizePage = null;

            await _wallpaperControlClient.CloseWallpaperAsync(monitor);
            await _onUpdateSource?.Invoke(WallpaperType.unknown, null, null);
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
                ShowErr(xamlRoot);
                _logger.Error(ex);
            }
        }

        private WallpaperType GetFileTypeFromPath(string filePath)
        {
            string extension = Path.GetExtension(filePath)?.ToLowerInvariant();

            return extension switch
            {
                //".html" or ".htm" => WallpaperType.web,
                ".gif" => WallpaperType.gif,
                ".jpg" or ".jpeg" or ".png" or ".bmp" => WallpaperType.picture,
                ".mp4" or ".webm" => WallpaperType.video,
                _ => WallpaperType.unknown,
            };
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

            WpCustomizePage = new(Wallpaper, DoubleValueChanged, BoolValueChanged, StringValueChanged);
        }

        private void InitCustomize(IMetaData metaData)
        {
            if (metaData == null) return;

            WpCustomizePage = new(metaData, DoubleValueChanged, BoolValueChanged, StringValueChanged);
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
        #endregion

        private ILocalizer _localizer;
        private IWallpaperControlClient _wallpaperControlClient;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private CancellationTokenSource _cancellationTokenSourceForImport;
        private CancellationTokenSource _cancellationTokenSourceForInitWpUI;

        internal Func<WallpaperType, string, string, Task> _onUpdateSource;
        internal Action<bool> _onIsLoading;
    }
}
