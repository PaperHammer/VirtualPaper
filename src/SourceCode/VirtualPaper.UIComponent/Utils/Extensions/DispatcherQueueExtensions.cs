using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace VirtualPaper.UIComponent.Utils.Extensions {
    public static class DispatcherQueueExtensions {
        /// <summary>
        /// 确保异步操作能够在正确的线程（通常是 UI 线程）上执行
        /// </summary>
        /// <param name="queue"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static Task EnqueueOrInvoke(this DispatcherQueue queue, Func<Task> action) {
            var tcs = new TaskCompletionSource<bool>();

            // 尝试立即执行（如果已经在正确的线程上）
            if (queue.HasThreadAccess) {
                try {
                    action().ContinueWith(t => tcs.SetResult(true));
                }
                catch (Exception ex) {
                    tcs.SetException(ex);
                }
            }
            else {
                // 否则排队等待执行
                queue.TryEnqueue(() => {
                    try {
                        action().ContinueWith(t => tcs.SetResult(true));
                    }
                    catch (Exception ex) {
                        tcs.SetException(ex);
                    }
                });
            }

            return tcs.Task;
        }
    }
}
