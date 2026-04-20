using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Launcher.Models;
using VirtualPaper.Launcher.Services.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.Launcher.ViewModels {
    public partial class HomePageViewModel : ObservableObject {
        private Brush _btnTextForeground = new SolidColorBrush(Colors.White);
        public Brush BtnTextForeground {
            get { return _btnTextForeground; }
            set { _btnTextForeground = value; OnPropertyChanged(); }
        }

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

        public HomePageViewModel(
            IDownloadService downloadService) {
            _downloadService = downloadService;

            ActionCommand = new RelayCommand(OnActionCommand);
            CurrentState = DownloadState.Ready;
            UpdateUIByState();
        }

        private void InitEvent() {
            ArcThemeUtil.AppThemeChanged += (s, e) => {
                RefreshWpTitleForeground();
            };
        }

        internal void RefreshWpTitleForeground() {
            var color = ArcThemeUtil.GetFormatMainWindowTheme() == AppTheme.Light ? Colors.White : Colors.Black;
            BtnTextForeground = new SolidColorBrush(color);
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
            var res = await GlobalDialogUtils.ShowDialogAsync(
                content: CurrentState == DownloadState.Downloading ? LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_Update_DescriptionCancelQuestion_ForDownloading)) : LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_Update_DescriptionCancelQuestion_ForCompleted)),
                title: LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_Update_TitleCancelQuestion)),
                primaryBtnText: LanguageUtil.GetI18n(nameof(Consts.I18n.Text_Confirm)),
                secondaryBtnText: LanguageUtil.GetI18n(nameof(Consts.I18n.Text_Cancel))
            );

            return res == DialogResult.Primary;
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
                    SpeedText = $"{progress.Speed:F2} MB/s | {LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_SpeedText_Ready))}：{progress.Remaining:hh\\:mm\\:ss}";
                }

                await VerifyAsync();
            }
            catch (OperationCanceledException) {
                if (CurrentState != DownloadState.Paused)
                    CurrentState = DownloadState.Paused;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<HomePageViewModel>().Error(ex);
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
                ArcLog.GetLogger<HomePageViewModel>().Error("Install for silent updating failed", ex);
                CurrentState = DownloadState.Completed;
                await GlobalDialogUtils.ShowDialogAsync(
                    content: LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_Update_ExceptionAppUpdateFail)),
                    title: LanguageUtil.GetI18n(nameof(Consts.I18n.Text_Error)),
                    primaryBtnText: LanguageUtil.GetI18n(nameof(Consts.I18n.Text_Confirm)));
            }
        }
        #endregion

        #region UI State Mapping
        private void UpdateUIByState() {
            switch (CurrentState) {
                case DownloadState.Ready:
                    ActionButtonText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_ActionButtonText_Ready));
                    StatusText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_StatusText_Ready));
                    SpeedText = $"-- MB/s | {LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_SpeedText_Ready))}：--:--";
                    Progress = 0;
                    break;

                case DownloadState.Downloading:
                    ActionButtonText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_ActionButtonText_Downloading)); ;
                    StatusText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_StatusText_Downloading));
                    break;

                case DownloadState.Paused:
                    ActionButtonText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_ActionButtonText_Paused));
                    StatusText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_StatusText_Paused));
                    break;

                case DownloadState.Verifying:
                    StatusText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_StatusText_Verifying));
                    SpeedText = string.Empty;
                    break;

                case DownloadState.Completed:
                    ActionButtonText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_ActionButtonText_Completed));
                    StatusText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_StatusText_Completed));
                    SpeedText = string.Empty;
                    break;

                case DownloadState.DownloadFailed:
                    ActionButtonText = LanguageUtil.GetI18n(nameof(Consts.I18n.Text_Retry));
                    StatusText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_StatusText_DownloadFailed));
                    break;

                case DownloadState.VerifyFailed:
                    ActionButtonText = LanguageUtil.GetI18n(nameof(Consts.I18n.Text_Retry));
                    StatusText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_StatusText_VerifyFailed));
                    break;

                case DownloadState.Installing:
                    ActionButtonText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_ActionButtonText_Installing));
                    StatusText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_StatusText_Installing));
                    SpeedText = string.Empty;
                    break;

                case DownloadState.Installed:
                    ActionButtonText = LanguageUtil.GetI18n(nameof(Consts.I18n.Text_Close));
                    StatusText = LanguageUtil.GetI18n(nameof(Consts.I18n.AppUpdater_StatusText_Installed));
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
