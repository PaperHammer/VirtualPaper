using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Feedback;
using VirtualPaper.UIComponent.Utils.Extensions;

namespace VirtualPaper.UIComponent.Context {
    /// <summary>
    /// 加载控制上下文 - 专门处理加载相关的逻辑
    /// </summary>
    public partial class LoadingContext : ObservableObject {
        /// <summary>
        /// 关联的加载控件
        /// </summary>
        public Loading? LoadingControl => _loadingReference.TryGetTarget(out var loading) ? loading : null;

        public bool IsValid { get; private set; }

        #region dependency properties
        private bool _cancelEnable;
        public bool CancelEnable {
            get { return _cancelEnable; }
            set { _cancelEnable = value; OnPropertyChanged(); }
        }

        private bool _progressbarEnable;
        public bool ProgressbarEnable {
            get { return _progressbarEnable; }
            set { _progressbarEnable = value; OnPropertyChanged(); }
        }

        private CancellationTokenSource[] _ctsTokens = [];
        public CancellationTokenSource[] CtsTokens {
            get { return _ctsTokens; }
            set { _ctsTokens = value; OnPropertyChanged(); }
        }

        private int _totalValue;
        public int TotalValue {
            get { return _totalValue; }
            set { _totalValue = value; OnPropertyChanged(); }
        }

        private int _curValue;
        public int CurValue {
            get { return _curValue; }
            set { _curValue = value; OnPropertyChanged(); }
        }

        private Visibility _isVisible = Visibility.Collapsed;
        public Visibility IsVisible {
            get { return _isVisible; }
            set { _isVisible = value; OnPropertyChanged(); }
        }
        #endregion

        public LoadingContext(Loading loadingControl) {
            _loadingReference = new WeakReference<Loading>(loadingControl);
            IsValid = LoadingControl != null;
        }

        #region loading actions
        /// <summary>
        /// 显示加载状态
        /// </summary>
        public void ShowLoading(bool showProgressBar = false) {
            if (_isShowing) return;

            _isShowing = true;
            LoadingControl?.Show(showProgressBar);
        }

        /// <summary>
        /// 隐藏加载状态
        /// </summary>
        public void HideLoading() {
            LoadingControl?.Hide();
            _isShowing = false;
        }

        /// <summary>
        /// 更新进度
        /// </summary>
        public void UpdateProgress(int current, int total) {
            LoadingControl?.UpdateProgress(current, total);
        }

        /// <summary>
        /// 设置取消令牌
        /// </summary>
        public void SetCancellationToken(CancellationTokenSource[] cts) {
            LoadingControl?.SetCancellation(cts);
        }

        /// <summary>
        /// 取消所有操作
        /// </summary>
        public void CancelAllOperations() {
            if (LoadingControl?.CtsTokens != null) {
                foreach (var cts in LoadingControl.CtsTokens) {
                    cts?.Cancel();
                }
            }
        }
        #endregion

        #region loading async actions
        /// <summary>
        /// 执行异步操作并自动管理加载状态
        /// </summary>
        public async Task ExecuteWithLoadingAsync(
            Func<Task> operation,
            bool showProgress = false,
            CancellationTokenSource[]? cts = null) {
            if (LoadingControl == null) {
                await operation();
                return;
            }

            try {
                ShowLoading(showProgress);
                if (cts != null) SetCancellationToken(cts);

                await operation();
            }
            finally {
                HideLoading();
            }
        }

        /// <summary>
        /// 执行带返回值的异步操作
        /// </summary>
        public async Task<T> ExecuteWithLoadingAsync<T>(
            Func<Task<T>> operation,
            bool showProgress = false,
            CancellationTokenSource[]? cts = null) {
            if (LoadingControl == null) {
                return await operation();
            }

            try {
                ShowLoading(showProgress);
                if (cts != null) SetCancellationToken(cts);

                return await operation();
            }
            finally {
                HideLoading();
            }
        }

        /// <summary>
        /// 执行长时间运行的操作，支持进度更新
        /// </summary>
        public async Task ExecuteLongRunningOperationAsync(
            Func<Action<int, int>, Task> operation,
            int totalSteps = 100) {
            if (LoadingControl == null) {
                await operation((current, total) => { });
                return;
            }

            try {
                ShowLoading(true);
                UpdateProgress(0, totalSteps);

                await operation((current, total) => {
                    UpdateProgress(current, total);
                });
            }
            finally {
                HideLoading();
            }
        }
        #endregion

        #region loading progress track
        /// <summary>
        /// 开始进度跟踪
        /// </summary>
        public IDisposable StartProgressTracking(int totalSteps = 100) {
            return new ProgressTracker(this, totalSteps);
        }

        /// <summary>
        /// 逐步更新进度
        /// </summary>
        public void StepProgress(int step = 1) {
            if (LoadingControl != null) {
                var newValue = LoadingControl.CurValue + step;
                if (newValue <= LoadingControl.TotalValue) {
                    UpdateProgress(newValue, LoadingControl.TotalValue);
                }
            }
        }

        /// <summary>
        /// 重置进度
        /// </summary>
        public void ResetProgress(int totalValue = 100) {
            UpdateProgress(0, totalValue);
        }

        /// <summary>
        /// 获取当前进度百分比
        /// </summary>
        public double GetProgressPercentage() {
            if (LoadingControl == null || LoadingControl.TotalValue <= 0)
                return 0;

            return (double)LoadingControl.CurValue / LoadingControl.TotalValue * 100;
        }
        #endregion

        #region inner
        /// <summary>
        /// 进度跟踪器
        /// </summary>
        private class ProgressTracker : IDisposable {
            private readonly LoadingContext _context;
            private readonly int _totalSteps;

            public ProgressTracker(LoadingContext context, int totalSteps) {
                _context = context;
                _totalSteps = totalSteps;

                _context.ShowLoading(true);
                _context.ResetProgress(_totalSteps);
            }

            public void Step(int step = 1) {
                _context.StepProgress(step);
            }

            public void Dispose() {
                _context.HideLoading();
            }
        }
        #endregion

        private readonly WeakReference<Loading> _loadingReference;
        private bool _isShowing;
    }
}