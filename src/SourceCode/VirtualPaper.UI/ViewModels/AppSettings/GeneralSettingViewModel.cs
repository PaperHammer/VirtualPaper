using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Models;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Localization;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UI.Utils;
using VirtualPaper.UIComponent.Utils;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinUI3Localizer;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using DispatcherQueueController = Microsoft.UI.Dispatching.DispatcherQueueController;

namespace VirtualPaper.UI.ViewModels.AppSettings {
    public partial class GeneralSettingViewModel : ObservableObject {
        public event EventHandler WallpaperInstallDirChanged;

        public string Text_Version { get; set; } = string.Empty;
        public string Version_Release_Notes { get; set; } = string.Empty;
        public string Version_UpdateCheck { get; set; } = string.Empty;
        public string Version_DownloadCancel { get; set; } = string.Empty;
        public string Version_DownloadStart { get; set; } = string.Empty;
        public string Version_FindNew { get; set; } = string.Empty;
        public string Version_Download { get; set; } = string.Empty;
        public string Version_SeeNews { get; set; } = string.Empty;
        public string Version_UpdateErr { get; set; } = string.Empty;
        public string Version_Install { get; set; } = string.Empty;
        public string Version_UptoNewest { get; set; } = string.Empty;

        public string Text_AppearanceAndAction { get; set; } = string.Empty;
        public string AppearanceAndAction_AutoStart { get; set; } = string.Empty;
        public string AppearanceAndAction_AutoStatExplain { get; set; } = string.Empty;
        public string AppearanceAndAction_AppTheme { get; set; } = string.Empty;
        public string AppearanceAndAction_AppThemeExplain { get; set; } = string.Empty;
        public string AppearanceAndAction_AppThemeHyperlink { get; set; } = string.Empty;
        public string AppearanceAndAction_AppSystemBackdrop { get; set; } = string.Empty;
        public string AppearanceAndAction_AppSystemBackdropExplain { get; set; } = string.Empty;
        public string AppearanceAndAction_AppSystemBackdrop_Mica_Hyperlink { get; set; } = string.Empty;
        public string AppearanceAndAction_AppSystemBackdrop_Acrylic_Hyperlink { get; set; } = string.Empty;
        public string AppearanceAndAction_AppLanguage { get; set; } = string.Empty;
        public string AppearanceAndAction_AppLanguageExplain { get; set; } = string.Empty;
        public string AppearanceAndAction_AppFileStorage { get; set; } = string.Empty;
        public string AppearanceAndAction_AppFileStorageExplain { get; set; } = string.Empty;
        public string AppearanceAndAction_AppFileStorage_ModifyTooltip { get; set; } = string.Empty;
        public string AppearanceAndAction_AppFileStorage_OpenTooltip { get; set; } = string.Empty;

        public bool IsWinStore => Constants.ApplicationType.IsMSIX;

        public string AppVersionText {
            get {
                var ver = "v" + _wpControlClient.AssemblyVersion;
                if (Constants.ApplicationType.IsTestBuild)
                    ver += "b";
                else if (Constants.ApplicationType.IsMSIX)
                    //ver += $" {_i18n.GetString("Settings_General_Version_MsStore")}";
                    ver += $" {_localizer.GetLocalizedString("Settings_General_Version_MsStore")}";
                return ver;
            }
        }

        public List<string> Themes { get; set; } = [];
        public List<string> SystemBackdrops { get; set; } = [];
        public List<LanguagesModel> Languages { get; set; } = [];

        private string _autoStartStatu = string.Empty;
        public string AutoStartStatu {
            get => _autoStartStatu;
            set { _autoStartStatu = value; OnPropertyChanged(); }
        }

        private Visibility _infoBar_Version_FindNew = Visibility.Collapsed;
        public Visibility InfoBar_Version_FindNew {
            get => _infoBar_Version_FindNew;
            set { _infoBar_Version_FindNew = value; OnPropertyChanged(); }
        }

