using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.IntelligentPanel.Models {
    public partial class SuperResolutionTaskItem : ObservableObject {
        public SuperResolutionData Data { get; }
        public Guid Id => Data.Id;
        public CancellationTokenSource Cts { get; } = new();

        #region data
        public string SourceFilePath => Data.SourceFilePath;
        public string SourceFileSize => Data.SourceFileSize;
        public string SourceFileExt => Data.SourceFileExt;
        public string SourceResolution => $"{Data.Width} * {Data.Height}";

        public string? ResultFilePath => Data.ResultFilePath;
        public string? ResultResolution => $"{Data.TargetWidth} * {Data.TargetHeight}";
        public string? ResultFileSize => Data.ResultFileSize;
        public string? ResultFileExt => Data.SourceFileExt;

        public int Magnification => Data.Magnification;
        public string ModeText => Data.Mode switch {
            EnhanceMode.QualityRestore => LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Enhance_QualityRestore)),
            EnhanceMode.SuperResolution => $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Enhance_SuperResolution))} x{Magnification}",
            _ => string.Empty
        };
        #endregion

        #region task status
        private TaskStatus _status = TaskStatus.Created;
        public TaskStatus Status {
            get => _status;
            set {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsPending));
                OnPropertyChanged(nameof(IsProcessing));
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(IsFailed));

                UpdateProgressBarState();
                NotifyCommandsCanExecuteChanged();
            }
        }

        public string StatusText => Status switch {
            TaskStatus.Created => LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Task_Status_Completed)),
            TaskStatus.WaitingToRun => LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Task_Status_Queue)),
            TaskStatus.Running => LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Task_Status_Processing)),
            TaskStatus.RanToCompletion => LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Task_Status_Completed)),
            TaskStatus.Faulted => LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Task_Status_Failed)),
            _ => LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Task_Status_Unknown))
        };

        public bool IsPending => Status is TaskStatus.Created or TaskStatus.WaitingToRun;
        public bool IsProcessing => Status == TaskStatus.Running;
        public bool IsCompleted => Status == TaskStatus.RanToCompletion;
        public bool IsFailed => Status == TaskStatus.Faulted;
        #endregion

        #region progressBar & status color
        private bool _isIndeterminate;
        public bool IsIndeterminate {
            get => _isIndeterminate;
            set { if (_isIndeterminate == value) return; _isIndeterminate = value; OnPropertyChanged(); }
        }

        private bool _isShowError;
        public bool IsShowError {
            get => _isShowError;
            set { if (_isShowError == value) return; _isShowError = value; OnPropertyChanged(); }
        }

        private double _progress;
        public double Progress {
            get => _progress;
            set { if (_progress == value) return; _progress = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 统一的状态颜色，同时用于状态文字和进度条前景
        /// ARGB 字节数组，由转换器转为 SolidColorBrush
        /// </summary>
        private byte[] _statusColor = _blueColor;
        public byte[] StatusColor {
            get => _statusColor;
            set { if (_statusColor == value) return; _statusColor = value; OnPropertyChanged(); }
        }

        // ARGB 格式
        private static readonly byte[] _blueColor = [255, 0, 120, 212];     // #FF0078D4 - 默认/运行中
        private static readonly byte[] _greenColor = [255, 14, 161, 19];    // #FF0EA113 - 已完成
        private static readonly byte[] _redColor = [255, 209, 52, 56];      // #FFD13438 - 失败
        #endregion

        #region commands
        public ICommand? PreviewCommand { get; set; }
        public ICommand? ImportCommand { get; set; }
        public ICommand? SaveCommand { get; set; }
        public ICommand? RemoveCommand { get; set; }
        #endregion

        public SuperResolutionTaskItem(SuperResolutionData data) {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        private void NotifyCommandsCanExecuteChanged() {
            (PreviewCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ImportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void UpdateProgressBarState() {
            switch (Status) {
                case TaskStatus.Running:
                    IsIndeterminate = true;
                    IsShowError = false;
                    Progress = 0;
                    StatusColor = _blueColor;
                    break;

                case TaskStatus.RanToCompletion:
                    IsIndeterminate = false;
                    IsShowError = false;
                    Progress = 100;
                    StatusColor = _greenColor;
                    break;

                case TaskStatus.Faulted:
                    IsIndeterminate = false;
                    IsShowError = true;
                    Progress = 100;
                    StatusColor = _redColor;
                    break;

                default: // Created, WaitingToRun
                    IsIndeterminate = false;
                    IsShowError = false;
                    Progress = 0;
                    StatusColor = _blueColor;
                    break;
            }
        }

        /// <summary>
        /// 数据完成后刷新 UI 绑定
        /// </summary>
        public void NotifyResultChanged() {
            OnPropertyChanged(nameof(ResultFilePath));
            OnPropertyChanged(nameof(ResultResolution));
            OnPropertyChanged(nameof(ResultFileSize));
            OnPropertyChanged(nameof(ResultFileExt));
        }
    }
}
