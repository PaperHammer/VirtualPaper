using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace VirtualPaper.Common.Utils {
    public class AsyncLoader<T> {
        public AsyncLoader(int maxDegreeOfParallelism = 10, int channelCapacity = 100) {
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
            _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(channelCapacity) {
                FullMode = BoundedChannelFullMode.Wait
            });
        }

        public async IAsyncEnumerable<T> LoadItemsAsync(Func<IEnumerable<string>, ParallelOptions, ChannelWriter<T>, CancellationToken, Task> processItems, IEnumerable<string> items, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            // Start a producer task that processes items and sends them to the channel.
            var producerTask = Task.Run(async () => {
                try {
                    await processItems(items, new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism, CancellationToken = cancellationToken }, _channel.Writer, cancellationToken);
                }
                finally {
                    _channel.Writer.Complete();
                }
            }, cancellationToken);

            // Consume items from the channel as they become available.
            await foreach (var item in ConsumeChannel(_channel.Reader, cancellationToken)) {
                yield return item;
            }

            await producerTask;
        }

        private static async IAsyncEnumerable<T> ConsumeChannel(ChannelReader<T> reader, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            while (await reader.WaitToReadAsync(cancellationToken)) {
                while (reader.TryRead(out var item)) {
                    yield return item;
                }
            }
        }

        private readonly Channel<T> _channel;
        private readonly int _maxDegreeOfParallelism;
    }
}
