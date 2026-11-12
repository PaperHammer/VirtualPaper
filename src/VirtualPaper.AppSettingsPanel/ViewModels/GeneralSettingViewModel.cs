using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Grpc.Core;
using VirtualPaper.AppSettingsPanel.Utils;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Localization;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Cores.AppUpdate.Models;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Events;
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

                var appBuild = LoadAppBuildInfo()?.AppBuildNumber;
                if (!string.IsNullOrEmpty(appBuild))
                    ver += $" (Build {appBuild})";

                return ver;
            }
        }

        public List<string> PluginVersionTexts {
            get {
                var plugins = LoadAppBuildInfo()?.Plugins;
                if (plugins == null) return [];
                return plugins
                    .OrderBy(kv => Enum.TryParse<PluginName>(kv.Key, true, out var p) ? (int)p : int.MaxValue)
                    .Select(kv => $"{kv.Key}: {kv.Value}")
                    .ToList();
            }
        }

        public bool HasPluginVersions => PluginVersionTexts.Count > 0;

        public Microsoft.UI.Xaml.Visibility PluginVersionsVisibility =>
            HasPluginVersions ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        private AppBuildInfo? LoadAppBuildInfo() {
            if (_buildInfo != null) return _buildInfo;

            // 从 AppData 目录读取 app_comp_manifest.json
            var path = Path.Combine(Constants.CommonPaths.AppDataDir, "app_comp_manifest.json");
            if (!File.Exists(path)) {
                _buildInfo = null;
                return null;
            }

            try {
                var json = File.ReadAllText(path);
                var manifest = JsonSerializer.Deserialize(json, UpdateManifestContext.Default.AppCompManifest);
                if (manifest?.Plugins == null) {
                    _buildInfo = null;
                    return null;
                }

                _buildInfo = new AppBuildInfo { AppBuildNumber = manifest.AppBuildNumber };
                foreach (var (pluginName, buildNumber) in manifest.Plugins) {
                    _buildInfo.Plugins[pluginName] = buildNumber;
                }
            }
            catch {
                _buildInfo = null;
            }

            return _buildInfo;
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

        private bool _isInstallBtnEnable = true;
        public bool IsInstallBtnEnable {
            get => _isInstallBtnEnable;
            set { _isInstallBtnEnable = value; OnPropertyChanged(); }
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

        public string IntelligentResultReminderStatus => LanguageUtil.GetI18n(
            RemindUnpreservedIntelligentResults ? Constants.I18n.Text_On : Constants.I18n.Text_Off);

        public bool RemindUnpreservedIntelligentResults {
            get => _userSettingsClient.Settings.RemindUnpreservedIntelligentResults;
            set {
                if (_userSettingsClient.Settings.RemindUnpreservedIntelligentResults == value) return;
                _userSettingsClient.Settings.RemindUnpreservedIntelligentResults = value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IntelligentResultReminderStatus));
            }
        }

        private int _seletedSystemBackdropIndx;
        public int SeletedSystemBackdropIndx {
            get => _seletedSystemBackdropIndx;
            set {
                if (_isRefreshingLanguage || value < 0) return;
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
                if (value == null || !IsLanguageChangeEnabled || value.Codes.Length == 0) return;
                if (value.Codes.Contains(_userSettingsClient.Settings.Language, StringComparer.OrdinalIgnoreCase)) return;
                _ = ChangeLanguageAsync(value);
            }
        }

        private bool _isLanguageChangeEnabled = true;
        public bool IsLanguageChangeEnabled {
            get => _isLanguageChangeEnabled;
            private set { _isLanguageChangeEnabled = value; OnPropertyChanged(); }
        }

        internal async Task ChangeLanguageAsync(LanguagesModel language) {
            var previousSelection = _selectedLanguage;
            string previousLanguage = _userSettingsClient.Settings.Language;
            // Guard reentry now, but let the selection provider finish before
            // notifying the binding that its control has become disabled.
            _isLanguageChangeEnabled = false;
            _selectedLanguage = language;
            _userSettingsClient.Settings.Language = language.Codes[0];
            OnPropertyChanged(nameof(SelectedLanguage));
            try {
                // Finish the two-way binding before restoring it after a save failure.
                await Task.Yield();
                OnPropertyChanged(nameof(IsLanguageChangeEnabled));
                await LanguageUtil.SetLanguageAsync(language.Codes[0]);
                await _userSettingsClient.SaveAsync<ISettings>();
            }
            catch (Exception exception) {
                _selectedLanguage = previousSelection;
                _userSettingsClient.Settings.Language = previousLanguage;
                try { await LanguageUtil.SetLanguageAsync(previousLanguage); }
                catch (Exception rollbackException) {
                    ArcLog.GetLogger<GeneralSettingViewModel>().Error("Language rollback failed", rollbackException);
                }
                OnPropertyChanged(nameof(SelectedLanguage));
                ArcLog.GetLogger<GeneralSettingViewModel>().Error("Language settings could not be saved", exception);
                GlobalMessageUtil.ShowException(exception);
            }
            finally { IsLanguageChangeEnabled = true; }
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

        private string? _text_UpdateReady;
        public string? Text_UpdateReady {
            get { return _text_UpdateReady; }
            set { _text_UpdateReady = value; OnPropertyChanged(); }
        }

        public ICommand? InstallBtnComand { get; private set; }
        public ICommand? ChangeFileStorageCommand { get; private set; }
        public ICommand? OpenFileStorageCommand { get; private set; }
        public ICommand? CheckUpdateCommand { get; private set; }
        public ICommand? StartDownloadComand { get; private set; }

        public GeneralSettingViewModel(
            IAppUpdaterClient appUpdater,
            IUserSettingsClient userSettingsClient,
            IWallpaperControlClient wallpaperControlClient,
            ICommandsClient commandsClient) {
            _appUpdater = appUpdater;
            _userSettingsClient = userSettingsClient;
            _wpControlClient = wallpaperControlClient;
            _commandsClient = commandsClient;

            InitText();
            InitCollections();
            InitContent();
            InitCommand();
            LanguageUtil.LanguageUpdated += OnLanguageUpdated;
        }

        private bool _isRefreshingLanguage;
        private AppUpdateStatus _lastUpdateStatus = AppUpdateStatus.Notchecked;
        private ReleaseInfo? _lastUpdateRelease;

        private void OnLanguageUpdated(object? sender, EventArgs args) {
            _isRefreshingLanguage = true;
            try {
                InitText();
                SystemBackdrops = [_sysbdDefault, _sysbdMica, _sysbdAcrylic];
                OnPropertyChanged(nameof(SystemBackdrops));
                OnPropertyChanged(nameof(SeletedSystemBackdropIndx));
                ChangeAutoShartStatu(_isAutoStart);
                OnPropertyChanged(nameof(IntelligentResultReminderStatus));
                OnPropertyChanged(nameof(AppVersionText));
                MenuUpdate(_lastUpdateStatus, _lastUpdateRelease);
            }
            finally { _isRefreshingLanguage = false; }
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
            InstallBtnComand = new RelayCommand(async () => {
                await RequstInstallAsync();
            });
        }

        private void InitContent() {
            _appUpdater.UpdateChecked += AppUpdater_UpdateChecked;
            _seletedSystemBackdropIndx = (int)_userSettingsClient.Settings.SystemBackdrop;
            _selectedLanguage = SupportedLanguages.GetLanguage(_userSettingsClient.Settings.Language);

            IsAutoStart = _userSettingsClient.Settings.IsAutoStart;
            WallpaperDir = _userSettingsClient.Settings.WallpaperDir;

            LoadAppBuildInfo();
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
            IsInstallBtnEnable = false;
            IsUpdateBtnEnable = false;
            IsUpdateRingActive = true;
            InfoBarVisibilityRestore();

            await _appUpdater.CheckUpdateAsync();

            IsUpdateRingActive = false;
            IsUpdateBtnEnable = true;
        }

        private void InfoBarVisibilityRestore() {
            CurrentVersionState = VersionState.None;
        }

        private void AppUpdater_UpdateChecked(object? sender, AppUpdaterEventArgs e) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                MenuUpdate(e.UpdateStatus, e.Release);
            });
        }

        private void MenuUpdate(AppUpdateStatus status, ReleaseInfo? release) {
            _lastUpdateStatus = status;
            _lastUpdateRelease = release;
            //CurrentVersionState = VersionState.FindNew;

            switch (status) {
                case AppUpdateStatus.Uptodate:
                    CurrentVersionState = VersionState.UptoNewest;
                    break;
                case AppUpdateStatus.Available:
                    Version = release?.IsPluginsUpdate == true
                        ? $"v{release?.Version} Build ({release?.AppBuild})"
                        : $"v{release?.Version}";
                    CurrentVersionState = VersionState.FindNew;
                    break;
                case AppUpdateStatus.InstallerReady:
                    Text_UpdateReady = LanguageUtil.GetI18n(Constants.I18n.Settings_General_Version_InstallerReady);
                    CurrentVersionState = VersionState.InstallReady;
                    IsInstallBtnEnable = true;
                    break;
                case AppUpdateStatus.PluginsReady:
                    Text_UpdateReady = LanguageUtil.GetI18n(Constants.I18n.Settings_General_Version_PluginsReady);
                    CurrentVersionState = VersionState.InstallReady;
                    IsInstallBtnEnable = true;
                    break;
                case AppUpdateStatus.Invalid or AppUpdateStatus.Error:
                    CurrentVersionState = VersionState.UpdateErr;
                    break;
                default:
                    break;
            }
            Version_LastCheckDate = LanguageUtil.GetI18n(Constants.I18n.Settings_General_Version_LastCheckDate);
            Version_LastCheckDate += status == AppUpdateStatus.Notchecked ? "" : $" {release?.CheckedTime}";
        }

        private async Task StartDownloadAsync() {
            IsUpdateBtnEnable = false;

            await _appUpdater.StartDownloadAsync();

            IsUpdateBtnEnable = true;
        }

        private async Task RequstInstallAsync() {
            await _commandsClient.RequestInstallAsync();
        }

        private async void WallpaperDirectoryChange() {
            var storagePath = (await WindowsStoragePickers.PickFolderAsync(WindowConsts.WindowHandle))?.Path;
            if (storagePath == null) return;

            if (storagePath == Constants.CommonPaths.AppDataDir) {
                GlobalMessageUtil.ShowError(Constants.I18n.Dialog_Content_WallpaperDirectoryChangePathInvalid, isNeedLocalizer: true);
                return;
            }

            await WallpaperDirectoryChangeAsync(storagePath);
        }

        private async void OpenFolder() {
            var folder = await StorageFolder.GetFolderFromPathAsync(WallpaperDir);
            await Launcher.LaunchFolderAsync(folder);
        }

        internal async Task WallpaperDirectoryChangeAsync(string destRootFolderPath) {
            if (WallpaperDirectoryChangeOngoing) return;
            var settings = _userSettingsClient.Settings;
            (WallpaperDirectoryDefinition Directory, string Source, string Target)[] moves = [];
            var changedLayouts = new List<(string Source, string Target)>();
            var sourceSnapshots = new Dictionary<string, WallpaperDirectorySnapshot>(StringComparer.OrdinalIgnoreCase);
            bool settingsChanged = false;
            WallpaperDirectoryChangeOngoing = true;

            try {
                string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destRootFolderPath));
                moves = WallpaperLibraryDirectories.All
                    .Select(directory => (Directory: directory, Source: directory.GetPath(settings),
                        Target: Path.Combine(destination, directory.FolderName)))
                    .Where(move => !Path.TrimEndingDirectorySeparator(Path.GetFullPath(move.Source))
                        .Equals(move.Target, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (moves.Length == 0) return;
                if (!Directory.Exists(settings.WallpaperDir)) throw new DirectoryNotFoundException(settings.WallpaperDir);
                ValidateMigrationDestination(destination, WallpaperLibraryDirectories.All.Select(directory => directory.GetPath(settings)));

                // Check every resulting path before copying any file contents.
                foreach (var move in moves) {
                    if (!Directory.Exists(move.Source)) continue;
                    var snapshot = await Task.Run(() => WallpaperDirectorySnapshot.Capture(move.Source));
                    snapshot.ValidateDestinationPathLength(move.Target);
                    sourceSnapshots.Add(move.Source, snapshot);
                }

                // Copy and verify every library before switching paths. Source data
                // is removed only after settings are saved and playback is restored.
                foreach (var move in moves) {
                    if (sourceSnapshots.TryGetValue(move.Source, out var snapshot))
                        await WallpaperDirectoryUpdateAsync(move.Source, move.Target, snapshot);
                }
                foreach (var move in moves) {
                    await _wpControlClient.ChangeWallpaperLayoutFolderPathAsync(move.Source, move.Target);
                    changedLayouts.Add((move.Source, move.Target));
                    settingsChanged = true;
                    move.Directory.SetPath(settings, move.Target);
                }
                await _userSettingsClient.SaveAsync<ISettings>();
                var response = await _wpControlClient.RestartAllWallpapersAsync();
                if (!response.IsFinished) throw new IOException("Restart all wallpapers failed.");

                // The new location is committed. Cleanup failures must not restore paths
                // to source folders that may already have been removed.
                try { WallpaperInstallDirChanged?.Invoke(this, EventArgs.Empty); }
                catch (Exception notification) { ArcLog.GetLogger<GeneralSettingViewModel>().Error(notification); }
                foreach (var move in moves) {
                    if (!sourceSnapshots.TryGetValue(move.Source, out var snapshot)) continue;
                    try {
                        if (!await Task.Run(() => DeleteUnchangedWallpaperDirectory(move.Source, snapshot)))
                            ArcLog.GetLogger<GeneralSettingViewModel>().Warn($"Wallpaper migration completed; the source changed and was retained: {move.Source}");
                    }
                    catch (Exception cleanup) { ArcLog.GetLogger<GeneralSettingViewModel>().Error(cleanup); }
                }
            }
            catch (Exception ex) {
                // Keep both the originals and any copied files if migration fails.
                try {
                    if (settingsChanged) {
                        foreach (var move in moves) move.Directory.SetPath(settings, move.Source);
                        await _userSettingsClient.SaveAsync<ISettings>();
                    }
                    foreach (var move in changedLayouts.AsEnumerable().Reverse()) {
                        await _wpControlClient.ChangeWallpaperLayoutFolderPathAsync(move.Target, move.Source);
                    }
                    if (changedLayouts.Count > 0) await _wpControlClient.RestartAllWallpapersAsync();
                }
                catch (Exception rollback) { ArcLog.GetLogger<GeneralSettingViewModel>().Error(rollback); }
                if (ex is OperationCanceledException || ex is RpcException { StatusCode: StatusCode.Cancelled })
                    GlobalMessageUtil.ShowCanceled();
                else GlobalMessageUtil.ShowException(ex);
                ArcLog.GetLogger<GeneralSettingViewModel>().Error(ex.Message);
            }
            finally {
                WallpaperDir = _userSettingsClient.Settings.WallpaperDir;
                WallpaperDirectoryChangeOngoing = false;
            }
        }

        private async void UpdateSettingsConfigFile() {
            await _userSettingsClient.SaveAsync<ISettings>();
        }

        internal static void ValidateMigrationDestination(string destination, IEnumerable<string> sources) {
            destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
            if (File.Exists(destination)) throw new IOException("The wallpaper destination must be a directory.");
            if (destination.Equals(Path.TrimEndingDirectorySeparator(Constants.CommonPaths.AppDataDir), StringComparison.OrdinalIgnoreCase))
                throw new IOException("The application data directory cannot be used as the wallpaper library root.");
            WallpaperDirectorySnapshot.ValidatePathLength(destination,
                WallpaperLibraryDirectories.All.Max(directory => directory.FolderName.Length));
            foreach (string source in sources) {
                string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
                if (destination.Equals(root, StringComparison.OrdinalIgnoreCase) || FileUtil.IsPathWithinDirectory(root, destination))
                    throw new IOException("The wallpaper destination cannot be inside a source library.");
            }
        }

        private async Task WallpaperDirectoryUpdateAsync(string sourceFolderPath, string destFolderPath, WallpaperDirectorySnapshot snapshot) {
            await Task.Run(() => {
                if (File.Exists(destFolderPath) || Directory.Exists(destFolderPath) && Directory.EnumerateFileSystemEntries(destFolderPath).Any())
                    throw new IOException($"The destination wallpaper directory is not empty: {destFolderPath}");
                FileUtil.CopyDirectory(sourceFolderPath, destFolderPath, true, overwrite: false);
                snapshot.VerifyCopy(destFolderPath);
                if (!snapshot.IsUnchanged(sourceFolderPath))
                    throw new IOException($"The source wallpaper directory changed during copying: {sourceFolderPath}");
            });
            await foreach (var libData in GetWpBasicDataByInstallFoldersAsync([sourceFolderPath])) {
                var data = (WpBasicData)libData.BasicData;
                string source = data.FolderPath;
                string target = Path.Combine(destFolderPath, Path.GetFileName(Path.TrimEndingDirectorySeparator(source)));
                data.FolderPath = target;
                data.FilePath = FileUtil.RelocatePath(data.FilePath, source, target);
                data.ThumbnailPath = FileUtil.RelocatePath(data.ThumbnailPath, source, target);
                await data.SaveMetadataAsync();
                foreach (string path in Directory.EnumerateFiles(source, Constants.Field.WpRuntimeDataFileName, SearchOption.AllDirectories)) {
                    var runtime = await WpRuntimeData.LoadAsync(path);
                    runtime.FolderPath = target;
                    runtime.DepthFilePath = FileUtil.RelocatePath(runtime.DepthFilePath, source, target);
                    runtime.WpEffectFilePathTemplate = FileUtil.RelocatePath(runtime.WpEffectFilePathTemplate, source, target);
                    runtime.WpEffectFilePathUsing = FileUtil.RelocatePath(runtime.WpEffectFilePathUsing, source, target);
                    runtime.WpEffectFilePathTemporary = FileUtil.RelocatePath(runtime.WpEffectFilePathTemporary, source, target);
                    runtime.Save();
                }
            }
        }

        private static bool DeleteUnchangedWallpaperDirectory(string source, WallpaperDirectorySnapshot snapshot) {
            if (!Directory.Exists(source)) return true;
            // No fixed delay or blind retry between checking for late writes and cleanup.
            if (!snapshot.IsUnchanged(source)) return false;
            Directory.Delete(source, recursive: true);
            return true;
        }

        internal static async IAsyncEnumerable<WpLibData> GetWpBasicDataByInstallFoldersAsync(List<string> folderPaths) {
            int idx = 0;
            foreach (string storeDir in folderPaths) {
                DirectoryInfo root = new(storeDir);
                DirectoryInfo[] folders = root.GetDirectories();

                foreach (DirectoryInfo folder in folders) {
                    string[] files = Directory.GetFiles(folder.FullName);
                    WpLibData libData = new();
                    foreach (string file in files) {
                        if (Path.GetFileName(file) == Constants.Field.WpBasicDataFileName) {
                            var basicData = await WpBasicData.LoadAsync(file);
                            if (basicData == null) continue;

                            libData.BasicData = basicData;
                            if (libData.BasicData != null && libData.BasicData.IsAvailable()) {
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
                LanguageUtil.LanguageUpdated -= OnLanguageUpdated;
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
        private readonly ICommandsClient _commandsClient;
        private AppBuildInfo? _buildInfo;
    }

    public enum VersionState {
        None,              // 无状态
        UptoNewest,        // 已是最新
        FindNew,           // 发现新版本
        Downloading,       // 正在下载
        DownloadFailed,    // 下载失败
        VerifyFailed,      // 校验失败
        Downloaded,        // 下载完成
        UpdateErr,          // 网络或更新错误
        InstallReady
    }
}
