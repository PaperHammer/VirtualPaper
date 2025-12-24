namespace VirtualPaper.Common.Utils.IPC.Interfaces {
    public interface IIpcSubscribe<in T> where T : IpcMessage {
        ValueTask OnIpcAsync(T message);
    }
}