        private Visibility _infoBar_Version_UpdateErr = Visibility.Collapsed;
        public Visibility InfoBar_Version_UpdateErr {
            get => _infoBar_Version_UpdateErr;
            set { _infoBar_Version_UpdateErr = value; OnPropertyChanged(); }
        }

        private Visibility _infoBar_Version_UptoNewest = Visibility.Collapsed;
        public Visibility InfoBar_Version_UptoNewest {
            get => _infoBar_Version_UptoNewest;
            set { _infoBar_Version_UptoNewest = value; OnPropertyChanged(); }
        }

        private string _version_LastCheckDate = string.Empty;
        public string Version_LastCheckDate {
            get => _version_LastCheckDate;
            set { _version_LastCheckDate = value; OnPropertyChanged(); }
        }

        private string _version = string.Empty;
        public string Version {
            get => _version;
            set { _version = value; OnPropertyChanged(); }
        }

        private bool _isStoped = true;
        public bool IsStoped {
            get => _isStoped;
            set { _isStoped = value; OnPropertyChanged(); }
        }

        private bool _isAutoStart;
        public bool IsAutoStart {
            get => _isAutoStart;
            set {
                _isAutoStart = value;
                ChangeAutoShartStatu(value);
                if (_userSettingsClient.Settings.IsAutoStart == value) return;

                _userSettingsClient.Settings.IsAutoStart = value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
            }
        }

        private int _seletedThemeIndx;
        public int SeletedThemeIndx {
            get => _seletedThemeIndx;
            set {
                _seletedThemeIndx = value;
                if (_userSettingsClient.Settings.ApplicationTheme == (AppTheme)value) return;

                _userSettingsClient.Settings.ApplicationTheme = (AppTheme)value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
            }
        }

        private int _seletedSystemBackdropIndx;
        public int SeletedSystemBackdropIndx {
            get => _seletedSystemBackdropIndx;
            set {
                _seletedSystemBackdropIndx = value;
                if (_userSettingsClient.Settings.SystemBackdrop == (AppSystemBackdrop)value) return;

                _userSettingsClient.Settings.SystemBackdrop = (AppSystemBackdrop)value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
            }
        }

        private LanguagesModel _selectedLanguage;
        public LanguagesModel SelectedLanguage {
            get => _selectedLanguage;
            set {
                _selectedLanguage = value;
                if (_userSettingsClient.Settings.Language == value.Language) return;

                if (value.Codes.FirstOrDefault(x => x == _userSettingsClient.Settings.Language) == null) {
                    _userSettingsClient.Settings.Language = value.Codes[0];
                    UpdateSettingsConfigFile();
                    LanguageUtil.LanguageChanged(value.Codes[0]);
                    OnPropertyChanged();
                }
            }
        }

        private string _wallpaperDir = string.Empty;
        public string WallpaperDir {
            get { return _wallpaperDir; }
            set { _wallpaperDir = value; OnPropertyChanged(); }
        }

        private bool _wallpaperDirectoryChangeOngoing;
        public bool WallpaperDirectoryChangeOngoing {
            get { return _wallpaperDirectoryChangeOngoing; }
            set {
                _wallpaperDirectoryChangeOngoing = value;
                OnPropertyChanged();
                IsWallpaperDirectoryChangeEnable = !value;
            }
        }

        private bool _isWallpaperDirectoryChangeEnable = true;
        public bool IsWallpaperDirectoryChangeEnable {
            get { return _isWallpaperDirectoryChangeEnable; }
            set { _isWallpaperDirectoryChangeEnable = value; OnPropertyChanged(); }
        }

