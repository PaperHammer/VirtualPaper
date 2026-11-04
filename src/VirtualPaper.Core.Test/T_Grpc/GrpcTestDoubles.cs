using Grpc.Core;

namespace VirtualPaper.Core.Test.T_Grpc;

internal sealed class RecordingStreamWriter<T> : IServerStreamWriter<T> {
    public WriteOptions? WriteOptions { get; set; }
    public IReadOnlyList<T> Items {
        get {
            lock (_items) {
                return _items.ToArray();
            }
        }
    }

    public Task WriteAsync(T message) {
        lock (_items) {
            _items.Add(message);
        }
        return Task.CompletedTask;
    }

    public void Clear() {
        lock (_items) {
            _items.Clear();
        }
    }

    private readonly List<T> _items = [];
}

internal sealed class ChannelStreamReader<T> : IAsyncStreamReader<T> {
    public T Current { get; private set; } = default!;

    public ValueTask WriteAsync(T item) => _channel.Writer.WriteAsync(item);
    public void Complete(Exception? error = null) => _channel.Writer.Complete(error);

    public async Task<bool> MoveNext(CancellationToken cancellationToken) {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken)) {
            if (_channel.Reader.TryRead(out var item)) {
                Current = item;
                return true;
            }
        }
        return false;
    }

    private readonly System.Threading.Channels.Channel<T> _channel =
        System.Threading.Channels.Channel.CreateUnbounded<T>();
}

internal sealed class FailingStreamWriter<T>(int successfulWritesBeforeFailure) : IServerStreamWriter<T> {
    public WriteOptions? WriteOptions { get; set; }
    public int WriteAttempts { get; private set; }
    public int SuccessfulWrites { get; private set; }

    public Task WriteAsync(T message) {
        WriteAttempts++;
        if (SuccessfulWrites >= successfulWritesBeforeFailure) {
            throw new IOException("client disconnected");
        }

        SuccessfulWrites++;
        return Task.CompletedTask;
    }
}

internal sealed class TestServerCallContext(CancellationToken cancellationToken) : ServerCallContext {
    protected override string MethodCore => "test";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "test";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore { get; } = [];
    protected override CancellationToken CancellationTokenCore => cancellationToken;
    protected override Metadata ResponseTrailersCore { get; } = [];
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore { get; } = new("test", []);
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        => throw new NotSupportedException();
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => Task.CompletedTask;
}
