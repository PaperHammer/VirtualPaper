using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using VirtualPaper.lang;
using VirtualPaper.Models;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.Services;
using VirtualPaper.Services.Interfaces;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace VirtualPaper.ViewModels {
    public class AppUpdaterWindowViewModel : ObservableObject, IWindowParameterReceiver {
        public static Uri AppIconPath => new Uri("pack://application:,,,/Resources/appicon_96.png");

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

        private string _speedText = string.Empty;
        public string SpeedText {
            get => _speedText;
            set { _speedText = value; OnPropertyChanged(); }
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

        public ICommand? ActionCommand { get; }

        private DownloadState _currentState;
        public DownloadState CurrentState {
            get { return _currentState; }
            set {
                _currentState = value;
                ActionButtonEnable = _currentState != DownloadState.Verifying;
                IsIndeterminate = _currentState == DownloadState.Verifying;
                UpdateUIByState();
            }
        }

        public AppUpdaterWindowViewModel(
            IDownloadService downloadService,
            IContentDialogService contentDialogService) {
            _downloadService = downloadService;
            _contentDialogService = contentDialogService;

            ActionCommand = new RelayCommand(OnActionCommand);
        }

        public void ReceiveParameter(object? parameter) {
            if (parameter is AppUpdateInfo info) {
                _downloadUri = info.DownloadUri;
                _shaUri = info.SHAUri;
                Version = info.Version;
                ChangeLog = info.ChangeLog;
                CurrentState = DownloadState.Ready;
                _savePath = Path.Combine(Path.GetTempPath(), Path.GetFileName(_downloadUri.LocalPath));
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
        private async void OnActionCommand() {
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
            if (_downloadUri == null)
                return;

            DeleteFile();
            _cts = new CancellationTokenSource();
            CurrentState = DownloadState.Downloading;

            try {
                _sha256 = await _downloadService.DownloadShaTxtAsync(_shaUri, _cts.Token);

                await foreach (var progress in _downloadService.DownloadAsync(_downloadUri, _savePath, _cts.Token)) {
                    Progress = progress.Percent;
                    SpeedText = $"{progress.Speed:F2} MB/s | 剩余时间：{progress.Remaining:hh\\:mm\\:ss}";
                }

                await VerifyAsync();
            }
            catch (OperationCanceledException) {
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
                DeleteFile();
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

        private async void InstallUpdate() {
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
                    SpeedText = $"-- MB/s | {LanguageManager.Instance["AppUpdater_SpeedText_Ready"]}：--:--";
                    Progress = 0;
                    break;

                case DownloadState.Downloading:
                    ActionButtonText = LanguageManager.Instance["AppUpdater_ActionButtonText_Downloading"]; ;
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_Downloading"];
                    break;

                case DownloadState.Paused:
                    ActionButtonText = LanguageManager.Instance["AppUpdater_ActionButtonText_Paused"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_Paused"];
                    break;

                case DownloadState.Verifying:
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_Verifying"];
                    SpeedText = string.Empty;
                    break;

                case DownloadState.Completed:
                    ActionButtonText = LanguageManager.Instance["AppUpdater_ActionButtonText_Completed"];
                    StatusText = LanguageManager.Instance["AppUpdater_StatusText_Completed"];
                    SpeedText = string.Empty;
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
                    SpeedText = string.Empty;
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
            DeleteFile();
        }

        private void DeleteFile() {
            try {
                if (File.Exists(_savePath))
                    File.Delete(_savePath);
            }
            catch {
            }
        }

        private readonly IDownloadService _downloadService;
        private readonly IContentDialogService _contentDialogService;
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
