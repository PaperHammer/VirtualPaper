namespace VirtualPaper.Common.Utils.IPC.Interfaces {
    public interface IIpcObserver {
        void Register(object subscriber);
        void Unregister(object subscriber);
        ValueTask Dispatch(IpcMessage message);
    }
}
