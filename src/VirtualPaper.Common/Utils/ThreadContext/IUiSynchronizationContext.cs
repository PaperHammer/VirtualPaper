namespace VirtualPaper.Common.Utils.ThreadContext {
    public abstract class IUiSynchronizationContext {
        public SynchronizationContext? Current { get; protected set; }
        public abstract void Post(Action action);
        public abstract  void Send(Action action);
    }
}
