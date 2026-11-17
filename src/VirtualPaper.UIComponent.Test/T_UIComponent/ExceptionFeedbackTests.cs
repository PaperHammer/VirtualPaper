using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Test.T_UIComponent;

[TestClass]
public class ExceptionFeedbackTests {
    [TestMethod]
    public void UnknownFailure_ShowsFriendlyTextAndRetainsTechnicalDetails() {
        var exception = new InvalidOperationException("Status(StatusCode=Internal, Detail=technical-payload)");
        var entries = Capture(() => GlobalMessageUtil.ShowException(exception));

        var message = entries.Single().Message;
        Assert.AreEqual(LanguageUtil.GetI18n("UserError_Unexpected"), message.Message);
        Assert.AreEqual(InfoBarSeverity.Error, message.Severity);
        Assert.AreEqual(exception.ToString(), message.TechnicalDetails);
        Assert.IsNotNull(message.ActionCommand);
        Assert.AreEqual(LanguageUtil.GetI18n("Text_Details"), message.ActionText);
    }

    [TestMethod]
    public void FileFailure_PreservesCallerContextAndMessageIdentity() {
        var exception = new FileNotFoundException("raw framework text", "missing.png");
        var entries = Capture(() => GlobalMessageUtil.ShowException(exception, key: "ImportFailed", extraMsg: "\nmissing.png"));

        var message = entries.Single().Message;
        Assert.AreEqual("ImportFailed", message.Key);
        Assert.AreEqual(LanguageUtil.GetI18n("UserError_NotFound") + "\nmissing.png", message.Message);
        Assert.AreEqual(exception.ToString(), message.TechnicalDetails);
    }

    [TestMethod]
    public void WrappedCancellation_IsInformationalAndDoesNotOfferErrorDetails() {
        var entries = Capture(() => GlobalMessageUtil.ShowException(new AggregateException(new OperationCanceledException("task"))));

        var message = entries.Single().Message;
        Assert.AreEqual(InfoBarSeverity.Informational, message.Severity);
        Assert.AreEqual(LanguageUtil.GetI18n("UserError_Canceled"), message.Message);
        Assert.IsNull(message.ActionCommand);
    }

    [TestMethod]
    public void TimeoutDuringCancellation_IsStillReportedAsAFailure() {
        var entries = Capture(() => GlobalMessageUtil.ShowException(new TaskCanceledException("deadline", new TimeoutException())));

        var message = entries.Single().Message;
        Assert.AreEqual(InfoBarSeverity.Error, message.Severity);
        Assert.AreEqual(LanguageUtil.GetI18n("UserError_Timeout"), message.Message);
    }

    [TestMethod]
    public void DifferentFailures_WithSameFriendlyText_RetainBothTechnicalDetailsInBatch() {
        var first = new InvalidOperationException("First technical failure");
        var second = new InvalidOperationException("Second technical failure");
        var entries = Capture(() => {
            GlobalMessageUtil.ShowException(first);
            GlobalMessageUtil.ShowException(first);
            GlobalMessageUtil.ShowException(second);
        });

        var messages = GlobalMessageBatch.Group(entries).Single().Messages;
        Assert.HasCount(2, messages);
        Assert.AreEqual(messages[0].Message, messages[1].Message);
        CollectionAssert.AreEqual(new[] { first.ToString(), second.ToString() }, messages.Select(message => message.TechnicalDetails).ToArray());
    }

    [TestMethod]
    public void ExplicitResourceMessage_RemainsLocalized() {
        var entries = Capture(() => GlobalMessageUtil.ShowException(new Exception("ScreenFit_ReadFailed"), isNeedLocalizer: true));
        Assert.AreEqual(LanguageUtil.GetI18n("ScreenFit_ReadFailed"), entries.Single().Message.Message);
    }

    [TestMethod]
    public void PersistedFailureNotice_KeepsTechnicalPayloadOutOfTheSummary() {
        const string details = "HTTP status 500: internal update payload";
        var entries = Capture(() => GlobalMessageUtil.ShowErrorWithDetails("AppUpdater_UpdateFailedMessage", details,
            key: "UpdateFailedNotice", isNeedLocalizer: true));

        var message = entries.Single().Message;
        Assert.AreEqual(LanguageUtil.GetI18n("AppUpdater_UpdateFailedMessage"), message.Message);
        Assert.AreEqual("UpdateFailedNotice", message.Key);
        Assert.AreEqual(details, message.TechnicalDetails);
        Assert.IsNotNull(message.ActionCommand);
    }

    private static List<GlobalMessageBatch.Entry> Capture(Action action) {
        var entries = new List<GlobalMessageBatch.Entry>();
        using (GlobalMessageBatch.Begin(messages => entries.AddRange(messages))) action();
        return entries;
    }
}
