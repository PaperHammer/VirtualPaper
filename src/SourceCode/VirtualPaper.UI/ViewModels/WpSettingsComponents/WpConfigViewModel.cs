using Microsoft.Extensions.DependencyInjection;
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
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UI.UserControls;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels.Utils;
using VirtualPaper.UI.Views.Utils;
using Windows.Storage;
using WinUI3Localizer;
using WallpaperType = VirtualPaper.Common.WallpaperType;

namespace VirtualPaper.UI.ViewModels.WpSettingsComponents
{
    public class WpConfigViewModel : ObservableObject
    {
        public event EventHandler<DoubleValueChangedEventArgs> DoubleValueChanged;
        public event EventHandler<IntValueChangedEventArgs> IntValueChanged;
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

        public WpConfigViewModel(
            Func<WallpaperType, string, string, Task> initWebviewContent)
        {
            _dialogService = App.Services.GetRequiredService<IDialogService>();

            _localizer = Localizer.Get();
            _userSettingsClient = App.Services.GetRequiredService<IUserSettingsClient>();
            _wallpaperControlClient = App.Services.GetRequiredService<IWallpaperControlClient>();
            _wpSettingsViewModel = App.Services.GetRequiredService<WpSettingsViewModel>();

            _onUpdateSource = initWebviewContent;
            //_onImporting = _wpSettingsViewModel.UpdateProgressbarValue;
            _onLoading = _wpSettingsViewModel.Loading;
            _onLoaded = _wpSettingsViewModel.Loaded;

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
                Loading(false, false, []);
                App.Services.GetRequiredService<WpSettingsViewModel>().WpConfigViewModel = this;

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
                Loaded([]);
                if (App.IsNeedReslease)
                {
                    App.SemaphoreSlimForLib.Release();
                    App.IsNeedReslease = false;
                }
            }
        }

        internal async Task DropFileAsync(IReadOnlyList<IStorageItem> items)
        {
            try
            {
                string filePath = WallpaperUtil.ImportSingleFile(items);

                if (filePath == null)
                {
                    await _dialogService.ShowDialogAsync(
                       $"\"{filePath}\"\n" + _localizer.GetLocalizedString("Dialog_Content_ImportFileFailed")
                       , _localizer.GetLocalizedString("Dialog_Title_Prompt")
                       , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
                    return;
                }

                await TryImportFromLocalAsync(filePath);
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        internal async Task TryImportFromLocalAsync(string filePath)
        {
            try
            {
                _ctsImport = new();
                Loading(true, false, [_ctsImport]);

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

                    JsonStorage<IMetaData>.StoreData(Path.Combine(wpData.FolderPath, "MetaData.json"), metaData);

                    Wallpaper = metaData;

                    InitBasicInfo();
                    InitCustomize(metaData);
                }
                else
                {
                    await _dialogService.ShowDialogAsync(
                        $"\"{filePath}\"\n" + _localizer.GetLocalizedString("Dialog_Content_ImportFileFailed")
                        , _localizer.GetLocalizedString("Dialog_Title_Prompt")
                        , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
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
                Loaded([_ctsImport]);
            }
        }

        internal void TryImportFromLib(IMetaData metaData)
        {
            try
            {
                Loading(false, false, []);

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
                Loaded([]);
            }
        }

        internal void Cancel()
        {
            _ctsImport?.Cancel();
        }

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
            bool isExists = await CheckFileExistsAsync(Wallpaper);
            if (!isExists) return;

            await _wallpaperControlClient.PreviewWallpaperAsync(Wallpaper, false);
        }

        internal async Task ModifyPreviewAsync(string controlName, string propertyName, string val)
        {
            await _wallpaperControlClient.ModifyPreviewAsync(controlName, propertyName, val);
        }

        internal async void Apply()
        {
            bool isExists = await CheckFileExistsAsync(Wallpaper);
            if (!isExists) return;

            WpCustomizePage?.UpdatePropertyFile(true);
        }

        internal async Task ShowDetailedInfoAsync()
        {
            try
            {
                if (Wallpaper == null) return;

                var metaData = JsonStorage<MetaData>.LoadData(Path.Combine(Wallpaper.FolderPath, "MetaData.json"));
                if (metaData == null)
                {
                    await _dialogService.ShowDialogAsync(
                        _localizer.GetLocalizedString("Dialog_Content_ReadMetaDataFialed")
                        , _localizer.GetLocalizedString("Dialog_Title_Prompt")
                        , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
                    return;
                }

                Wallpaper.Title = metaData.Title;
                Wallpaper.Desc = metaData.Desc;
                Wallpaper.Tags = metaData.Tags;
                _ = await _dialogService.ShowDialogAsync(
                        new DetailedInfoView(new DetailedInfoViewModel(Wallpaper, false, true, false))
                        , _localizer.GetLocalizedString("Dialog_Title_Edit")
                        , _localizer.GetLocalizedString("Dialog_Btn_Confirm"));
            }
            catch (Exception ex)
            {
                _wpSettingsViewModel.ErrOccoured(ex);
            }
        }

        private void Loading(bool cancelEnable, bool progreaasbarEnavle, CancellationTokenSource[] cts)
        {
            _onLoading?.Invoke(cancelEnable, progreaasbarEnavle, cts);
        }

        private void Loaded(CancellationTokenSource[] cts)
        {
            foreach (var item in cts)
            {
                item?.Dispose();
            }
            _onLoaded?.Invoke();
        }

        private async void InitBasicInfo()
        {
            if (Wallpaper == null) await _onUpdateSource?.Invoke(WallpaperType.unknown, null, null);
            else await _onUpdateSource?.Invoke(Wallpaper.Type, Wallpaper.FilePath, Wallpaper.WpCustomizePathUsing);
        }

        private void InitCustomize()
        {
            if (Wallpaper == null) return;

            WpCustomizePage = new(_monitor, Wallpaper, IntValueChanged, DoubleValueChanged, BoolValueChanged, StringValueChanged);
        }

        private void InitCustomize(IMetaData metaData)
        {
            if (metaData == null) return;

            WpCustomizePage = new(_monitor, metaData, IntValueChanged, DoubleValueChanged, BoolValueChanged, StringValueChanged);
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
        private IMonitor _monitor;
        private IDialogService _dialogService;
        private IUserSettingsClient _userSettingsClient;
        private IWallpaperControlClient _wallpaperControlClient;
        private CancellationTokenSource _ctsImport;
        private Action<bool, bool, CancellationTokenSource[]> _onLoading;
        private Action _onLoaded;
        //private Action<int, int> _onImporting;
        private Func<WallpaperType, string, string, Task> _onUpdateSource;
        private WpSettingsViewModel _wpSettingsViewModel;
    }
}
