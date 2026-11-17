using System.Diagnostics;

namespace VirtualPaper.UIOperationTest.Infrastructure;

// A provider can disappear while a native picker is opening or navigating.
// Retry the observation, never the user action that may already have succeeded.
internal static class UiOperationWait {
    internal static T Until<T>(Func<T> read, Predicate<T> ready, string operation,
        Func<Exception, bool>? transient = null, TimeSpan? timeout = null) {
        var timer = Stopwatch.StartNew();
        Exception? lastError = null;
        do {
            try {
                T value = read();
                if (ready(value)) return value;
            }
            catch (Exception ex) when (transient?.Invoke(ex) == true) { lastError = ex; }
            Thread.Sleep(20);
        } while (timer.Elapsed < (timeout ?? TimeSpan.FromSeconds(10)));
        throw new TimeoutException(operation + " did not complete", lastError);
    }
}
