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

        public bool IsPluginsUpdate => _releaseInfo?.IsPluginsUpdate ?? false;

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
            IServiceProvider serviceProvider,
            IAppUpdaterService appUpdaterService,
            Func<SimpleContentDialogCreateOptions, Task<ContentDialogResult>>? dialogPresenter = null) {
            _downloadService = downloadService;
            _contentDialogService = contentDialogService;
            _serviceProvider = serviceProvider;
            _appUpdaterService = appUpdaterService;
            _dialogPresenter = dialogPresenter ??
                (options => _contentDialogService.ShowSimpleDialogAsync(options));
        }

        public void ReceiveParameter(object? parameter) {
            if (parameter is ReleaseInfo info) {
                _releaseInfo = info;
                _handler = UpdateServiceFactory.Resolve(_serviceProvider, info);

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
            var res = await _dialogPresenter(
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
        internal async void OnActionCommand() => await OnActionCommandAsync();

        internal async Task OnActionCommandAsync() {
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
            if (_releaseInfo == null || _handler == null)
                return;

            ResetAllState(CurrentState == DownloadState.VerifyFailed);
            _cts = new CancellationTokenSource();
            CurrentState = DownloadState.Downloading;

            try {
                var lastUpdate = DateTime.MinValue;
                var progress = new Progress<DownloadProgress>(p => {
                    var now = DateTime.Now;
                    if ((now - lastUpdate).TotalMilliseconds < 100) return;
                    lastUpdate = now;

                    Progress = p.Percent;
                    UpdateSpeedInfo(p.Speed, p.ReceivedBytes, p.TotalBytes, p.Remaining);
                });

                var success = await _handler.DownloadUpdateAsync(_releaseInfo, progress, _cts.Token);
                if (!success) {
                    CurrentState = DownloadState.DownloadFailed;
                    return;
                }

                CurrentState = DownloadState.Verifying;
                success = await _handler.VerifyUpdateAsync(_releaseInfo, _cts.Token);
                if (!success) {
                    CurrentState = DownloadState.VerifyFailed;
                    if (IsPluginsUpdate)
                        FileUtil.RemoveDirectory(Constants.CommonPaths.PendingPluginsUpdateDir);
                    return;
                }

                CurrentState = DownloadState.Completed;

                // Trigger a new update check to refresh the status in GeneralSettingViewModel
                _ = Task.Run(() => _appUpdaterService.CheckUpdateAsync());
            }
            catch (OperationCanceledException) {
                var dir = IsPluginsUpdate
                    ? Constants.CommonPaths.PendingPluginsUpdateDir
                    : Constants.CommonPaths.PendingInstallerUpdateDir;

                for (int i = 0; i < 5; i++) {
                    try {
                        FileUtil.RemoveDirectory(dir);
                        break;
                    }
                    catch {
                        await Task.Delay(100);
                    }
                }
                if (CurrentState != DownloadState.Paused)
                    CurrentState = DownloadState.Paused;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<AppUpdaterWindowViewModel>().Error(ex);
                CurrentState = CurrentState == DownloadState.Verifying
                    ? DownloadState.VerifyFailed
                    : DownloadState.DownloadFailed;
            }
            finally {
                _cts?.Dispose();
                _cts = null;
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

        private void ResetAllState(bool isRedownloadAll) {
            if (isRedownloadAll) {
                Progress = 0;
                ClearSpeedInfo();
            }
            IsError = false;
            IsIndeterminate = false;
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
                    _ = _dialogPresenter(
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
                    RequestFlashTaskbar?.Invoke();
                    break;
                    
                case DownloadState.VerifyFailed:
                    ActionButtonText = LanguageManager.Instance["Common_TextRetry"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_VerifyFailed"];
                    IsError = true;
                    ClearSpeedInfo();
                    RequestFlashTaskbar?.Invoke();
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
        }

        private readonly IDownloadService _downloadService;
        private readonly IContentDialogService _contentDialogService;
        private readonly Func<SimpleContentDialogCreateOptions, Task<ContentDialogResult>> _dialogPresenter;
        private readonly IServiceProvider _serviceProvider;
        private readonly IAppUpdaterService _appUpdaterService;
        private ReleaseInfo? _releaseInfo;
        private IUpdateService? _handler;
        private CancellationTokenSource? _cts;
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