        public GeneralSettingViewModel(
            IAppUpdaterClient appUpdater,
            IDialogService dialogService,
            IUserSettingsClient userSettingsClient,
            IWallpaperControlClient wallpaperControlClient) {
            _dialogService = dialogService;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? DispatcherQueueController.CreateOnCurrentThread().DispatcherQueue;

            _appUpdater = appUpdater;
            _userSettingsClient = userSettingsClient;
            _wpControlClient = wallpaperControlClient;

            _localizer = LanguageUtil.LocalizerInstacne;

            InitText();
            InitCollections();
            InitContent();
        }

        private void InfoBarVisibilityRestore() {
            InfoBar_Version_FindNew = Visibility.Collapsed;
            InfoBar_Version_UpdateErr = Visibility.Collapsed;
            InfoBar_Version_UptoNewest = Visibility.Collapsed;
        }

        private void InitContent() {
            _appUpdater.UpdateChecked += AppUpdater_UpdateChecked;
            _seletedThemeIndx = (int)_userSettingsClient.Settings.ApplicationTheme;
            _seletedSystemBackdropIndx = (int)_userSettingsClient.Settings.SystemBackdrop;
            _selectedLanguage = SupportedLanguages.GetLanguage(_userSettingsClient.Settings.Language);

            IsAutoStart = _userSettingsClient.Settings.IsAutoStart;
            WallpaperDir = _userSettingsClient.Settings.WallpaperDir;
        }

        private void InitText() {
            Text_Version = _localizer.GetLocalizedString("Settings_General_Text_Version");
            Version_Release_Notes = _localizer.GetLocalizedString("Settings_General_Version_Release_Notes");
            Version_UpdateCheck = _localizer.GetLocalizedString("Settings_General_Version_UpdateCheck");
            Version_DownloadCancel = _localizer.GetLocalizedString("Settings_General_Version_DownloadCancel");
            Version_DownloadStart = _localizer.GetLocalizedString("Settings_General_Version_DownloadStart");
            Version_FindNew = _localizer.GetLocalizedString("Settings_General_Version_FindNew");
            Version_Download = _localizer.GetLocalizedString("Settings_General_Version_Download");
            Version_SeeNews = _localizer.GetLocalizedString("Settings_General_Version_SeeNews");
            Version_UpdateErr = _localizer.GetLocalizedString("Settings_General_Version_UpdateErr");
            Version_Install = _localizer.GetLocalizedString("Settings_General_Version_Install");
            Version_UptoNewest = _localizer.GetLocalizedString("Settings_General_Version_UptoNewest");
            Version_LastCheckDate = _localizer.GetLocalizedString("Settings_General_Version_LastCheckDate");

            Text_AppearanceAndAction = _localizer.GetLocalizedString("Settings_General_Text_AppearanceAndAction");
            AppearanceAndAction_AutoStart = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AutoStart");
            AppearanceAndAction_AutoStatExplain = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AutoStatExplain");
            AppearanceAndAction_AppTheme = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppTheme");
            AppearanceAndAction_AppThemeExplain = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppThemeExplain");
            AppearanceAndAction_AppThemeHyperlink = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppThemeHyperlink");
            _themeDark = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction__themeDark");
            _themeLight = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction__themeLight");
            _themeFollowSystem = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction__themeFollowSystem");
            AppearanceAndAction_AppSystemBackdrop = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppSystemBackdrop");
            AppearanceAndAction_AppSystemBackdropExplain = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppSystemBackdropExplain");
            AppearanceAndAction_AppSystemBackdrop_Mica_Hyperlink = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppSystemBackdrop_Mica_Hyperlink");
            AppearanceAndAction_AppSystemBackdrop_Acrylic_Hyperlink = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppSystemBackdrop_Acrylic_Hyperlink");
            _sysbdDefault = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction__sysbdDefault");
            _sysbdMica = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction__sysbdMica");
            _sysbdAcrylic = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction__sysbdAcrylic");
            AppearanceAndAction_AppLanguage = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppLanguage");
            AppearanceAndAction_AppLanguageExplain = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppLanguageExplain");
            AppearanceAndAction_AppFileStorage = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppFileStorage");
            AppearanceAndAction_AppFileStorageExplain = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppFileStorageExplain");
            AppearanceAndAction_AppFileStorage_ModifyTooltip = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppFileStorage_ModifyTooltip");
            AppearanceAndAction_AppFileStorage_OpenTooltip = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AppFileStorage_OpenTooltip");
        }

