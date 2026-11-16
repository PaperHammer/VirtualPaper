using Moq;
using VirtualPaper.AppSettingsPanel.Utils;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.UI.Test.T_AppSettings;

[TestClass]
public class AiUseNoticeServiceTests {
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FirstUse_OnlyAcknowledgementPersistsTheNoticeVersion(bool accept) {
        var settings = new Settings();
        var client = CreateClient(settings);
        var service = new AiUseNoticeService(client.Object, () => Task.FromResult(accept));

        Assert.AreEqual(accept, await service.EnsureAcknowledgedAsync());
        Assert.AreEqual(accept ? 1 : 0, settings.AiUseNoticeVersion);
        Assert.AreEqual(accept, service.IsAcknowledged);
        client.Verify(value => value.SaveAsync<ISettings>(), accept ? Times.Once() : Times.Never());
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task AcknowledgedVersion_LanguageChangeAndNewServiceDoNotPromptAgain(int version) {
        var settings = new Settings { AiUseNoticeVersion = version, Language = "zh-CN" };
        var client = CreateClient(settings);
        foreach (string language in new[] { "en-US", "ru-RU" }) {
            settings.Language = language;
            var service = new AiUseNoticeService(client.Object, () => throw new AssertFailedException("Notice was already acknowledged."));
            Assert.IsTrue(await service.EnsureAcknowledgedAsync());
        }
        Assert.AreEqual(version, settings.AiUseNoticeVersion);
        client.Verify(value => value.SaveAsync<ISettings>(), Times.Never);
    }

    [TestMethod]
    public async Task ConcurrentEntries_ShareTheDecision_AndCancellationAllowsRetry() {
        var client = CreateClient(new Settings());
        var choice = new TaskCompletionSource<bool>();
        int prompts = 0;
        var service = new AiUseNoticeService(client.Object, () => { prompts++; return choice.Task; });
        Task<bool> first = service.EnsureAcknowledgedAsync(), second = service.EnsureAcknowledgedAsync();
        Assert.AreSame(first, second);
        choice.SetResult(false);
        Assert.IsFalse(await first);
        Assert.IsFalse(await second);
        Assert.AreEqual(1, prompts);
        client.Verify(value => value.SaveAsync<ISettings>(), Times.Never);

        choice = new();
        Task<bool> retry = service.EnsureAcknowledgedAsync();
        choice.SetResult(true);
        Assert.IsTrue(await retry);
        Assert.AreEqual(2, prompts);
        client.Verify(value => value.SaveAsync<ISettings>(), Times.Once);
    }

    [TestMethod]
    public async Task PendingPersistence_DoesNotAllowAnotherEntryToStartEarly() {
        var client = CreateClient(new Settings());
        var saved = new TaskCompletionSource();
        client.Setup(value => value.SaveAsync<ISettings>()).Returns(saved.Task);
        var service = new AiUseNoticeService(client.Object, () => Task.FromResult(true));
        Task<bool> first = service.EnsureAcknowledgedAsync(), second = service.EnsureAcknowledgedAsync();
        Assert.IsFalse(service.IsAcknowledged);
        Assert.AreSame(first, second);
        Assert.IsFalse(second.IsCompleted);
        saved.SetResult();
        Assert.IsTrue(await first);
        Assert.IsTrue(service.IsAcknowledged);
    }

    [TestMethod]
    public async Task FailedPersistence_RestoresTheVersion_AndPromptsOnRetry() {
        var settings = new Settings();
        var client = CreateClient(settings);
        client.SetupSequence(value => value.SaveAsync<ISettings>())
            .ThrowsAsync(new IOException("Unavailable"))
            .Returns(Task.CompletedTask);
        int prompts = 0;
        var service = new AiUseNoticeService(client.Object, () => { prompts++; return Task.FromResult(true); });
        await Assert.ThrowsExactlyAsync<IOException>(() => service.EnsureAcknowledgedAsync());
        Assert.AreEqual(0, settings.AiUseNoticeVersion);
        Assert.IsFalse(service.IsAcknowledged);
        Assert.IsTrue(await service.EnsureAcknowledgedAsync());
        Assert.AreEqual(2, prompts);
        Assert.AreEqual(1, settings.AiUseNoticeVersion);
    }

    [TestMethod]
    public async Task UnavailableNotice_DoesNotAcknowledgeOrSave() {
        var settings = new Settings();
        var client = CreateClient(settings);
        var service = new AiUseNoticeService(client.Object, () => throw new FileNotFoundException("Notice missing"));
        await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => service.EnsureAcknowledgedAsync());
        Assert.AreEqual(0, settings.AiUseNoticeVersion);
        client.Verify(value => value.SaveAsync<ISettings>(), Times.Never);
    }

    private static Mock<IUserSettingsClient> CreateClient(Settings settings) {
        var client = new Mock<IUserSettingsClient>();
        client.SetupGet(value => value.Settings).Returns(settings);
        client.Setup(value => value.SaveAsync<ISettings>()).Returns(Task.CompletedTask);
        return client;
    }
}
