using System.Collections.Concurrent;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Test.T_UIComponent;

[TestClass]
public sealed class PanelMessageCenterTests {
    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize() {
        _first = "PanelTests-A-" + Guid.NewGuid().ToString("N");
        _second = "PanelTests-B-" + Guid.NewGuid().ToString("N");
    }

    [TestCleanup]
    public void Cleanup() {
        foreach (string panel in new[] { _first, _second }) {
            PanelMessageCenter.UnregisterAllActions(panel);
            PanelMessageCenter.ClearAllSubscriptions(panel);
        }
    }

    [TestMethod]
    public async Task Actions_IsolatePanelsAndKeys_AndReplacementTakesEffect() {
        PanelMessageCenter.RegisterAction<int, string>(_first, "open", value => Task.FromResult($"A:{value}"));
        PanelMessageCenter.RegisterAction<int, string>(_second, "open", value => Task.FromResult($"B:{value}"));
        PanelMessageCenter.RegisterAction<int, string>(_first, "save", value => Task.FromResult($"saved:{value}"));
        Assert.AreEqual("A:7", await PanelMessageCenter.InvokeAsync<int, string>(_first, "open", 7));
        Assert.AreEqual("B:9", await PanelMessageCenter.InvokeAsync<int, string>(_second, "open", 9));
        PanelMessageCenter.RegisterAction<int, string>(_first, "open", value => Task.FromResult($"new:{value}"));
        Assert.AreEqual("new:11", await PanelMessageCenter.InvokeAsync<int, string>(_first, "open", 11));
        PanelMessageCenter.UnregisterAction(_first, "open");
        var missing = await PanelMessageCenter.TryInvokeAsync<int, string>(_first, "open", 0);
        Assert.IsFalse(missing.found);
        Assert.IsNull(missing.result);
        Assert.AreEqual("saved:3", await PanelMessageCenter.InvokeAsync<int, string>(_first, "save", 3));
        Assert.AreEqual("B:4", await PanelMessageCenter.InvokeAsync<int, string>(_second, "open", 4));
        PanelMessageCenter.UnregisterAllActions(_first);
        Assert.IsFalse((await PanelMessageCenter.TryInvokeAsync<int, string>(_first, "save", 0)).found);
        Assert.AreEqual("B:5", await PanelMessageCenter.InvokeAsync<int, string>(_second, "open", 5));
    }

