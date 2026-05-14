using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.IntelligentPanel.Models {
    /// <summary>
    /// 风格迁移任务的 UI 表示层，包装 StyleTransferData 并提供绑定属性
    /// </summary>
    public partial class StyleTransferTaskItem : ObservableObject {
        public StyleTransferData Data { get; }
        public Guid Id => Data.Id;
        public CancellationTokenSource Cts { get; } = new();

        #region data
        public string SourceFilePath => Data.SourceFilePath;
        public string SourceFileSize => Data.SourceFileSize;
        public string SourceFileExt => Data.SourceFileExt;
        public string SourceResolution => $"{Data.Width} * {Data.Height}";

        public string StyleFilePath => Data.StyleFilePath;
        public string? StyleName => Data.StyleName;
        public string StyleFileSize => Data.StyleFileSize;
        public string StyleFileExt => Data.StyleFileExt;

        public string? ResultFilePath => Data.ResultFilePath;
        public string? ResultResolution => Data.ResultResolution;
        public string? ResultFileSize => Data.ResultFileSize;
        public string? ResultFileExt => Data.ResultFileExt;
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
            TaskStatus.Created => "已创建",
            TaskStatus.WaitingToRun => "排队中",
            TaskStatus.Running => "处理中...",
            TaskStatus.RanToCompletion => "已完成",
            TaskStatus.Faulted => "失败",
            _ => "未知"
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

        public StyleTransferTaskItem(StyleTransferData data) {
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
