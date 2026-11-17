using VirtualPaper.Common.Utils.UndoRedo;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class UndoRedoFailureTests {
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task FailureOrCancellation_KeepsHistoryAndSavedMarker_AndAllowsRetry(bool redo, bool cancel) {
        using var history = new UndoRedoUtil<IUndoableCommand>(isSaved: true);
        var command = new DeferredCommand();
        history.Record(command);
        if (redo) Assert.IsTrue(await history.UndoAsync());
        history.MarkAsSaved();
        int undoCount = history.UndoStackSize, redoCount = history.RedoStackSize;
        long undoMemory = history.UndoMemoryBytes, redoMemory = history.RedoMemoryBytes;
        int savedEvents = 0, completedEvents = 0;
        history.IsSavedChanged += (_, _) => savedEvents++;
        history.AfterUndo += (_, _) => completedEvents++;
        history.AfterRedo += (_, _) => completedEvents++;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        command.Pending = completion.Task;
        Task<bool> operation = redo ? history.RedoAsync() : history.UndoAsync();
        try {
            Assert.IsFalse(operation.IsCompleted);
            Assert.IsTrue(history.IsUndoingOrRedoing);
            if (cancel) {
                completion.SetCanceled(TestContext.CancellationToken);
                await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
            }
            else {
                var failure = new IOException("Snapshot cannot be read");
                completion.SetException(failure);
                Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<IOException>(() => operation));
            }

            Assert.AreEqual(undoCount, history.UndoStackSize);
            Assert.AreEqual(redoCount, history.RedoStackSize);
            Assert.AreEqual(undoMemory, history.UndoMemoryBytes);
            Assert.AreEqual(redoMemory, history.RedoMemoryBytes);
            Assert.IsTrue(history.IsSaved);
            Assert.IsFalse(history.IsUndoingOrRedoing);
            Assert.AreEqual(0, completedEvents);
            Assert.AreEqual(0, savedEvents);

            command.Pending = Task.CompletedTask;
            Assert.IsTrue(await (redo ? history.RedoAsync() : history.UndoAsync())
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken), "A failed operation must release the execution gate.");
            Assert.AreEqual(redo ? undoCount + 1 : undoCount - 1, history.UndoStackSize);
            Assert.AreEqual(redo ? redoCount - 1 : redoCount + 1, history.RedoStackSize);
            Assert.IsFalse(history.IsSaved);
            Assert.AreEqual(1, completedEvents);
            Assert.AreEqual(1, savedEvents);
        }
        finally {
            completion.TrySetResult();
            try { await operation; }
            catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
        }
    }

    private sealed class DeferredCommand : IUndoableCommand, IMemoryAwareUndoableCommand {
        public string Description => "Deferred snapshot";
        public long EstimatedMemoryBytes => 32;
        public Task Pending { get; set; } = Task.CompletedTask;
        public Task ExecuteAsync() => Pending;
        public Task UndoAsync() => Pending;
    }
}
