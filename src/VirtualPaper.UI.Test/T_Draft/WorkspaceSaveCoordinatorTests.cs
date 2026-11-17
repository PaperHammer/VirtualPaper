using Moq;
using VirtualPaper.Common;
using VirtualPaper.EditPanel.Services;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.UI.Test.T_Draft;

[TestClass]
public class WorkspaceSaveCoordinatorTests {
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SavedProject_ClosesWithoutPromptingOrSaving(bool canCancel) {
        var runtime = new Mock<IRuntime>(MockBehavior.Strict);
        var dialogs = new Mock<IGlobalDialogService>(MockBehavior.Strict);

        Assert.IsTrue(await new WorkspaceSaveCoordinator(dialogs.Object).CanCloseAsync(runtime.Object, true, canCancel));

        runtime.VerifyNoOtherCalls();
        dialogs.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(DialogResult.None, false, false)]
    [DataRow(DialogResult.None, true, false)]
    [DataRow(DialogResult.Secondary, false, true)]
    [DataRow(DialogResult.Secondary, true, true)]
    public async Task CancelOrDiscard_DoesNotSave(DialogResult choice, bool canCancel, bool expected) {
        var runtime = new Mock<IRuntime>(MockBehavior.Strict);
        runtime.SetupGet(x => x.FileName).Returns("unsaved.vpd");
        var dialogs = Dialogs(choice, canCancel);

        Assert.AreEqual(expected, await new WorkspaceSaveCoordinator(dialogs.Object).CanCloseAsync(runtime.Object, false, canCancel));

        runtime.VerifyGet(x => x.FileName, Times.Once);
        runtime.VerifyNoOtherCalls();
        Assert.HasCount(1, dialogs.Invocations);
        StringAssert.Contains((string)dialogs.Invocations[0].Arguments[0], "\"unsaved.vpd\"");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Save_DeclinedOrThrows_BlocksClosingUntilRetrySucceeds(bool throws, bool canCancel) {
        var runtime = new Mock<IRuntime>(MockBehavior.Strict);
        runtime.SetupGet(x => x.FileName).Returns("unsaved.vpw");
        var failure = new IOException("Disk is unavailable");
        var sequence = runtime.SetupSequence(x => x.SaveAsync());
        if (throws) sequence.ThrowsAsync(failure);
        else sequence.ReturnsAsync(false);
        sequence.ReturnsAsync(true);
        var dialogs = Dialogs(DialogResult.Primary, canCancel);
        var coordinator = new WorkspaceSaveCoordinator(dialogs.Object);

        if (throws) Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<IOException>(
            () => coordinator.CanCloseAsync(runtime.Object, false, canCancel)));
        else Assert.IsFalse(await coordinator.CanCloseAsync(runtime.Object, false, canCancel));

        Assert.IsTrue(await coordinator.CanCloseAsync(runtime.Object, false, canCancel));
        runtime.Verify(x => x.SaveAsync(), Times.Exactly(2));
        Assert.HasCount(2, dialogs.Invocations);
    }

    private static Mock<IGlobalDialogService> Dialogs(DialogResult result, bool canCancel) {
        var dialogs = new Mock<IGlobalDialogService>(MockBehavior.Strict);
        if (canCancel) dialogs.Setup(x => x.ShowDialogAsync(It.IsAny<object>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true)).ReturnsAsync(result);
        else dialogs.Setup(x => x.ShowDialogAsync(It.IsAny<object>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), true)).ReturnsAsync(result);
        return dialogs;
    }
}
