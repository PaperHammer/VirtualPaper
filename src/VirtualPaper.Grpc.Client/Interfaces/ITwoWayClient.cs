using VirtualPaper.Grpc.Service.TwoWay;

namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface ITwoWayClient : IDisposable {
        event EventHandler<TwoWayMessage>? MessageReceived;
        Task SendMessageAsync(TwoWayMessage message);
    }
}
