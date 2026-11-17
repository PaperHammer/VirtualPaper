using System.Collections.Concurrent;
using VirtualPaper.PlayerWeb.Core.Utils;

namespace VirtualPaper.UI.Test.T_PlayerWeb;

[TestClass]
public class WebViewScriptExecutorTests {
    [TestMethod]
    public async Task States_CoalesceBurstUpdatesPerKeyWhileDispatchIsBusy() {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unblock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new ConcurrentQueue<string>();
        using var executor = new WebViewScriptExecutor(script => {
            received.Enqueue(script);
            if (script == "initial();") {
                entered.TrySetResult();
                return unblock.Task;
            }
            if (received.Count == 4) completed.TrySetResult();
            return Task.CompletedTask;
        });
        try {
            executor.EnqueueState("initial", "initial");
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

            for (int value = 0; value < 1000; value++) {
                executor.EnqueueState("property:brightness", "property", "brightness", value);
                executor.EnqueueState("property:contrast", "property", "contrast", value + 1);
                executor.EnqueueState("dimensions", "resize", value, value + 2);
            }
            unblock.TrySetResult();
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            executor.Dispose();
            await executor.Completion.WaitAsync(TimeSpan.FromSeconds(2));

            CollectionAssert.AreEquivalent(new[] {
                "initial();", "property(\"brightness\", 999);",
                "property(\"contrast\", 1000);", "resize(999, 1001);"
            }, received.ToArray());
        }
        finally { unblock.TrySetResult(); }
    }

    [TestMethod]
    public async Task Dispose_StopsIdleLoopsAndIsSafeToRepeat() {
        int dispatched = 0;
        var executor = new WebViewScriptExecutor(_ => {
            Interlocked.Increment(ref dispatched);
            return Task.CompletedTask;
        });

        executor.Dispose();
        executor.Dispose();
        executor.EnqueueRawScript("late();");
        executor.EnqueueState("position", "late", 1);
        await executor.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(0, dispatched);
        Assert.IsTrue(executor.Completion.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task Dispose_StopsBothLoopsEvenIfDispatchedCallbacksNeverFinish() {
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int dispatched = 0;
        using var executor = new WebViewScriptExecutor(_ => {
            if (Interlocked.Increment(ref dispatched) == 2) bothEntered.TrySetResult();
            return blocked.Task;
        });
        try {
            executor.EnqueueRawScript("first();");
            executor.EnqueueRawScript("mustNotRun();");
            executor.EnqueueState("position", "move", 1);
            await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

            executor.Dispose();
            await executor.Completion.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.AreEqual(2, dispatched);
            Assert.IsTrue(executor.Completion.IsCompletedSuccessfully);
        }
        finally { blocked.TrySetResult(); }
    }

    [TestMethod]
    public async Task Events_KeepTheirOrderBeforeDisposal() {
        var received = new ConcurrentQueue<string>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var executor = new WebViewScriptExecutor(script => {
            received.Enqueue(script);
            if (received.Count == 3) completed.TrySetResult();
            return Task.CompletedTask;
        });
        executor.EnqueueEvent("first", 1);
        executor.EnqueueRawScript("second();");
        executor.EnqueueEvent("third", true);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        executor.Dispose();
        await executor.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        CollectionAssert.AreEqual(new[] { "first(1);", "second();", "third(true);" }, received.ToArray());
    }
}
