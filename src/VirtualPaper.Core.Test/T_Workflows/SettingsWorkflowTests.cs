using System.Text.Json.Nodes;
using Google.Protobuf.WellKnownTypes;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.Grpc.Client;
using VirtualPaper.Grpc.Service.UserSettings;
using VirtualPaper.Models;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Core.Test.T_Workflows;

[TestClass]
[DoNotParallelize]
[TestCategory("Workflow")]
public class SettingsWorkflowTests {
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(30000, CooperativeCancellation = true)]
    public async Task UiHardwareAcceleration_LegacyProfileAndClient_PreserveSavedPreferenceAcrossRestart() {
        using var environment = new WorkflowEnvironment();
        var token = TestContext.CancellationTokenSource.Token;
        token.ThrowIfCancellationRequested();
        File.WriteAllText(Constants.CommonPaths.UserSettingsPath, "{\"WallpaperWaitTime\":73}");
        environment.StartSettingsServer();
        using (var client = new UserSettingsClient(environment.CreateChannel())) {
            Assert.IsTrue(client.Settings.UiHardwareAccelerationEnabled);
            client.Settings.UiHardwareAccelerationEnabled = false;
            await client.SaveAsync<ISettings>().WaitAsync(token);
            Assert.IsFalse(JsonNode.Parse(File.ReadAllText(Constants.CommonPaths.UserSettingsPath))!["UiHardwareAccelerationEnabled"]!.GetValue<bool>());

            var legacyClient = new Grpc_UserSettingsService.Grpc_UserSettingsServiceClient(environment.CreateChannel());
            var request = await legacyClient.GetSettingsAsync(new Empty(), cancellationToken: token);
            request.ClearUiHardwareAccelerationEnabled();
            request.WallpaperWaitTime = 74;
            await legacyClient.SetSettingsAsync(request, cancellationToken: token);
            await client.LoadAsync<ISettings>().WaitAsync(token);
            Assert.IsFalse(client.Settings.UiHardwareAccelerationEnabled);
        }

        environment.StartSettingsServer();
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        Assert.IsFalse(restarted.Settings.UiHardwareAccelerationEnabled);
        Assert.AreEqual(74, restarted.Settings.WallpaperWaitTime);
        restarted.Settings.UiHardwareAccelerationEnabled = true;
        await restarted.SaveAsync<ISettings>().WaitAsync(token);
        await restarted.LoadAsync<ISettings>().WaitAsync(token);
        Assert.IsTrue(restarted.Settings.UiHardwareAccelerationEnabled);
        Assert.IsTrue(JsonNode.Parse(File.ReadAllText(Constants.CommonPaths.UserSettingsPath))!["UiHardwareAccelerationEnabled"]!.GetValue<bool>());
        environment.UiRunner.Verify(runner => runner.RestartUIAsync(), Times.Never);
    }

    [TestMethod]
    public async Task BuiltInLibrary_LegacyProfileGetsSavedDefault_AndExplicitPathSurvivesRestartAndLegacyClient() {
        using var environment = new WorkflowEnvironment();
        var token = TestContext.CancellationTokenSource.Token;
        token.ThrowIfCancellationRequested();
        string userDirectory = Path.Combine(environment.Root, "moved library", "wallpapers");
        var legacy = new JsonObject { ["WallpaperDir"] = userDirectory };
        File.WriteAllText(Constants.CommonPaths.UserSettingsPath, legacy.ToJsonString());
        environment.StartSettingsServer();
        using var client = new UserSettingsClient(environment.CreateChannel());
        string defaultBuiltIn = Constants.CommonPaths.GetBuiltInWallpapersDir(userDirectory);
        Assert.AreEqual(defaultBuiltIn, client.Settings.BuiltInWallpapersDir);
        Assert.AreEqual(defaultBuiltIn, JsonNode.Parse(File.ReadAllText(Constants.CommonPaths.UserSettingsPath))!["BuiltInWallpapersDir"]!.GetValue<string>());

        string explicitDirectory = Path.Combine(environment.Root, "independent built-ins");
        client.Settings.BuiltInWallpapersDir = explicitDirectory;
        await client.SaveAsync<ISettings>().WaitAsync(token);
        var legacyClient = new Grpc_UserSettingsService.Grpc_UserSettingsServiceClient(environment.CreateChannel());
        var request = await legacyClient.GetSettingsAsync(new Empty(), cancellationToken: token);
        request.ClearBuiltInWallpapersDir();
        request.WallpaperWaitTime = 74;
        await legacyClient.SetSettingsAsync(request, cancellationToken: token);

        environment.StartSettingsServer();
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        Assert.AreEqual(userDirectory, restarted.Settings.WallpaperDir);
        Assert.AreEqual(explicitDirectory, restarted.Settings.BuiltInWallpapersDir);
        Assert.AreEqual(74, restarted.Settings.WallpaperWaitTime);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [Timeout(30000, CooperativeCancellation = true)]
    public async Task Client_SaveToDisk_ReloadAndRestart_PreservesPreferences(bool animations) {
        using var environment = new WorkflowEnvironment();
        var token = TestContext.CancellationTokenSource.Token;
        token.ThrowIfCancellationRequested();
        environment.StartSettingsServer();
        using (var client = new UserSettingsClient(environment.CreateChannel())) {
            client.Settings.NavigationAnimationsEnabled = animations;
            client.Settings.RemindUnpreservedIntelligentResults = animations;
            client.Settings.WallpaperWaitTime = 137;
            client.Settings.WaitingTime = 9;
            client.Settings.WhiteListScr = [new ProcInfo { ProcName = "workflow.exe", IconPath = "workflow.ico" }];
            await client.SaveAsync<ISettings>().WaitAsync(token);

            var json = JsonNode.Parse(File.ReadAllText(Constants.CommonPaths.UserSettingsPath))!;
            Assert.AreEqual(animations, json["NavigationAnimationsEnabled"]!.GetValue<bool>());
            Assert.AreEqual(animations, json["RemindUnpreservedIntelligentResults"]!.GetValue<bool>());
            Assert.AreEqual(137, json["WallpaperWaitTime"]!.GetValue<int>());
            var previous = client.Settings;
            client.Settings.NavigationAnimationsEnabled = !animations;
            client.Settings.RemindUnpreservedIntelligentResults = !animations;
            await client.LoadAsync<ISettings>().WaitAsync(token);
            Assert.AreNotSame(previous, client.Settings);
            AssertPreferences(client.Settings, animations);
        }

        environment.StartSettingsServer(); // Recreate the real service from disk, not a cached Settings object.
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        AssertPreferences(restarted.Settings, animations);
        environment.UiRunner.Verify(r => r.RestartUIAsync(), Times.Never);
    }

    [TestMethod]
    [Timeout(30000, CooperativeCancellation = true)]
    public async Task LegacyConfigAndOmittedField_PreserveDefaultAndSavedPreference() {
        using var environment = new WorkflowEnvironment();
        var token = TestContext.CancellationTokenSource.Token;
        token.ThrowIfCancellationRequested();
        File.WriteAllText(Constants.CommonPaths.UserSettingsPath, "{\"WallpaperWaitTime\":73}");
        environment.StartSettingsServer();
        using var client = new UserSettingsClient(environment.CreateChannel());
        Assert.IsTrue(client.Settings.NavigationAnimationsEnabled);
        Assert.IsTrue(client.Settings.RemindUnpreservedIntelligentResults);
        Assert.AreEqual(73, client.Settings.WallpaperWaitTime);
        client.Settings.NavigationAnimationsEnabled = false;
        client.Settings.RemindUnpreservedIntelligentResults = false;
        await client.SaveAsync<ISettings>().WaitAsync(token);

        var legacyClient = new Grpc_UserSettingsService.Grpc_UserSettingsServiceClient(environment.CreateChannel());
        var request = await legacyClient.GetSettingsAsync(new Empty(), cancellationToken: token);
        request.ClearNavigationAnimationsEnabled();
        request.ClearRemindUnpreservedIntelligentResults();
        request.WallpaperWaitTime = 74;
        await legacyClient.SetSettingsAsync(request, cancellationToken: token);
        token.ThrowIfCancellationRequested();
        client.Load<ISettings>();
        Assert.IsFalse(client.Settings.NavigationAnimationsEnabled);
        Assert.IsFalse(client.Settings.RemindUnpreservedIntelligentResults);
        Assert.AreEqual(74, client.Settings.WallpaperWaitTime);

        environment.StartSettingsServer();
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        Assert.IsFalse(restarted.Settings.NavigationAnimationsEnabled);
        Assert.IsFalse(restarted.Settings.RemindUnpreservedIntelligentResults);
        Assert.AreEqual(74, restarted.Settings.WallpaperWaitTime);
    }

    [TestMethod]
    [Timeout(30000, CooperativeCancellation = true)]
    public async Task CorruptSettings_RecoverToReadableDefaults_ThenPersistThroughClient() {
        using var environment = new WorkflowEnvironment();
        var token = TestContext.CancellationTokenSource.Token;
        token.ThrowIfCancellationRequested();
        File.WriteAllText(Constants.CommonPaths.UserSettingsPath, "{broken json");
        environment.StartSettingsServer();
        using var client = new UserSettingsClient(environment.CreateChannel());
        Assert.IsTrue(client.Settings.NavigationAnimationsEnabled);
        Assert.IsTrue(client.Settings.RemindUnpreservedIntelligentResults);
        Assert.IsNotNull(JsonNode.Parse(File.ReadAllText(Constants.CommonPaths.UserSettingsPath)));
        client.Settings.WaitingTime = 12;
        await client.SaveAsync<ISettings>().WaitAsync(token);
        environment.StartSettingsServer();
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        Assert.AreEqual(12, restarted.Settings.WaitingTime);
    }

    [TestMethod]
    public async Task AiUseNotice_LegacyProfileStartsUnacknowledged_AndClientPersistsAcrossRestart() {
        using var environment = new WorkflowEnvironment();
        var token = TestContext.CancellationTokenSource.Token;
        File.WriteAllText(Constants.CommonPaths.UserSettingsPath, "{\"WallpaperWaitTime\":73}");
        environment.StartSettingsServer();
        using (var client = new UserSettingsClient(environment.CreateChannel())) {
            Assert.AreEqual(0, client.Settings.AiUseNoticeVersion);
            client.Settings.AiUseNoticeVersion = 1;
            await client.SaveAsync<ISettings>().WaitAsync(token);
            Assert.AreEqual(1, JsonNode.Parse(File.ReadAllText(Constants.CommonPaths.UserSettingsPath))!["AiUseNoticeVersion"]!.GetValue<int>());
        }
        environment.StartSettingsServer();
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        Assert.AreEqual(1, restarted.Settings.AiUseNoticeVersion);
        var legacy = new Grpc_UserSettingsService.Grpc_UserSettingsServiceClient(environment.CreateChannel());
        var request = await legacy.GetSettingsAsync(new Empty(), cancellationToken: token);
        request.ClearAiUseNoticeVersion();
        request.WallpaperWaitTime = 74;
        await legacy.SetSettingsAsync(request, cancellationToken: token);
        await restarted.LoadAsync<ISettings>().WaitAsync(token);
        Assert.AreEqual(1, restarted.Settings.AiUseNoticeVersion);
        Assert.AreEqual(74, restarted.Settings.WallpaperWaitTime);
    }

    private static void AssertPreferences(ISettings settings, bool animations) {
        Assert.AreEqual(animations, settings.NavigationAnimationsEnabled);
        Assert.AreEqual(animations, settings.RemindUnpreservedIntelligentResults);
        Assert.AreEqual(137, settings.WallpaperWaitTime);
        Assert.AreEqual(9, settings.WaitingTime);
        Assert.HasCount(1, settings.WhiteListScr);
        Assert.AreEqual("workflow.exe", settings.WhiteListScr[0].ProcName);
        Assert.AreEqual("WORKFLOW-DISPLAY-1", settings.SelectedMonitor.DeviceId);
    }
}
