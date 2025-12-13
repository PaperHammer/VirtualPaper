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
    public partial class ArcLoadingContext : ObservableObject {
        /// <summary>
        /// 关联的加载控件
        /// </summary>
        public bool IsValid => LoadingControl != null;
        public bool IsLoading => LoadingControl?.Visibility == Visibility.Visible;
        public Task? LoadingFinished => _loadingFinishedTcs?.Task;
        private Loading? LoadingControl => _loadingReference.TryGetTarget(out var loading) ? loading : null;

        public ArcLoadingContext(ArcPageContext arcPageContext, Loading loadingControl) {
            _loadingReference = new WeakReference<Loading>(loadingControl);
            _arcPageContext = arcPageContext;
        }

        public async Task RunAsync(
            Func<CancellationToken, Task> operation,
            CancellationTokenSource? cts = null,
            bool showProgress = false) {
            if (!IsValid) {
                await operation(default);
                return;
            }

            CancellationToken token = cts?.Token ?? CancellationToken.None;

            EnterLoading(showProgress, cts);

            IDisposable blockingHandle = _arcPageContext.Blocking.Add(token);            

            await operation(token);

            LeaveLoading(blockingHandle);
        }

        public async Task RunWithProgressAsync(
            Func<CancellationToken, Action<int, int>, Task> operation,
            int total,
            CancellationTokenSource? cts = null) {
            await RunAsync(
                operation: async token => {
                    if (!IsValid) {
                        await operation(token, (_, _) => { });
                        return;
                    }

                    LoadingControl!.ProgressbarEnable = true;
                    LoadingControl.TotalValue = total;
                    LoadingControl.CurValue = 0;

                    await operation(token, (cur, tot) => {
                        LoadingControl.CurValue = cur;
                        LoadingControl.TotalValue = tot;
                    });
                },
                cts: cts,
                showProgress: true);
        }

        private void EnterLoading(bool showProgress, CancellationTokenSource? cts) {
            if (!IsValid) return;

            if (cts != null) {
                LoadingControl!.CtsToken = cts;
                LoadingControl.CancelEnable = true;
            }
            LoadingControl!.ProgressbarEnable = showProgress;
            LoadingControl.Visibility = Visibility.Visible;
        }

        private void LeaveLoading(IDisposable blockingHandle) {
            if (!IsValid) return;

            LoadingControl!.Visibility = Visibility.Collapsed;
            LoadingControl.CurValue = 0;
            LoadingControl.TotalValue = 0;
            LoadingControl.ProgressbarEnable = false;
            LoadingControl.CtsToken = null;
            blockingHandle.Dispose();
        }

        private readonly WeakReference<Loading> _loadingReference;
        private readonly ArcPageContext _arcPageContext;
        private TaskCompletionSource? _loadingFinishedTcs;
    }
}