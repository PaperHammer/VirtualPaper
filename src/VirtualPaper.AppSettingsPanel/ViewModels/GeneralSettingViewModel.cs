using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Grpc.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Events;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Localization;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Utils;
using Windows.Storage;
using Windows.System;

namespace VirtualPaper.AppSettingsPanel.ViewModels {
    public partial class GeneralSettingViewModel : ObservableObject, IDisposable {
        public event EventHandler? WallpaperInstallDirChanged;

        public bool IsWinStore => Constants.ApplicationType.IsMSIX;

        public string AppVersionText {
            get {
                var ver = "v" + _wpControlClient.AssemblyVersion;
                if (Constants.ApplicationType.IsTestBuild)
                    ver += "b";
                else if (Constants.ApplicationType.IsMSIX)
                    ver += $" {LanguageUtil.GetI18n(Constants.I18n.Settings_General_Version_MsStore)}";
                return ver;
            }
        }

        public List<string> SystemBackdrops { get; set; } = [];
        public List<LanguagesModel> Languages { get; set; } = [];

        private string _autoStartStatu = string.Empty;
        public string AutoStartStatu {
            get => _autoStartStatu;
            set { _autoStartStatu = value; OnPropertyChanged(); }
        }

        private VersionState _currentVersionState = VersionState.None;
        public VersionState CurrentVersionState {
            get => _currentVersionState;
            set { _currentVersionState = value; OnPropertyChanged(); }
        }

        private string _version_LastCheckDate = string.Empty;
        public string Version_LastCheckDate {
            get => _version_LastCheckDate;
            private set { _version_LastCheckDate = value; OnPropertyChanged(); }
        }

        private string _version = string.Empty;
        public string Version {
            get => _version;
            set { _version = value; OnPropertyChanged(); }
        }

        private bool _isUpdateBtnEnable = true;
        public bool IsUpdateBtnEnable {
            get => _isUpdateBtnEnable;
            set { _isUpdateBtnEnable = value; OnPropertyChanged(); }
        }

        private bool _isUpdateRingActive = false;
        public bool IsUpdateRingActive {
            get => _isUpdateRingActive;
            set { _isUpdateRingActive = value; OnPropertyChanged(); }
        }

        private float _downloadProgress = 0;
        public float DownloadProgress {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        private string _downloadProgressText = string.Empty;
        public string DownloadProgressText {
            get => _downloadProgressText;
            set { _downloadProgressText = value; OnPropertyChanged(); }
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

        private LanguagesModel _selectedLanguage = null!;
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

        public ICommand? ChangeFileStorageCommand { get; private set; }
        public ICommand? OpenFileStorageCommand { get; private set; }
        public ICommand? CheckUpdateCommand { get; private set; }
        public ICommand? StartDownloadComand { get; private set; }

        public GeneralSettingViewModel(
            IAppUpdaterClient appUpdater,
            IUserSettingsClient userSettingsClient,
            IWallpaperControlClient wallpaperControlClient) {
            _appUpdater = appUpdater;
            _userSettingsClient = userSettingsClient;
            _wpControlClient = wallpaperControlClient;

            InitText();
            InitCollections();
            InitContent();
            InitCommand();
        }

        private void InitCommand() {
            ChangeFileStorageCommand = new RelayCommand(() => {
                WallpaperDirectoryChange();
            });
            OpenFileStorageCommand = new RelayCommand(() => {
                OpenFolder();
            });
            CheckUpdateCommand = new RelayCommand(async () => {
                await CheckUpdateAsync();
            });
            StartDownloadComand = new RelayCommand(async () => {
                await StartDownloadAsync();
            });
        }

        private void InitContent() {
            _appUpdater.UpdateChecked += AppUpdater_UpdateChecked;
            _seletedSystemBackdropIndx = (int)_userSettingsClient.Settings.SystemBackdrop;
            _selectedLanguage = SupportedLanguages.GetLanguage(_userSettingsClient.Settings.Language);

            IsAutoStart = _userSettingsClient.Settings.IsAutoStart;
            WallpaperDir = _userSettingsClient.Settings.WallpaperDir;
        }

        private void InitText() {
            Version_LastCheckDate = LanguageUtil.GetI18n(Constants.I18n.Settings_General_Version_LastCheckDate);

            _sysbdDefault = LanguageUtil.GetI18n(Constants.I18n.Settings_General_AppearanceAndAction__sysbdDefault);
            _sysbdMica = LanguageUtil.GetI18n(Constants.I18n.Settings_General_AppearanceAndAction__sysbdMica);
            _sysbdAcrylic = LanguageUtil.GetI18n(Constants.I18n.Settings_General_AppearanceAndAction__sysbdAcrylic);
        }

        private void InitCollections() {
            Languages = [.. SupportedLanguages.Languages];
            SystemBackdrops = [_sysbdDefault, _sysbdMica, _sysbdAcrylic];
        }

        private void ChangeAutoShartStatu(bool isAutoStart) {
            if (isAutoStart) {
                AutoStartStatu = LanguageUtil.GetI18n(Constants.I18n.Text_On);
            }
            else {
                AutoStartStatu = LanguageUtil.GetI18n(Constants.I18n.Text_Off);
            }
        }

        private async Task CheckUpdateAsync() {
            IsUpdateBtnEnable = false;
            IsUpdateRingActive = true;
            InfoBarVisibilityRestore();

            await _appUpdater.CheckUpdateAsync();

            IsUpdateBtnEnable = true;
            IsUpdateRingActive = false;
        }

        private void InfoBarVisibilityRestore() {
            CurrentVersionState = VersionState.None;
        }

        private void AppUpdater_UpdateChecked(object? sender, AppUpdaterEventArgs e) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                MenuUpdate(e.UpdateStatus, e.UpdateDate, e.UpdateVersion);
            });
        }