        private void InitCollections() {
            Themes = [_themeFollowSystem, _themeLight, _themeDark];
            Languages = [.. SupportedLanguages.Languages];
            SystemBackdrops = [_sysbdDefault, _sysbdMica, _sysbdAcrylic];
        }

        private void ChangeAutoShartStatu(bool isAutoStart) {
            if (isAutoStart) {
                AutoStartStatu = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AutoStartStatu_On");
            }
            else {
                AutoStartStatu = _localizer.GetLocalizedString("Settings_General_AppearanceAndAction_AutoStartStatu_Off");
            }
        }

        internal async Task CheckUpdateAsync() {
            InfoBarVisibilityRestore();

            await _appUpdater.CheckUpdate();
        }

        private void AppUpdater_UpdateChecked(object sender, AppUpdaterEventArgs e) {
            _ = _dispatcherQueue.TryEnqueue(() => {
                MenuUpdate(e.UpdateStatus, e.UpdateDate, e.UpdateVersion);
            });
        }

        private void MenuUpdate(AppUpdateStatus status, DateTime date, Version version) {
            switch (status) {
                case AppUpdateStatus.uptodate:
                    InfoBar_Version_UptoNewest = Visibility.Visible;
                    break;
                case AppUpdateStatus.available:
                    Version = $"v{version}";
                    InfoBar_Version_FindNew = Visibility.Visible;
                    break;
                case AppUpdateStatus.invalid or AppUpdateStatus.error:
                    InfoBar_Version_UpdateErr = Visibility.Visible;
                    break;
                default:
                    break;
            }
            Version_LastCheckDate = _localizer.GetLocalizedString("Settings_General_Version_LastCheckDate");
            Version_LastCheckDate += status == AppUpdateStatus.notchecked ? "" : $"{date}";
        }

        internal async Task StartDownloadAsync() {
            IsStoped = false;

            await _appUpdater.StartUpdate();

            IsStoped = true;
        }

        internal async void WallpaperDirectoryChange() {
            var folderPicker = new FolderPicker();
            folderPicker.SetOwnerWindow(App.Services.GetRequiredService<MainWindow>());
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null) {
                return;
            }
            if (folder.Path == Constants.CommonPaths.AppDataDir) {
                await _dialogService.ShowDialogAsync(
                        _localizer.GetLocalizedString(Constants.I18n.Dialog_Content_WallpaperDirectoryChangePathInvalid)
                        , _localizer.GetLocalizedString(Constants.I18n.Dialog_Title_Prompt)
                        , _localizer.GetLocalizedString(Constants.I18n.Text_Confirm));
                return;
            }

            if (folder != null && !string.Equals(folder.Path, _userSettingsClient.Settings.WallpaperDir, StringComparison.OrdinalIgnoreCase)) {
                await WallpaperDirectoryChange(folder.Path);
            }
        }

        internal async void OpenFolder() {
            var folder = await StorageFolder.GetFolderFromPathAsync(WallpaperDir);
            await Launcher.LaunchFolderAsync(folder);
        }

