using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Cores.AppUpdate.Specific;
using VirtualPaper.lang;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.Services;
using VirtualPaper.Services.Interfaces;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace VirtualPaper.ViewModels {
    public class AppUpdaterWindowViewModel : ObservableObject, IWindowParameterReceiver {
        private string _version = string.Empty;
        public string Version {
            get => _version;
            set { _version = value; OnPropertyChanged(); }
        }

        private string _changeLog = string.Empty;
        public string ChangeLog {
            get => _changeLog;
            set { _changeLog = value; OnPropertyChanged(); }
        }

        private float _progress;
        public float Progress {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        private string _statusText = string.Empty;
        public string StatusText {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private string _speedValue = string.Empty;
        public string SpeedValue {
            get => _speedValue;
            set { _speedValue = value; OnPropertyChanged(); }
        }

        private string _sizeText = string.Empty;
        public string SizeText {
            get => _sizeText;
            set { _sizeText = value; OnPropertyChanged(); }
        }

        private string _remainingText = string.Empty;
        public string RemainingText {
            get => _remainingText;
            set { _remainingText = value; OnPropertyChanged(); }
        }

        private string _actionButtonText = string.Empty;
        public string ActionButtonText {
            get => _actionButtonText;
            set { _actionButtonText = value; OnPropertyChanged(); }
        }
        
        private bool _actionButtonEnable = true;
        public bool ActionButtonEnable {
            get => _actionButtonEnable;
            set { _actionButtonEnable = value; OnPropertyChanged(); }
        }

        private bool _isIndeterminate;
        public bool IsIndeterminate {
            get => _isIndeterminate;
            set { _isIndeterminate = value; OnPropertyChanged(); }
        }

        private bool _isError;
        public bool IsError {
            get => _isError;
            set { _isError = value; OnPropertyChanged(); }
        }

        public bool IsPluginsUpdate { get; private set; }

        public Action? RequestFlashTaskbar { get; set; }

        private DownloadState _currentState;
        public DownloadState CurrentState {
            get { return _currentState; }
            set {
                if (value == _currentState) return;

                _currentState = value;
                ActionButtonEnable = _currentState != DownloadState.Verifying;
                IsIndeterminate = _currentState == DownloadState.Verifying;
                UpdateUIByState();

                OnPropertyChanged();
            }
        }

        public AppUpdaterWindowViewModel(
            IDownloadService downloadService,
            IContentDialogService contentDialogService,
            IPluginsUpdateService pluginsUpdateService,
            IInstallerUpdateService installerUpdateService,
            IAppUpdaterService appUpdaterService) {
            _downloadService = downloadService;
            _contentDialogService = contentDialogService;
            _pluginsUpdateService = pluginsUpdateService;
            _installerUpdateService = installerUpdateService;
            _appUpdaterService = appUpdaterService;
        }

        public void ReceiveParameter(object? parameter) {
            if (parameter is ReleaseInfo info) {
                if (info.IsPluginsUpdate) {
                    IsPluginsUpdate = true;
                }
                _releaseInfo = info;

                Version = info.IsPluginsUpdate
                    ? $"{info.Version?.ToString()} (Build {info.AppBuild?.ToString()})"
                    : info.Version?.ToString() ?? string.Empty;
                ChangeLog = info.Changelog ?? string.Empty;
                CurrentState = DownloadState.Ready;
            }
        }

        public void AutoStartDownload() {
            if (CurrentState == DownloadState.Ready) {
                _ = StartDownloadAsync();
            }
        }

        public void Cancel() {
            if (_cts == null)
                return;

            _cts.Cancel();
            CurrentState = DownloadState.Paused;
        }

        public async Task<bool> ShowCancelDialogAsync() {
            var res = await _contentDialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions() {
                    Title = LanguageManager.Instance["AppUpdater_Update_TitleCancelQuestion"],
                    Content = CurrentState == DownloadState.Downloading ? LanguageManager.Instance["AppUpdater_Update_DescriptionCancelQuestion_ForDownloading"] : LanguageManager.Instance["AppUpdater_Update_DescriptionCancelQuestion_ForCompleted"],
                    PrimaryButtonText = LanguageManager.Instance["Common_TextConfirm"],
                    CloseButtonText = LanguageManager.Instance["Common_TextCancel"],
                }
            );

            return res == ContentDialogResult.Primary;
        }

        #region Command Handlers
        internal async void OnActionCommand() {
            switch (CurrentState) {
                case DownloadState.Ready:
                case DownloadState.DownloadFailed:
                case DownloadState.VerifyFailed:
                    await StartDownloadAsync();
                    break;

                case DownloadState.Downloading:
                    PauseDownload();
                    break;

                case DownloadState.Paused:
                    ResumeDownload();
                    break;

                case DownloadState.Completed:
                    // 关闭 UI 触发更新（plugin/installer 都通过 Proc_UI_Exited 执行）
                    break;
            }
        }
        #endregion

        #region Download Logic
        private async Task StartDownloadAsync() {
            if (IsPluginsUpdate) {
                await StartPluginsDownloadAsync();
                return;
            }

            await StartInstallerDownloadAsync();
        }

        private async Task StartInstallerDownloadAsync() {
            if (_releaseInfo == null)
                return;

            ResetAllState();
            _cts = new CancellationTokenSource();
            CurrentState = DownloadState.Downloading;

            try {
                var lastUpdate = DateTime.MinValue;
                var progress = new Progress<DownloadProgress>(p => {
                    // 节流：每 100ms 更新一次 UI
                    var now = DateTime.Now;
                    if ((now - lastUpdate).TotalMilliseconds < 100) return;
                    lastUpdate = now;

                    Progress = p.Percent;
                    UpdateSpeedInfo(p.Speed, p.ReceivedBytes, p.TotalBytes, p.Remaining);
                });

                var result = await _installerUpdateService.DownloadAsync(_releaseInfo, progress, _cts.Token);

                if (!result.Success || result.InstallerPath == null) {
                    CurrentState = DownloadState.DownloadFailed;
                    return;
                }

                _installerPath = result.InstallerPath;

                CurrentState = DownloadState.Verifying;
                var verifyResult = await _installerUpdateService.VerifyAsync(_releaseInfo, _installerPath, _cts.Token);

                if (!verifyResult.Success) {
                    CurrentState = DownloadState.VerifyFailed;
                    return;
                }

                CurrentState = DownloadState.Completed;

                // Trigger a new update check to refresh the status in GeneralSettingViewModel
                _ = Task.Run(() => _appUpdaterService.CheckUpdateAsync());
            }
            catch (OperationCanceledException) {
                FileUtil.DeleteDirectoryContents(Constants.CommonPaths.PendingInstallerUpdateDir);
                if (CurrentState != DownloadState.Paused)
                    CurrentState = DownloadState.Paused;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<AppUpdaterWindowViewModel>().Error(ex);
                CurrentState = DownloadState.DownloadFailed;
            }
        }

        private async Task StartPluginsDownloadAsync() {
            if (_releaseInfo == null)
                return;

            ResetAllState();
            _cts = new CancellationTokenSource();
            CurrentState = DownloadState.Downloading;

            try {
                var lastUpdate = DateTime.MinValue;
                var progress = new Progress<DownloadProgress>(p => {
                    // 节流：每 100ms 更新一次 UI
                    var now = DateTime.Now;
                    if ((now - lastUpdate).TotalMilliseconds < 100) return;
                    lastUpdate = now;

                    Progress = p.Percent;
                    UpdateSpeedInfo(p.Speed, p.ReceivedBytes, p.TotalBytes, p.Remaining);
                });

                var result = await _pluginsUpdateService.DownloadPendingAsync(_releaseInfo, progress, _cts.Token);

                if (!result.Success) {
                    CurrentState = DownloadState.DownloadFailed;
                    return;
                }

                CurrentState = DownloadState.Verifying;
                var verifyResult = await _pluginsUpdateService.VerifyAndSavePendingAsync(_releaseInfo, _cts.Token);

                if (!verifyResult.Success) {
                    CurrentState = DownloadState.VerifyFailed;
                    return;
                }

                CurrentState = DownloadState.Completed;

                // Trigger a new update check to refresh the status in GeneralSettingViewModel
                _ = Task.Run(() => _appUpdaterService.CheckUpdateAsync());
            }
            catch (OperationCanceledException) {
                FileUtil.RemoveDirectory(Constants.CommonPaths.PendingPluginsUpdateDir);
                if (CurrentState != DownloadState.Paused)
                    CurrentState = DownloadState.Paused;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<AppUpdaterWindowViewModel>().Error(ex);
                CurrentState = DownloadState.DownloadFailed;
            }
        }

        private void PauseDownload() {
            _downloadService.Pause();
            CurrentState = DownloadState.Paused;
        }

        private void ResumeDownload() {
            _downloadService.Resume();
            CurrentState = DownloadState.Downloading;
        }

        private void UpdateSpeedInfo(float speed, long receivedBytes, long totalBytes, TimeSpan remaining) {
            SpeedValue = $"{speed:F2} MB/s";
            SizeText = totalBytes > 0 ? $"{FileUtil.SizeSuffix(receivedBytes)} / {FileUtil.SizeSuffix(totalBytes)}" : string.Empty;
            RemainingText = $"{LanguageManager.Instance[nameof(Constants.I18n.AppUpdater_SpeedText_Ready)]}：{remaining:hh\\:mm\\:ss}";
        }

        private void ClearSpeedInfo() {
            SpeedValue = string.Empty;
            SizeText = string.Empty;
            RemainingText = string.Empty;
        }

        private void ResetAllState() {
            Progress = 0;
            IsError = false;
            IsIndeterminate = false;
            ClearSpeedInfo();
        }
        #endregion

        #region UI State Mapping
        private void UpdateUIByState() {
            switch (CurrentState) {
                case DownloadState.Ready:
                    ActionButtonText = LanguageManager.Instance["AppUpdater_ActionButtonText_Ready"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_Ready"];
                    Progress = 0;
                    break;

                case DownloadState.Downloading:
                    ActionButtonText = LanguageManager.Instance["AppUpdater_ActionButtonText_Downloading"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_Downloading"];
                    break;

                case DownloadState.Paused:
                    ActionButtonText = LanguageManager.Instance["AppUpdater_ActionButtonText_Paused"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_Paused"];
                    break;

                case DownloadState.Verifying:
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_Verifying"];
                    ClearSpeedInfo();
                    break;

                case DownloadState.Completed:
                    ActionButtonText = LanguageManager.Instance["Common_TextConfirm"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_Completed"];
                    ClearSpeedInfo();
                    _ = _contentDialogService.ShowSimpleDialogAsync(
                        new SimpleContentDialogCreateOptions() {
                            Title = LanguageManager.Instance["PluginsUpdate_Close"],
                            Content = IsPluginsUpdate
                                ? LanguageManager.Instance["PluginsUpdate_PostponeTip"]
                                : LanguageManager.Instance["InstallerUpdate_PostponeTip"],
                            CloseButtonText = LanguageManager.Instance["Common_TextConfirm"],
                        }
                    );
                    RequestFlashTaskbar?.Invoke();
                    break;

                case DownloadState.DownloadFailed:
                    ActionButtonText = LanguageManager.Instance["Common_TextRetry"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_DownloadFailed"];
                    IsError = true;
                    ClearSpeedInfo();
                    break;
                    
                case DownloadState.VerifyFailed:
                    ActionButtonText = LanguageManager.Instance["Common_TextRetry"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_VerifyFailed"];
                    IsError = true;
                    ClearSpeedInfo();
                    break;

                case DownloadState.Installing:
                    ActionButtonText = LanguageManager.Instance["AppUpdater_ActionButtonText_Installing"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_Installing"];
                    ClearSpeedInfo();
                    break;

                case DownloadState.Installed:
                    ActionButtonText = LanguageManager.Instance["Common_TextClose"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_Installed"];
                    break;
            }
        }
        #endregion

        public void Dispose() {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private readonly IDownloadService _downloadService;
        private readonly IContentDialogService _contentDialogService;
        private readonly IPluginsUpdateService _pluginsUpdateService;
        private readonly IInstallerUpdateService _installerUpdateService;
        private readonly IAppUpdaterService _appUpdaterService;
        private ReleaseInfo? _releaseInfo;
        private CancellationTokenSource? _cts;
        private string? _installerPath;
    }

    public enum DownloadState {
        None,
        Ready,
        Downloading,
        Paused,
        Verifying,
        Completed,
        DownloadFailed,
        VerifyFailed,
        Installing,
        Installed
    }
}
