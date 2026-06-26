using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.lang;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.ViewModels {
    public class RestartUpdateWindowViewModel : ObservableObject {
        private string _statusText = string.Empty;
        public string StatusText {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private float _progress;
        public float Progress {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        private string _stageText = string.Empty;
        public string StageText {
            get => _stageText;
            set { _stageText = value; OnPropertyChanged(); }
        }

        private string _detailText = string.Empty;
        public string DetailText {
            get => _detailText;
            set { _detailText = value; OnPropertyChanged(); }
        }

        private bool _isCompleted;
        public bool IsCompleted {
            get => _isCompleted;
            set { _isCompleted = value; OnPropertyChanged(); }
        }

        private bool _hasError;
        public bool HasError {
            get => _hasError;
            set { _hasError = value; OnPropertyChanged(); }
        }

        private bool _isIndeterminate;
        public bool IsIndeterminate {
            get => _isIndeterminate;
            set { _isIndeterminate = value; OnPropertyChanged(); }
        }

        private string _postponeTip = string.Empty;
        public string PostponeTip {
            get => _postponeTip;
            set { _postponeTip = value; OnPropertyChanged(); }
        }

        public ICommand CloseCommand { get; }

        public RestartUpdateWindowViewModel() {
            CloseCommand = new RelayCommand(OnClose);
        }

        public event Action? CloseRequested;

        public async Task StartUpdateAsync(ReleaseInfo releaseInfo) {
            if (releaseInfo.Manifest == null || !releaseInfo.Manifest.IsRestartUpdate) {
                StatusText = LanguageManager.Instance[Constants.I18n.RestartUpdate_InvalidInfo];
                HasError = true;
                return;
            }

            var restartService = App.Services.GetRequiredService<IRestartUpdateService>();
            var progress = new Progress<RestartUpdateProgress>(OnProgressReported);

            StatusText = LanguageManager.Instance[Constants.I18n.RestartUpdate_Starting];
            var result = await restartService.ExecuteUpdateAsync(releaseInfo, progress);

            if (result.Success) {
                StatusText = LanguageManager.Instance[Constants.I18n.RestartUpdate_Completed];
                PostponeTip = LanguageManager.Instance[Constants.I18n.RestartUpdate_PostponeTip];
                IsCompleted = true;
            }
            else {
                StatusText = string.Format(LanguageManager.Instance[Constants.I18n.RestartUpdate_Failed], result.ErrorMessage);
                HasError = true;
            }
        }

        private void OnProgressReported(RestartUpdateProgress progress) {
            Progress = progress.Percent;
            DetailText = progress.Message;
            StageText = progress.Stage switch {
                RestartUpdateStage.Downloading => LanguageManager.Instance[Constants.I18n.RestartUpdate_Stage_Downloading],
                RestartUpdateStage.BackingUp => LanguageManager.Instance[Constants.I18n.RestartUpdate_Stage_BackingUp],
                RestartUpdateStage.Replacing => LanguageManager.Instance[Constants.I18n.RestartUpdate_Stage_Replacing],
                RestartUpdateStage.Completed => LanguageManager.Instance[Constants.I18n.RestartUpdate_Stage_Completed],
                RestartUpdateStage.Failed => LanguageManager.Instance[Constants.I18n.RestartUpdate_Stage_Failed],
                _ => ""
            };
        }

        private void OnClose() {
            CloseRequested?.Invoke();
        }
    }
}
