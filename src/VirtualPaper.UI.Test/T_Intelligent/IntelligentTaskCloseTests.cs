using Moq;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.Services;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.UI.Test.T_Intelligent;

[TestClass]
public class IntelligentTaskCloseTests {
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Preservation_RequiresSuccess_AndTracksExportAndImportIndependently(bool style) {
        var item = Item(style);
        Assert.IsTrue(item.IsTemporaryResult);
        await item.PreserveAsync(() => Task.FromResult(false), import: false);
        await item.PreserveAsync(() => Task.FromResult(false), import: true);
        Assert.IsTrue(item.IsTemporaryResult, "Cancelled/failed operations must not mark a result as saved");
        await Assert.ThrowsAsync<IOException>(() => item.PreserveAsync(() => Task.FromException<bool>(new IOException()), import: true));
        Assert.IsFalse(item.IsPreserving);
        Assert.IsTrue(item.IsTemporaryResult);

        await item.PreserveAsync(() => Task.FromResult(true), import: false);
        Assert.IsTrue(item.IsExported);
        Assert.IsFalse(item.IsImported);
        Assert.IsFalse(item.IsTemporaryResult);
        await item.PreserveAsync(() => Task.FromResult(true), import: true);
        Assert.IsTrue(item.IsImported);
        await item.PreserveAsync(() => Task.FromResult(false), import: true);
        Assert.IsTrue(item.IsImported, "A later cancelled operation must not erase a previous successful save");
    }

    [TestMethod]
    public async Task Preservation_DoesNotRunTwoOperationsAtOnce() {
        var item = Item();
        var completion = new TaskCompletionSource<bool>();
        var first = item.PreserveAsync(() => completion.Task, import: false);
        int calls = 0;
        var second = item.PreserveAsync(() => { calls++; return Task.FromResult(true); }, import: true);
        Assert.IsTrue(item.IsPreserving);
        completion.SetResult(true);
        await Task.WhenAll(first, second);
        Assert.AreEqual(0, calls);
        Assert.IsTrue(item.IsExported);
        Assert.IsFalse(item.IsImported);
    }

    [TestMethod]
    public async Task Close_AggregatesBothModules_AndDoesNotCancelBeforeCommit() {
        using var fixture = new Fixture();
        var style = (StyleTransferTaskItem)Item(style: true);
        style.Status = TaskStatus.WaitingToRun;
        var result = (SuperResolutionTaskItem)Item();
        fixture.Style.Tasks.Add(style);
        fixture.SuperResolution.Tasks.Add(result);
        fixture.Dialogs.Setup(d => d.ConfirmCloseAsync(1, 1, true)).ReturnsAsync(new TaskCloseDecision(true, true));

        Assert.IsTrue(await fixture.Coordinator.CanCloseAsync());
        Assert.IsFalse(style.Cts.IsCancellationRequested);
        Assert.IsTrue(fixture.Settings.RemindUnpreservedIntelligentResults);
        fixture.Client.Verify(c => c.SaveAsync<ISettings>(), Times.Never);

        await fixture.Coordinator.CommitCloseAsync();
        Assert.IsTrue(style.Cts.IsCancellationRequested);
        Assert.IsFalse(fixture.Settings.RemindUnpreservedIntelligentResults);
        fixture.Client.Verify(c => c.SaveAsync<ISettings>(), Times.Once);
        Assert.IsTrue(fixture.Style.IsClosing);
        Assert.IsTrue(fixture.SuperResolution.IsClosing);
    }

