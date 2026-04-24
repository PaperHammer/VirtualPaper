namespace VirtualPaper.Common.Utils.ThreadContext {
    public class UiSynchronizationContext : IUiSynchronizationContext {
        public UiSynchronizationContext() {
            Current = SynchronizationContext.Current ?? throw new InvalidOperationException("No synchronization context available.");
        }

        public override void Post(Action action) {
            Current?.Post(_ => action(), null);
        }

        public override void Send(Action action) {
            Current?.Send(_ => action(), null);
        }

        /// <summary>
        /// 将异步操作 Post 到 UI 线程，并返回可等待的 Task。
        /// </summary>
        public override Task PostAsync(Func<Task> asyncAction) {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            Current?.Post(_ => {
                try {
                    asyncAction().ContinueWith(t => {
                        if (t.IsFaulted)
                            tcs.SetException(t.Exception!.InnerExceptions);
                        else if (t.IsCanceled)
                            tcs.SetCanceled();
                        else
                            tcs.SetResult();
                    }, TaskScheduler.Default);
                }
                catch (Exception ex) {
                    tcs.SetException(ex);
                }
            }, null);

            return tcs.Task;
        }

        /// <summary>
        /// 将有返回值的异步操作 Post 到 UI 线程，并返回可等待的 Task{T}。
        /// </summary>
        public override Task<T> PostAsync<T>(Func<Task<T>> asyncAction) {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            Current?.Post(_ => {
                try {
                    asyncAction().ContinueWith(t => {
                        if (t.IsFaulted)
                            tcs.SetException(t.Exception!.InnerExceptions);
                        else if (t.IsCanceled)
                            tcs.SetCanceled();
                        else
                            tcs.SetResult(t.Result);
                    }, TaskScheduler.Default);
                }
                catch (Exception ex) {
                    tcs.SetException(ex);
                }
            }, null);

            return tcs.Task;
        }
    }

    public static class CrossThreadInvoker {
        private static IUiSynchronizationContext? _uiSyncContext;

        public static void Initialize(IUiSynchronizationContext syncContext) {
            _uiSyncContext = syncContext;
        }

        public static void InvokeOnUIThread(Action action, bool synchronous = false) {
            if (_uiSyncContext == null) {
                throw new InvalidOperationException("UI synchronization context is not initialized.");
            }

            if (SynchronizationContext.Current == _uiSyncContext.Current) {
                action(); 
            }
            else {
                if (synchronous) {
                    _uiSyncContext.Send(action);
                }
                else {
                    _uiSyncContext.Post(action);
                }
            }
        }

        /// <summary>
        /// 将异步操作调度到 UI 线程执行，并等待其完成。
        /// 可安全地从后台线程 await。
        /// </summary>
        public static Task InvokeOnUIThreadAsync(Func<Task> asyncAction) {
            if (_uiSyncContext == null)
                throw new InvalidOperationException("UI synchronization context is not initialized.");

            // 已经在 UI 线程，直接执行
            if (SynchronizationContext.Current == _uiSyncContext.Current) {
                return asyncAction();
            }

            return _uiSyncContext.PostAsync(asyncAction);
        }

        /// <summary>
        /// 将有返回值的异步操作调度到 UI 线程执行。
        /// </summary>
        public static Task<T> InvokeOnUIThreadAsync<T>(Func<Task<T>> asyncAction) {
            if (_uiSyncContext == null)
                throw new InvalidOperationException("UI synchronization context is not initialized.");

            if (SynchronizationContext.Current == _uiSyncContext.Current) {
                return asyncAction();
            }

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _uiSyncContext.Current?.Post(_ => {
                try {
                    asyncAction()
                        .ContinueWith(t => {
                            if (t.IsFaulted)
                                tcs.SetException(t.Exception!.InnerExceptions);
                            else if (t.IsCanceled)
                                tcs.SetCanceled();
                            else
                                tcs.SetResult(t.Result);
                        }, TaskScheduler.Default);
                }
                catch (Exception ex) {
                    tcs.SetException(ex);
                }
            }, null);

            return tcs.Task;
        }
    }
}
