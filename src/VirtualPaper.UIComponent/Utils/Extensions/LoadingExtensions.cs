using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.UIComponent.Feedback;

namespace VirtualPaper.UIComponent.Utils.Extensions {
    public static class LoadingExtensions {
        /// <summary>
        /// 显示加载状态
        /// </summary>
        public static void Show(this Loading loading, bool showProgressBar = false) {
            if (loading == null) return;

            CrossThreadInvoker.InvokeOnUIThread(() => {
                loading.ProgressbarEnable = showProgressBar;
                loading.Visibility = Visibility.Visible;
            });
        }

        /// <summary>
        /// 隐藏加载状态
        /// </summary>
        public static void Hide(this Loading loading) {
            if (loading == null || loading.Visibility == Visibility.Collapsed) return;

            CrossThreadInvoker.InvokeOnUIThread(() => {
                loading.Visibility = Visibility.Collapsed;
                if (loading.CtsTokens != null) {
                    foreach (var cts in loading.CtsTokens) {
                        cts.Dispose();
                    }
                }
                loading.CtsTokens = null;
                loading.CancelEnable = false;
            });
        }

        /// <summary>
        /// 更新进度
        /// </summary>
        public static void UpdateProgress(this Loading loading, int current, int total) {
            if (loading == null) return;

            CrossThreadInvoker.InvokeOnUIThread(() => {
                loading.CurValue = current;
                loading.TotalValue = total;
            });
        }

        /// <summary>
        /// 设置取消令牌
        /// </summary>
        public static void SetCancellation(this Loading loading, CancellationTokenSource[] cts) {
            if (loading == null) return;

            CrossThreadInvoker.InvokeOnUIThread(() => {
                loading.CtsTokens = cts;
                loading.CancelEnable = true;
            });
        }

        /// <summary>
        /// 执行异步操作并自动管理加载状态
        /// </summary>
        public static async Task ExecuteAsync(
            this Loading loading,
            Func<Task> operation,
            bool showProgress = false,
            CancellationTokenSource[]? cts = null) {
            if (loading == null) return;

            try {
                loading.Show(showProgress);
                if (cts != null) loading.SetCancellation(cts);

                await operation();
            }
            finally {
                loading.Hide();
            }
        }

        /// <summary>
        /// 执行带返回值的异步操作
        /// </summary>
        public static async Task<T> ExecuteAsync<T>(
            this Loading loading,
            Func<Task<T>> operation,
            bool showProgress = false,
            CancellationTokenSource[]? cts = null) {
            if (loading == null) return default!;

            try {
                loading.Show(showProgress);
                if (cts != null) loading.SetCancellation(cts);

                return await operation();
            }
            finally {
                loading.Hide();
            }
        }
    }
}