        private async Task WallpaperDirectoryChange(string destRootFolderPath) {
            string destFolderPath = string.Empty;
            WallpaperDirectoryChangeOngoing = true;

            try {
                #region 构建目标路径
                destFolderPath = Path.Combine(destRootFolderPath, Constants.FolderName.WpStoreFolderName);
                #endregion

                #region 更新代替换文件中的路径，并移动文件
                bool isDirChanged = await WallpaperDirectoryUpdateAsync(
                    [_userSettingsClient.Settings.WallpaperDir], destFolderPath);
                if (!isDirChanged) {
                    BasicUIComponentUtil.ShowMsg(true, Constants.I18n.InfobarMsg_Err, InfoBarSeverity.Error);
                    return;
                }
                #endregion             

                #region 更新存储的运行信息，重启壁纸
                var previousDirFolderPath = _userSettingsClient.Settings.WallpaperDir;
                _userSettingsClient.Settings.WallpaperDir = destFolderPath;
                UpdateSettingsConfigFile();
                WallpaperInstallDirChanged?.Invoke(this, EventArgs.Empty);
                WallpaperDir = _userSettingsClient.Settings.WallpaperDir;
                await _wpControlClient.ChangeWallpaperLayoutFolrderPathAsync(previousDirFolderPath, destFolderPath);
                var response = await _wpControlClient.RestartAllWallpapersAsync();
                if (!response.IsFinished) {
                    throw new Exception("Restart all wallpapers failed");
                }
                #endregion

                #region 删除原目录下的文件
                _ = await FileUtil.TryDeleteDirectoryAsync(previousDirFolderPath, 1000, 3000);
                #endregion

            }
            catch (OperationCanceledException) {
                BasicUIComponentUtil.ShowMsg(true, Constants.I18n.InfobarMsg_Cancel, InfoBarSeverity.Warning);
                if (destFolderPath != string.Empty) {
                    FileUtil.EmptyDirectory(destFolderPath);
                }
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowMsg(true, Constants.I18n.InfobarMsg_Err, InfoBarSeverity.Error);
                _logger.Error(ex);
                if (destFolderPath != string.Empty) {
                    FileUtil.EmptyDirectory(destFolderPath);
                }
            }
            finally {
                WallpaperDir = _userSettingsClient.Settings.WallpaperDir;
                WallpaperDirectoryChangeOngoing = false;
            }
        }

        private async void UpdateSettingsConfigFile() {
            await _userSettingsClient.SaveAsync<ISettings>();
        }

        private static async Task<bool> WallpaperDirectoryUpdateAsync(List<string> wallpaperInstallFolders, string destFolderPath) {
            bool allOperationsSuccessful = true;

            try {
                await foreach (var libData in GetWpBasicDataByInstallFoldersAsync(wallpaperInstallFolders)) {
                    var data = libData.BasicData;
                    await data.MoveToAsync(Path.Combine(destFolderPath, data.FolderName));
                }
            }
            catch (Exception ex) {
                allOperationsSuccessful = false;
                BasicUIComponentUtil.ShowExp(ex);
            }

            return allOperationsSuccessful;
        }

        private static async IAsyncEnumerable<WpLibData> GetWpBasicDataByInstallFoldersAsync(List<string> folderPaths) {
            int idx = 0;
            foreach (string storeDir in folderPaths) {
                DirectoryInfo root = new(storeDir);
                DirectoryInfo[] folders = root.GetDirectories();

                foreach (DirectoryInfo folder in folders) {
                    string[] files = Directory.GetFiles(folder.FullName);
                    WpLibData libData = new();
                    foreach (string file in files) {
                        if (Path.GetFileName(file) == Constants.Field.WpBasicDataFileName) {
                            libData.BasicData = await JsonStorage<WpBasicData>.LoadDataAsync(file);

                            if (libData.BasicData.IsAvailable()) {
                                libData.Idx = idx++;
                                yield return libData;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private readonly ILocalizer _localizer;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private string _themeDark = string.Empty;
        private string _themeLight = string.Empty;
        private string _themeFollowSystem = string.Empty;
        private string _sysbdDefault = string.Empty;
        private string _sysbdMica = string.Empty;
        private string _sysbdAcrylic = string.Empty;
        private readonly IAppUpdaterClient _appUpdater;
        private readonly IDialogService _dialogService;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly DispatcherQueue _dispatcherQueue;
    }
}
