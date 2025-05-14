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
    }

    public static class CrossThreadInvoker {
        private static IUiSynchronizationContext? _uiSyncContext;

        public static void Initialize(IUiSynchronizationContext syncContext) {
            _uiSyncContext = syncContext;
        }

        public static void InvokeOnUiThread(Action action, bool synchronous = false) {
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
    }
}
