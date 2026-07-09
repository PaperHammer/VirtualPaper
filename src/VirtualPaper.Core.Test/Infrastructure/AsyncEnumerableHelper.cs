using System.Runtime.CompilerServices;

namespace VirtualPaper.Core.Test.Infrastructure {
    internal static class AsyncEnumerableHelper {
        public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
            this IEnumerable<T> source,
            [EnumeratorCancellation] CancellationToken token = default) {
            foreach (var item in source) {
                token.ThrowIfCancellationRequested();
                yield return item;
            }
            await Task.CompletedTask;
        }
    }
}