        private void MenuUpdate(AppUpdateStatus status, DateTime date, Version version) {
            Version = $"v{version}";
#if DEBUG
            CurrentVersionState = VersionState.FindNew;
#else
            switch (status) {
                case AppUpdateStatus.Uptodate:
                    CurrentVersionState = VersionState.UptoNewest;
                    break;
                case AppUpdateStatus.Available:
                    Version = $"v{version}";
                    CurrentVersionState = VersionState.FindNew;
                    break;
                case AppUpdateStatus.Invalid or AppUpdateStatus.Error:
                    CurrentVersionState = VersionState.UpdateErr;
                    break;
                default:
                    break;
            }
#endif
            Version_LastCheckDate = LanguageUtil.GetI18n(Constants.I18n.Settings_General_Version_LastCheckDate);
            Version_LastCheckDate += status == AppUpdateStatus.Notchecked ? "" : $" {date}";
        }

        private async Task StartDownloadAsync() {
            IsUpdateBtnEnable = false;

            await _appUpdater.StartDownloadAsync();

            IsUpdateBtnEnable = true;
        }

        private async void WallpaperDirectoryChange() {
            var storagePath = (await WindowsStoragePickers.PickFolderAsync(WindowConsts.WindowHandle))?.Path;
            if (storagePath == null) return;

            if (storagePath == Constants.CommonPaths.AppDataDir) {
                GlobalMessageUtil.ShowError(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), Constants.I18n.Dialog_Content_WallpaperDirectoryChangePathInvalid, isNeedLocalizer: true);
                return;
            }

            if (!string.Equals(storagePath, _userSettingsClient.Settings.WallpaperDir, StringComparison.OrdinalIgnoreCase)) {
                await WallpaperDirectoryChangeAsync(storagePath);
            }
        }

        private async void OpenFolder() {
            var folder = await StorageFolder.GetFolderFromPathAsync(WallpaperDir);
            await Launcher.LaunchFolderAsync(folder);
        }

        private async Task WallpaperDirectoryChangeAsync(string destRootFolderPath) {
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
                    GlobalMessageUtil.ShowError(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), Constants.I18n.InfobarMsg_Err, isNeedLocalizer: true);
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
            catch (Exception ex) when
                        (ex is OperationCanceledException ||
                        (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)) {
                GlobalMessageUtil.ShowCanceled(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)));
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
                ArcLog.GetLogger<GeneralSettingViewModel>().Error(ex.Message);
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

        private async Task<bool> WallpaperDirectoryUpdateAsync(List<string> wallpaperInstallFolders, string destFolderPath) {
            bool allOperationsSuccessful = true;

            try {
                await foreach (var libData in GetWpBasicDataByInstallFoldersAsync(wallpaperInstallFolders)) {
                    var data = libData.BasicData;
                    await data.MoveToAsync(Path.Combine(destFolderPath, data.FolderName));
                }
            }
            catch (Exception ex) {
                allOperationsSuccessful = false;
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
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
                            libData.BasicData = await JsonSaver.LoadAsync<WpBasicData>(file, WpBasicDataContext.Default);

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

        #region dispose
        private bool _disposed = false;
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
                _appUpdater.UpdateChecked -= AppUpdater_UpdateChecked;
                WallpaperInstallDirChanged = null;
            }

            _disposed = true;
        }
        #endregion

        private string _sysbdDefault = string.Empty;
        private string _sysbdMica = string.Empty;
        private string _sysbdAcrylic = string.Empty;
        private readonly IAppUpdaterClient _appUpdater;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly IWallpaperControlClient _wpControlClient;
    }

    public enum VersionState {
        None,              // 无状态
        UptoNewest,        // 已是最新
        FindNew,           // 发现新版本
        Downloading,       // 正在下载
        DownloadFailed,    // 下载失败
        VerifyFailed,      // 校验失败
        Downloaded,        // 下载完成
        UpdateErr          // 网络或更新错误
    }
}
