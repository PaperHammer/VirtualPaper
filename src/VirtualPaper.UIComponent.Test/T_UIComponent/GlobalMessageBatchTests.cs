using System.Collections.Concurrent;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Test.T_UIComponent;

[TestClass]
public class GlobalMessageBatchTests {
    [TestMethod]
    public void NestedOperations_GroupSameSeverity_AndDeduplicateIdenticalMessages() {
        var published = new List<GlobalMessageBatch.Entry>();
        using (GlobalMessageBatch.Begin(entries => published.AddRange(entries))) {
            using (GlobalMessageUtil.BeginBatch()) {
                GlobalMessageUtil.ShowError("First file failed");
                GlobalMessageUtil.ShowError("First file failed");
                GlobalMessageUtil.ShowError("Second file failed");
                GlobalMessageUtil.ShowInfo("Some files are in use");
            }
            Assert.IsEmpty(published, "Nested calls must wait for the outer operation to finish.");
        }

        var groups = GlobalMessageBatch.Group(published).ToArray();
        Assert.HasCount(2, groups);
        CollectionAssert.AreEqual(new[] { "First file failed", "Second file failed" },
            groups.Single(group => group.Messages[0].Severity == InfoBarSeverity.Error).Messages.Select(message => message.Message).ToArray());
        Assert.AreEqual("Some files are in use", groups.Single(group => group.Messages[0].Severity == InfoBarSeverity.Informational).Messages[0].Message);
    }

    [TestMethod]
    public async Task ParallelChildWork_IsCollectedWithoutLosingMessages() {
        var published = new List<GlobalMessageBatch.Entry>();
        using (GlobalMessageBatch.Begin(entries => published.AddRange(entries))) {
            await Task.WhenAll(Enumerable.Range(0, 100).Select(index => Task.Run(() => GlobalMessageUtil.ShowError($"File {index} failed"))));
            Assert.IsEmpty(published);
        }

        var groups = GlobalMessageBatch.Group(published).ToArray();
        Assert.HasCount(1, groups);
        Assert.HasCount(100, groups[0].Messages);
    }

    [TestMethod]
    public async Task IndependentOperations_DoNotMergeTheirMessages() {
        var published = new ConcurrentDictionary<string, string[]>();
        async Task RunAsync(string operation) {
            using var batch = GlobalMessageBatch.Begin(entries => published[operation] = entries.Select(entry => entry.Message.Message).ToArray());
            GlobalMessageUtil.ShowError(operation + " first");
            await Task.Yield();
            GlobalMessageUtil.ShowError(operation + " second");
        }
        await Task.WhenAll(RunAsync("Import"), RunAsync("Save"));

        CollectionAssert.AreEqual(new[] { "Import first", "Import second" }, published["Import"]);
        CollectionAssert.AreEqual(new[] { "Save first", "Save second" }, published["Save"]);
    }

    [TestMethod]
    public async Task CompletedScope_FlushesOnce_AndDoesNotSwallowLateMessages() {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int publications = 0;
        var batch = GlobalMessageBatch.Begin(entries => { Assert.HasCount(1, entries); publications++; });
        Task<bool> late;
        try {
            GlobalMessageUtil.ShowError("Initial failure");
            late = Task.Run(async () => {
                await gate.Task;
                return GlobalMessageBatch.TryAdd(new(null, new GlobalMsgInfo(null, false, "Late failure", null, InfoBarSeverity.Error)));
            });
        }
        finally { batch.Dispose(); }
        batch.Dispose();
        gate.SetResult();

        Assert.IsFalse(await late.WaitAsync(TimeSpan.FromSeconds(5)), "A notification after completion must use normal delivery.");
        Assert.AreEqual(1, publications);
        Assert.IsFalse(GlobalMessageBatch.TryAdd(new(null, new GlobalMsgInfo(null, false, "Next operation", null, InfoBarSeverity.Error))));
    }

    [TestMethod]
    public void WithdrawnStatus_IsNotPublishedAfterTheOperationCompletes() {
        var published = new List<GlobalMessageBatch.Entry>();
        using (GlobalMessageBatch.Begin(entries => published.AddRange(entries))) {
            GlobalMessageUtil.ShowWarning("Layer is locked", key: "LayerLocked");
            using (GlobalMessageUtil.BeginBatch()) {
                GlobalMessageUtil.ShowWarning("Layer is locked", key: "LayerLocked");
                GlobalMessageBatch.Remove(null, "LayerLocked");
                GlobalMessageUtil.ShowError("File failed");
            }
        }

        Assert.HasCount(1, published);
        Assert.AreEqual("File failed", published[0].Message.Message);
    }

    [TestMethod]
    public void SingleMessage_PreservesItsInstanceKeyAndSeverity() {
        var message = new GlobalMsgInfo("OperationFailure", false, "Specific failure", null, InfoBarSeverity.Error);

        var group = GlobalMessageBatch.Group([new(null, message)]).Single();

        Assert.HasCount(1, group.Messages);
        Assert.AreSame(message, group.Messages[0]);
        Assert.AreEqual("OperationFailure", group.Messages[0].Key);
        Assert.AreEqual(InfoBarSeverity.Error, group.Messages[0].Severity);
    }

    [TestMethod]
    public async Task LateChildNotification_AfterNestedScopeCompletion_JoinsTheActiveOuterOperation() {
        var published = new List<GlobalMessageBatch.Entry>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (GlobalMessageBatch.Begin(entries => published.AddRange(entries))) {
            Task child;
            using (GlobalMessageUtil.BeginBatch()) {
                GlobalMessageUtil.ShowError("Initial failure");
                child = Task.Run(async () => { await gate.Task; GlobalMessageUtil.ShowError("Late child failure"); });
            }
            gate.SetResult();
            await child.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsEmpty(published);
        }

        CollectionAssert.AreEqual(new[] { "Initial failure", "Late child failure" }, published.Select(entry => entry.Message.Message).ToArray());
    }

    [TestMethod]
    public void FailedOperation_FlushesAndRestoresThePreviousScope() {
        var published = new List<string>();
        Assert.ThrowsExactly<IOException>(() => {
            using var batch = GlobalMessageBatch.Begin(entries => published.AddRange(entries.Select(entry => entry.Message.Message)));
            GlobalMessageUtil.ShowError("Failed operation");
            throw new IOException("Controlled failure");
        });
        using (GlobalMessageBatch.Begin(entries => published.AddRange(entries.Select(entry => entry.Message.Message)))) {
            GlobalMessageUtil.ShowError("New operation");
        }

        CollectionAssert.AreEqual(new[] { "Failed operation", "New operation" }, published);
    }
}