    [TestMethod]
    public async Task Close_CancelledConfirmation_KeepsPreferenceAndTasks_AndPromptsAgain() {
        using var fixture = new Fixture();
        var item = (SuperResolutionTaskItem)Item();
        fixture.SuperResolution.Tasks.Add(item);
        fixture.Dialogs.Setup(d => d.ConfirmCloseAsync(0, 1, true)).ReturnsAsync(new TaskCloseDecision(false, true));
        Assert.IsFalse(await fixture.Coordinator.CanCloseAsync());
        Assert.IsFalse(await fixture.Coordinator.CanCloseAsync());
        Assert.IsFalse(item.Cts.IsCancellationRequested);
        Assert.IsTrue(fixture.Settings.RemindUnpreservedIntelligentResults);
        fixture.Dialogs.Verify(d => d.ConfirmCloseAsync(0, 1, true), Times.Exactly(2));
        fixture.Client.Verify(c => c.SaveAsync<ISettings>(), Times.Never);
    }

    [TestMethod]
    public async Task Close_SuppressionNeverSkipsActiveTasksOrSavesInProgress() {
        using var fixture = new Fixture();
        fixture.Settings.RemindUnpreservedIntelligentResults = false;
        var item = (SuperResolutionTaskItem)Item();
        fixture.SuperResolution.Tasks.Add(item);
        Assert.IsTrue(await fixture.Coordinator.CanCloseAsync());
        fixture.Dialogs.VerifyNoOtherCalls();

        item.Status = TaskStatus.Running;
        fixture.Dialogs.Setup(d => d.ConfirmCloseAsync(1, 0, false)).ReturnsAsync(new TaskCloseDecision(false, false));
        Assert.IsFalse(await fixture.Coordinator.CanCloseAsync());
        item.Status = TaskStatus.RanToCompletion;
        var completion = new TaskCompletionSource<bool>();
        var saving = item.PreserveAsync(() => completion.Task, import: true);
        fixture.Dialogs.Setup(d => d.ConfirmCloseAsync(1, 0, false)).ReturnsAsync(new TaskCloseDecision(false, false));
        Assert.IsFalse(await fixture.Coordinator.CanCloseAsync());
        completion.SetResult(true);
        await saving;
        Assert.IsTrue(await fixture.Coordinator.CanCloseAsync());
    }

    [TestMethod]
    public async Task Close_PreservedAndFailedTasks_DoNotPrompt() {
        using var fixture = new Fixture();
        var exported = (StyleTransferTaskItem)Item(style: true);
        await exported.PreserveAsync(() => Task.FromResult(true), import: false);
        var imported = (SuperResolutionTaskItem)Item();
        await imported.PreserveAsync(() => Task.FromResult(true), import: true);
        var failed = (SuperResolutionTaskItem)Item();
        failed.Status = TaskStatus.Faulted;
        fixture.Style.Tasks.Add(exported);
        fixture.SuperResolution.Tasks.Add(imported);
        fixture.SuperResolution.Tasks.Add(failed);
        Assert.IsTrue(await fixture.Coordinator.CanCloseAsync());
        fixture.Dialogs.VerifyNoOtherCalls();
    }

    private static IntelligentTaskItem Item(bool style = false) => style
        ? new StyleTransferTaskItem(new StyleTransferData("source.png", "1 KB", ".png", 64, 64, "style.png", "Style", "1 KB", ".png")) { Status = TaskStatus.RanToCompletion }
        : new SuperResolutionTaskItem(new SuperResolutionData("source.png", "1 KB", ".png", 64, 64, EnhanceMode.SuperResolution, 2, 128, 128)) { Status = TaskStatus.RanToCompletion };

    private sealed class Fixture : IDisposable {
        public StyleTranferViewModel Style { get; } = new();
        public SuperResolutionViewModel SuperResolution { get; } = new();
        public Settings Settings { get; } = new();
        public Mock<IUserSettingsClient> Client { get; } = new();
        public Mock<IIntelligentTaskDialogs> Dialogs { get; } = new(MockBehavior.Strict);
        public IntelligentTaskCloseCoordinator Coordinator { get; }
        public Fixture() {
            Client.SetupGet(c => c.Settings).Returns(Settings);
            Coordinator = new(Style, SuperResolution, Client.Object, Dialogs.Object);
        }
        public void Dispose() { Style.Dispose(); SuperResolution.Dispose(); }
    }
}
