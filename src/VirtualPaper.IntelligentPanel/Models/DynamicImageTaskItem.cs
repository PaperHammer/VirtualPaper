using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.IntelligentPanel.Models {
    public sealed class DynamicImageTaskItem : ObservableObject {
        public DynamicImageData Data { get; }
        public Guid Id => Data.Id;
        public CancellationTokenSource Cts { get; } = new();

        public string SourceFilePath => Data.SourceFilePath;
        public string SourceFileSize => Data.SourceFileSize;
        public string SourceResolution => $"{Data.Width} × {Data.Height}";
        public string QualityText => Data.Quality == DynamicImageQuality.High
            ? LanguageUtil.GetI18n("Intelligent_Dynamic_High")
            : LanguageUtil.GetI18n("Intelligent_Dynamic_Balanced");
        public string? PreviewFilePath => Data.PreviewFilePath;
        public string? ResultDirectory => Data.ResultDirectory;
        public string? ResultSize => Data.ResultSize;
        public string ResultSummary => IsCompleted
            ? string.Format(
                LanguageUtil.GetI18n("Intelligent_Dynamic_ResultSummary"),
                Data.ObjectCount,
                Data.LayerCount,
                Data.ProcessingTime.TotalSeconds)
            : string.Empty;

        private TaskStatus _status = TaskStatus.Created;
        public TaskStatus Status {
            get => _status;
            set {
                if (_status == value)
                    return;
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsProcessing));
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(ResultSummary));
                NotifyCommands();
            }
        }

        public string StatusText => Status switch {
            TaskStatus.WaitingToRun => LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Task_Status_Queue)),
            TaskStatus.Running => LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Task_Status_Processing)),
            TaskStatus.RanToCompletion => LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Task_Status_Completed)),
            TaskStatus.Faulted => LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Task_Status_Failed)),
            _ => LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Task_Status_Unknown))
        };
        public bool IsProcessing => Status == TaskStatus.Running;
        public bool IsCompleted => Status == TaskStatus.RanToCompletion;
        public bool IsFailed => Status == TaskStatus.Faulted;

        public ICommand? PreviewCommand { get; set; }
        public ICommand? ExportCommand { get; set; }
        public ICommand? OpenFolderCommand { get; set; }
        public ICommand? RemoveCommand { get; set; }

        public DynamicImageTaskItem(DynamicImageData data) =>
            Data = data ?? throw new ArgumentNullException(nameof(data));

        public void NotifyResultChanged() {
            OnPropertyChanged(nameof(PreviewFilePath));
            OnPropertyChanged(nameof(ResultDirectory));
            OnPropertyChanged(nameof(ResultSize));
            OnPropertyChanged(nameof(ResultSummary));
        }

        private void NotifyCommands() {
            (PreviewCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}
