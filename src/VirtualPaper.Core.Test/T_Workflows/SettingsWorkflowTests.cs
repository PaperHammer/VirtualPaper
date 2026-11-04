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
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [Timeout(30000)]
    public async Task Client_SaveToDisk_ReloadAndRestart_PreservesPreferences(bool animations) {
        using var environment = new WorkflowEnvironment();
        environment.StartSettingsServer();
        using (var client = new UserSettingsClient(environment.CreateChannel())) {
            client.Settings.NavigationAnimationsEnabled = animations;
            client.Settings.WallpaperWaitTime = 137;
            client.Settings.WaitingTime = 9;
            client.Settings.WhiteListScr = [new ProcInfo { ProcName = "workflow.exe", IconPath = "workflow.ico" }];
            await client.SaveAsync<ISettings>();

            var json = JsonNode.Parse(File.ReadAllText(Constants.CommonPaths.UserSettingsPath))!;
            Assert.AreEqual(animations, json["NavigationAnimationsEnabled"]!.GetValue<bool>());
            Assert.AreEqual(137, json["WallpaperWaitTime"]!.GetValue<int>());
            var previous = client.Settings;
            client.Settings.NavigationAnimationsEnabled = !animations;
            await client.LoadAsync<ISettings>();
            Assert.AreNotSame(previous, client.Settings);
            AssertPreferences(client.Settings, animations);
        }

        environment.StartSettingsServer(); // Recreate the real service from disk, not a cached Settings object.
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        AssertPreferences(restarted.Settings, animations);
        environment.UiRunner.Verify(r => r.RestartUI(), Times.Never);
    }

    [TestMethod]
    [Timeout(30000)]
    public async Task LegacyConfigAndOmittedField_PreserveDefaultAndSavedPreference() {
        using var environment = new WorkflowEnvironment();
        File.WriteAllText(Constants.CommonPaths.UserSettingsPath, "{\"WallpaperWaitTime\":73}");
        environment.StartSettingsServer();
        using var client = new UserSettingsClient(environment.CreateChannel());
        Assert.IsTrue(client.Settings.NavigationAnimationsEnabled);
        Assert.AreEqual(73, client.Settings.WallpaperWaitTime);
        client.Settings.NavigationAnimationsEnabled = false;
        await client.SaveAsync<ISettings>();

        var legacyClient = new Grpc_UserSettingsService.Grpc_UserSettingsServiceClient(environment.CreateChannel());
        var request = await legacyClient.GetSettingsAsync(new Empty());
        request.ClearNavigationAnimationsEnabled();
        request.WallpaperWaitTime = 74;
        await legacyClient.SetSettingsAsync(request);
        client.Load<ISettings>();
        Assert.IsFalse(client.Settings.NavigationAnimationsEnabled);
        Assert.AreEqual(74, client.Settings.WallpaperWaitTime);

        environment.StartSettingsServer();
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        Assert.IsFalse(restarted.Settings.NavigationAnimationsEnabled);
        Assert.AreEqual(74, restarted.Settings.WallpaperWaitTime);
    }

    [TestMethod]
    [Timeout(30000)]
    public async Task CorruptSettings_RecoverToReadableDefaults_ThenPersistThroughClient() {
        using var environment = new WorkflowEnvironment();
        File.WriteAllText(Constants.CommonPaths.UserSettingsPath, "{broken json");
        environment.StartSettingsServer();
        using var client = new UserSettingsClient(environment.CreateChannel());
        Assert.IsTrue(client.Settings.NavigationAnimationsEnabled);
        Assert.IsNotNull(JsonNode.Parse(File.ReadAllText(Constants.CommonPaths.UserSettingsPath)));
        client.Settings.WaitingTime = 12;
        await client.SaveAsync<ISettings>();
        environment.StartSettingsServer();
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        Assert.AreEqual(12, restarted.Settings.WaitingTime);
    }

    private static void AssertPreferences(ISettings settings, bool animations) {
        Assert.AreEqual(animations, settings.NavigationAnimationsEnabled);
        Assert.AreEqual(137, settings.WallpaperWaitTime);
        Assert.AreEqual(9, settings.WaitingTime);
        Assert.HasCount(1, settings.WhiteListScr);
        Assert.AreEqual("workflow.exe", settings.WhiteListScr[0].ProcName);
        Assert.AreEqual("WORKFLOW-DISPLAY-1", settings.SelectedMonitor.DeviceId);
    }
}
