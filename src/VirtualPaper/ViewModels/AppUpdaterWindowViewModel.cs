using System.Diagnostics;
using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Cores.AppUpdate;
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

        public bool IsRestartUpdate { get; private set; }

        private DownloadState _currentState;
        public DownloadState CurrentState {
            get { return _currentState; }
            set {
                if (value == _currentState) return;

                _currentState = value;
                ActionButtonEnable = _currentState != DownloadState.Verifying;
                IsIndeterminate = _currentState == DownloadState.Verifying;
                UpdateUIByState();
            }
        }

        public AppUpdaterWindowViewModel(
            IDownloadService downloadService,
            IContentDialogService contentDialogService,
            IRestartUpdateService restartUpdateService) {
            _downloadService = downloadService;
            _contentDialogService = contentDialogService;
            _restartUpdateService = restartUpdateService;
        }

        public void ReceiveParameter(object? parameter) {
            if (parameter is ReleaseInfo info) {
                if (info.IsRestartUpdate) {
                    IsRestartUpdate = true;
                    _releaseInfo = info;                    
                }
                else {
                    _downloadUri = info.InstallerUri!;
                    _shaUri = info.InstallerShaUri!;
                    _savePath = Path.Combine(Constants.CommonPaths.InstallerCacheDir, Path.GetFileName(_downloadUri.LocalPath));
                }
                Version = $"{info.Version?.ToString()} (Build {info.AppBuild?.ToString()})";
                ChangeLog = info.Changelog ?? string.Empty;
                CurrentState = DownloadState.Ready;
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
                    InstallUpdate();
                    break;
            }
        }
        #endregion

        #region Download Logic
        private async Task StartDownloadAsync() {
            if (IsRestartUpdate) {
                await StartPluginsDownloadAsync();
                return;
            }

            if (_downloadUri == null)
                return;

            FileUtil.DeleteDirectoryContents(Constants.CommonPaths.InstallerCacheDir);
            _cts = new CancellationTokenSource();
            CurrentState = DownloadState.Downloading;

            try {
                _sha256 = await _downloadService.DownloadShaTxtAsync(_shaUri, _cts.Token);
                File.WriteAllText(_savePath + ".sha256", _sha256);

                await foreach (var progress in _downloadService.DownloadAsync(_downloadUri, _savePath, _cts.Token)) {
                    Progress = progress.Percent;
                    UpdateSpeedInfo(progress.Speed, progress.ReceivedBytes, progress.TotalBytes, progress.Remaining);
                }

                await VerifyAsync();
            }
            catch (OperationCanceledException) {
                FileUtil.DeleteDirectoryContents(Constants.CommonPaths.InstallerCacheDir);
                if (CurrentState != DownloadState.Paused)
                    CurrentState = DownloadState.Paused;
            }
            catch (Exception ex) {
                App.Log.Error(ex);
                CurrentState = DownloadState.DownloadFailed;
            }
        }

        private async Task StartPluginsDownloadAsync() {
            if (_releaseInfo == null)
                return;

            _cts = new CancellationTokenSource();
            CurrentState = DownloadState.Downloading;

            try {
                var progress = new Progress<DownloadProgress>(p => {
                    Progress = p.Percent;
                    UpdateSpeedInfo(p.Speed, p.ReceivedBytes, p.TotalBytes, p.Remaining);
                });

                var result = await _restartUpdateService.DownloadPendingAsync(_releaseInfo, progress, _cts.Token);

                if (!result.Success) {
                    CurrentState = DownloadState.DownloadFailed;
                    return;
                }

                CurrentState = DownloadState.Verifying;
                var verifyResult = await _restartUpdateService.VerifyAndSavePendingAsync(_releaseInfo, _cts.Token);

                if (!verifyResult.Success) {
                    CurrentState = DownloadState.VerifyFailed;
                    return;
                }

                CurrentState = DownloadState.Completed;
            }
            catch (OperationCanceledException) {
                FileUtil.RemoveDirectory(Constants.CommonPaths.PendingUpdatesDir);
                if (CurrentState != DownloadState.Paused)
                    CurrentState = DownloadState.Paused;
            }
            catch (Exception ex) {
                App.Log.Error(ex);
                CurrentState = DownloadState.DownloadFailed;
            }
        }

        private async Task VerifyAsync() {
            CurrentState = DownloadState.Verifying;
            var verified = await _downloadService.VerifyFileIntegrityAsync(_savePath, _sha256, _cts!.Token);

            if (!verified) {
                CurrentState = DownloadState.VerifyFailed;
                FileUtil.DeleteDirectoryContents(Constants.CommonPaths.InstallerCacheDir);
                return;
            }

            CurrentState = DownloadState.Completed;
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

        private async void InstallUpdate() {
            if (IsRestartUpdate) {
                return;
            }

            CurrentState = DownloadState.Installing;
            try {
                //run setup in silent mode.
                Process.Start(_savePath, "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS");
                //inno installer will auto retry, waiting for application exit.
                CurrentState = DownloadState.Installed;
                App.ShutDown();
            }
            catch (Exception ex) {
                App.Log.Error("Install for silent updating failed", ex);
                CurrentState = DownloadState.Completed;
                _ = await _contentDialogService.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions() {
                        Title = LanguageManager.Instance["Common_TextError"],
                        Content = LanguageManager.Instance["AppUpdater_Update_ExceptionAppUpdateFail"],
                        CloseButtonText = LanguageManager.Instance["Common_TextConfirm"],
                    }
                );
            }
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
                    ActionButtonText = IsRestartUpdate
                        ? LanguageManager.Instance["Common_TextConfirm"]
                        : LanguageManager.Instance["AppUpdater_ActionButtonText_Completed"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_Completed"];
                    ClearSpeedInfo();
                    if (IsRestartUpdate) {
                        _ = _contentDialogService.ShowSimpleDialogAsync(
                            new SimpleContentDialogCreateOptions() {
                                Title = LanguageManager.Instance["RestartUpdate_Close"],
                                Content = LanguageManager.Instance["RestartUpdate_PostponeTip"],
                                CloseButtonText = LanguageManager.Instance["Common_TextConfirm"],
                            }
                        );
                    }
                    break;

                case DownloadState.DownloadFailed:
                    ActionButtonText = LanguageManager.Instance["Common_TextRetry"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_DownloadFailed"];
                    break;
                    
                case DownloadState.VerifyFailed:
                    ActionButtonText = LanguageManager.Instance["Common_TextRetry"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_VerifyFailed"];
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
        private readonly IRestartUpdateService _restartUpdateService;
        private ReleaseInfo? _releaseInfo;
        private Uri _downloadUri = null!;
        private Uri _shaUri = null!;
        private CancellationTokenSource? _cts;
        private string _savePath = string.Empty;
        private string _sha256 = string.Empty;
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