    [TestMethod]
    public async Task MissingAction_IsDistinctFromARegisteredActionReturningDefault() {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PanelMessageCenter.InvokeAsync<int, string>(_first, "missing", 0));
        StringAssert.Contains(error.Message, _first);
        StringAssert.Contains(error.Message, "missing");
        PanelMessageCenter.RegisterAction<int, string?>(_first, "empty", _ => Task.FromResult<string?>(null));
        var empty = await PanelMessageCenter.TryInvokeAsync<int, string?>(_first, "empty", 0);
        Assert.IsTrue(empty.found);
        Assert.IsNull(empty.result);
        PanelMessageCenter.RegisterAction<int, string>(_first, "typed", _ => Task.FromResult("text"));
        var wrongResultType = await PanelMessageCenter.TryInvokeAsync<int, int>(_first, "typed", 0);
        Assert.IsTrue(wrongResultType.found);
        Assert.AreEqual(0, wrongResultType.result);
        Assert.AreEqual(0, await PanelMessageCenter.InvokeAsync<int, int>(_first, "typed", 0));
    }

    [TestMethod]
    public async Task HandlerFailures_PropagateFromBothInvocationApis_AndAllowRetry() {
        var error = new IOException("Expected save failure");
        PanelMessageCenter.RegisterAction<int, string>(_first, "save", _ => Task.FromException<string>(error));
        Assert.AreSame(error, await Assert.ThrowsAsync<IOException>(() => PanelMessageCenter.InvokeAsync<int, string>(_first, "save", 1)));
        Assert.AreSame(error, await Assert.ThrowsAsync<IOException>(() => PanelMessageCenter.TryInvokeAsync<int, string>(_first, "save", 1)));
        PanelMessageCenter.RegisterAction<int, string>(_first, "save", _ => Task.FromResult("retry succeeded"));
        Assert.AreEqual("retry succeeded", await PanelMessageCenter.InvokeAsync<int, string>(_first, "save", 1));
    }

    [TestMethod]
    public async Task UnregisteringAnInFlightAction_DoesNotLoseItsResponseOrKeepItRegistered() {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        PanelMessageCenter.RegisterAction<int, int>(_first, "load", async value => {
            entered.TrySetResult();
            return value + await release.Task.WaitAsync(TestContext.CancellationToken);
        });
        Task<int> pending = PanelMessageCenter.InvokeAsync<int, int>(_first, "load", 10);
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
            Assert.IsFalse(pending.IsCompleted);
            PanelMessageCenter.UnregisterAllActions(_first);
            Assert.IsFalse((await PanelMessageCenter.TryInvokeAsync<int, int>(_first, "load", 5)).found);
            release.TrySetResult(20);
            Assert.AreEqual(30, await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        }
        finally { release.TrySetResult(20); }
    }

    [TestMethod]
    public void Events_IsolatePanelEventAndPayloadType_AndContinueAfterSubscriberFailure() {
        var received = new List<string>();
        using var first = PanelMessageCenter.Subscribe<int>(_first, "changed", value => received.Add($"first:{value}"));
        using var throwing = PanelMessageCenter.Subscribe<int>(_first, "changed", _ => throw new IOException("Expected subscriber failure"));
        using var last = PanelMessageCenter.Subscribe<int>(_first, "changed", value => received.Add($"last:{value}"));
        using var wrongType = PanelMessageCenter.Subscribe<string>(_first, "changed", value => received.Add(value));
        using var otherEvent = PanelMessageCenter.Subscribe<int>(_first, "other", _ => received.Add("wrong event"));
        using var otherPanel = PanelMessageCenter.Subscribe<int>(_second, "changed", _ => received.Add("wrong panel"));
        PanelMessageCenter.Publish(_first, "changed", 7);
        CollectionAssert.AreEqual(new[] { "first:7", "last:7" }, received);
        first.Dispose(); first.Dispose();
        PanelMessageCenter.Publish(_first, "changed", 8);
        CollectionAssert.AreEqual(new[] { "first:7", "last:7", "last:8" }, received);
    }

    [TestMethod]
    public void SubscriptionChangesDuringPublish_ApplyToTheNextSnapshot() {
        var received = new List<string>();
        IDisposable? removed = null, added = null;
        using var first = PanelMessageCenter.Subscribe<int>(_first, "changed", _ => {
            received.Add("first");
            removed?.Dispose();
            added ??= PanelMessageCenter.Subscribe<int>(_first, "changed", _ => received.Add("added"));
        });
        removed = PanelMessageCenter.Subscribe<int>(_first, "changed", _ => received.Add("removed"));
        try {
            PanelMessageCenter.Publish(_first, "changed", 1);
            CollectionAssert.AreEqual(new[] { "first", "removed" }, received);
            received.Clear();
            PanelMessageCenter.Publish(_first, "changed", 2);
            CollectionAssert.AreEqual(new[] { "first", "added" }, received);
        }
        finally { removed.Dispose(); added?.Dispose(); }
    }

    [TestMethod]
    public void ClearingSubscriptions_IsPanelScoped_AndOldTokensCannotRemoveNewSubscribers() {
        var received = new List<string>();
        using var old = PanelMessageCenter.Subscribe<int>(_first, "changed", _ => received.Add("old"));
        using var other = PanelMessageCenter.Subscribe<int>(_second, "changed", _ => received.Add("other"));
        PanelMessageCenter.ClearAllSubscriptions(_first);
        using var current = PanelMessageCenter.Subscribe<int>(_first, "changed", _ => received.Add("new"));
        old.Dispose(); old.Dispose();
        PanelMessageCenter.Publish(_first, "changed", 1);
        PanelMessageCenter.Publish(_second, "changed", 1);
        PanelMessageCenter.Publish(_first, "not registered", 1);
        CollectionAssert.AreEqual(new[] { "new", "other" }, received);
    }

    [TestMethod]
    public async Task ConcurrentPublish_DeliversEveryPayloadExactlyOnce() {
        var received = new ConcurrentBag<int>();
        using var subscription = PanelMessageCenter.Subscribe<int>(_first, "changed", received.Add);
        await Task.WhenAll(Enumerable.Range(0, 100).Select(value => Task.Run(() =>
            PanelMessageCenter.Publish(_first, "changed", value), TestContext.CancellationToken)));
        CollectionAssert.AreEqual(Enumerable.Range(0, 100).ToArray(), received.Order().ToArray());
        subscription.Dispose();
        PanelMessageCenter.Publish(_first, "changed", -1);
        Assert.HasCount(100, received);
    }

    [TestMethod]
    public async Task NullHandlers_AreRejectedWithoutRegisteringPartialEntries() {
        Assert.Throws<ArgumentNullException>(() => PanelMessageCenter.RegisterAction<int, int>(_first, "invalid", null!));
        Assert.Throws<ArgumentNullException>(() => PanelMessageCenter.Subscribe<int>(_first, "invalid", null!));
        Assert.IsFalse((await PanelMessageCenter.TryInvokeAsync<int, int>(_first, "invalid", 0)).found);
        PanelMessageCenter.Publish(_first, "invalid", 1);
    }

    private string _first = null!, _second = null!;
}
