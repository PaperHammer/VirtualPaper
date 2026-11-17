using VirtualPaper.UIOperationTest.Infrastructure;

namespace VirtualPaper.UI.Test.T_Infrastructure;

[TestClass]
public class UiOperationWaitTests {
    [TestMethod]
    public void ReadyObservationReturnsWithoutRepeatingIt() {
        int reads = 0;
        var expected = new object();
        var result = UiOperationWait.Until(() => { reads++; return expected; }, value => value != null, "ready");
        Assert.AreSame(expected, result);
        Assert.AreEqual(1, reads);
    }

    [TestMethod]
    public void ReplacedProviderAndTemporarilyMissingControlAreRetriedUntilReady() {
        int reads = 0;
        var expected = new object();
        var result = UiOperationWait.Until(() => ++reads switch {
            1 => throw new IOException("Provider replaced"),
            2 => null,
            _ => expected
        }, value => value != null, "opening picker", ex => ex is IOException);
        Assert.AreSame(expected, result);
        Assert.AreEqual(3, reads);
    }

    [TestMethod]
    public void MissingObservationDoesNotCountAsCompletedAction() {
        Assert.ThrowsExactly<TimeoutException>(() => UiOperationWait.Until<object?>(() => null,
            value => value != null, "picker ready", timeout: TimeSpan.Zero));
        Assert.ThrowsExactly<TimeoutException>(() => UiOperationWait.Until(() => false,
            closed => closed, "picker closed", timeout: TimeSpan.Zero));
    }

    [TestMethod]
    public void UnavailableProviderTimesOutWithItsOriginalError() {
        var expected = new IOException("Provider disconnected");
        var error = Assert.ThrowsExactly<TimeoutException>(() => UiOperationWait.Until<object>(() => throw expected,
            _ => true, "filename", ex => ex is IOException, TimeSpan.Zero));
        Assert.AreSame(expected, error.InnerException);
        StringAssert.Contains(error.Message, "filename");
    }

    [TestMethod]
    public void UnexpectedErrorIsNeverRetriedOrReclassified() {
        int reads = 0;
        var expected = new InvalidOperationException("Wrong owner");
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => UiOperationWait.Until<object>(() => {
            reads++;
            throw expected;
        }, _ => true, "picker", ex => ex is IOException));
        Assert.AreSame(expected, error);
        Assert.AreEqual(1, reads);
    }
}
