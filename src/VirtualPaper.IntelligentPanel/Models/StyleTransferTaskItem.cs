using System;
using System.Threading.Tasks;
using System.Windows.Input;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.IntelligentPanel.Models {
    /// <summary>
    /// 风格迁移任务的 UI 表示层，包装 StyleTransferData 并提供绑定属性
    /// </summary>
    public partial class StyleTransferTaskItem : ObservableObject {
        /// <summary>
        /// 底层数据
        /// </summary>
        public StyleTransferData Data { get; }

        public Guid Id => Data.Id;

        #region data
        public string SourceFilePath => Data.SourceFilePath;
        public string SourceFileSize => Data.SourceFileSize;
        public string SourceFileExt => Data.SourceFileExt;
        public string SourceResolution => $"{Data.Width} × {Data.Height}";

        public string StyleFilePath => Data.StyleFilePath;
        public string? StyleName => Data.StyleName;
        public string StyleFileSize => Data.StyleFileSize;
        public string StyleFileExt => Data.StyleFileExt;

        public string? ResultFilePath => Data.ResultFilePath;
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
                OnPropertyChanged(nameof(IsCanceled));
            }
        }

        public string StatusText => Status switch {
            TaskStatus.Created => "已创建",
            TaskStatus.WaitingToRun => "排队中",
            TaskStatus.Running => "处理中...",
            TaskStatus.RanToCompletion => "已完成",
            TaskStatus.Canceled => "已取消",
            TaskStatus.Faulted => "失败",
            _ => "未知"
        };

        public bool IsPending => Status is TaskStatus.Created or TaskStatus.WaitingToRun;
        public bool IsProcessing => Status == TaskStatus.Running;
        public bool IsCompleted => Status == TaskStatus.RanToCompletion;
        public bool IsFailed => Status == TaskStatus.Faulted;
        public bool IsCanceled => Status == TaskStatus.Canceled;
        #endregion

        #region progressBar
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

        private byte[] _foregroundColor = _defaultColor;
        public byte[] ForegroundColor {
            get => _foregroundColor;
            set { if (_foregroundColor == value) return; _foregroundColor = value; OnPropertyChanged(); }
        }

        // ARGB 格式
        private static readonly byte[] _defaultColor = [];               // 空 = 使用 ProgressBar 默认主题色
        private static readonly byte[] _greenColor = [255, 14, 161, 19]; // #FF0EA113
        private static readonly byte[] _redColor = [255, 209, 52, 56];   // #FFD13438
        #endregion

        #region commands
        public ICommand? PreviewCommand { get; set; }
        public ICommand? ImportCommand { get; set; }
        public ICommand? SaveCommand { get; set; }
        public ICommand? RemoveCommand { get; set; }
        public ICommand? CancelCommand { get; set; }
        #endregion

        public StyleTransferTaskItem(StyleTransferData data) {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// 数据完成后刷新 UI 绑定
        /// </summary>
        public void NotifyResultChanged() {
            OnPropertyChanged(nameof(ResultFilePath));
        }
    }
}
