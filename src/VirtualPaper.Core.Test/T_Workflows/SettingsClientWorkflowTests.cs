using System.Globalization;
using Grpc.Core;
using VirtualPaper.Common;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.Grpc.Client;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Core.Test.T_Workflows;

[TestClass]
[DoNotParallelize]
public sealed class SettingsClientWorkflowTests {
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Rules_SaveReloadAndRestart_PreserveValuesAndReplaceTheOldInventory(bool asynchronous) {
        using var environment = new WorkflowEnvironment();
        environment.StartSettingsServer();
        using var client = new UserSettingsClient(environment.CreateChannel());
        client.AppRules.Clear();
        client.AppRules.Add(new ApplicationRules("编辑器.exe", AppWpRunRulesEnum.KeepRun));
        client.AppRules.Add(new ApplicationRules("video.exe", AppWpRunRulesEnum.Pause));
        if (asynchronous) await client.SaveAsync<List<IApplicationRules>>();
        else client.Save<List<IApplicationRules>>();
        client.AppRules.Clear();
        if (asynchronous) await client.LoadAsync<List<IApplicationRules>>();
        else client.Load<List<IApplicationRules>>();
        AssertRules(client);
        environment.StartSettingsServer();
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        AssertRules(restarted);
        restarted.AppRules.Clear();
        await restarted.SaveAsync<List<IApplicationRules>>();
        await restarted.LoadAsync<List<IApplicationRules>>();
        Assert.IsEmpty(restarted.AppRules);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LayoutReload_MapsEveryFieldWithoutKeepingRemovedLayouts(bool asynchronous) {
        using var environment = new WorkflowEnvironment();
        environment.StartSettingsServer();
        var layout = new WallpaperLayout(Path.Combine(environment.Root, "壁纸"), "display-2", "Second display", "RImage3D");
        environment.Settings.WallpaperLayouts.Add(layout);
        environment.Settings.Save<List<IWallpaperLayout>>();
        using var client = new UserSettingsClient(environment.CreateChannel());
        Assert.HasCount(1, client.WallpaperLayouts);
        var loaded = client.WallpaperLayouts[0];
        Assert.AreEqual(layout.FolderPath, loaded.FolderPath);
        Assert.AreEqual(layout.MonitorDeviceId, loaded.MonitorDeviceId);
        Assert.AreEqual(layout.MonitorContent, loaded.MonitorContent);
        Assert.AreEqual(layout.RType, loaded.RType);
        environment.Settings.WallpaperLayouts.Clear();
        if (asynchronous) await client.LoadAsync<List<IWallpaperLayout>>();
        else client.Load<List<IWallpaperLayout>>();
        Assert.IsEmpty(client.WallpaperLayouts);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReopeningARecentProject_MovesItsExistingEntryToTheFront_WithoutDuplicates(bool differentCase) {
        using var environment = new WorkflowEnvironment();
        environment.StartSettingsServer();
        using var client = new UserSettingsClient(environment.CreateChannel());
        string first = Path.Combine(environment.Root, "First.vpd"), second = Path.Combine(environment.Root, "Second.vpw");
        await client.UpdateRecentUsedAsync(first);
        var existing = client.RecentUseds.Single();
        existing.DateTime = "2000-01-01 00:00:00";
        await client.UpdateRecentUsedAsync(second);
        await client.UpdateRecentUsedAsync(differentCase ? first.ToUpperInvariant() : first);
        Assert.HasCount(2, client.RecentUseds);
        Assert.AreSame(existing, client.RecentUseds[0]);
        Assert.AreEqual(FileType.FDesign, existing.Type);
        Assert.AreEqual("First.vpd", existing.FileName);
        Assert.AreEqual(first, existing.FilePath);
        Assert.IsTrue(DateTime.TryParseExact(existing.DateTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture, DateTimeStyles.None, out _));
        Assert.AreNotEqual("2000-01-01 00:00:00", existing.DateTime);
        environment.StartSettingsServer();
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        CollectionAssert.AreEqual(new[] { first, second }, restarted.RecentUseds.Select(item => item.FilePath).ToArray());
        Assert.AreEqual(existing.DateTime, restarted.RecentUseds[0].DateTime);
    }

    [TestMethod]
    public async Task RecentBatchAndDelete_PreserveFinalOrderAcrossRestart() {
        using var environment = new WorkflowEnvironment();
        environment.StartSettingsServer();
        using var client = new UserSettingsClient(environment.CreateChannel());
        string a = Path.Combine(environment.Root, "A.vpd"), b = Path.Combine(environment.Root, "B.vpw"), c = Path.Combine(environment.Root, "C.png");
        await client.UpdateRecetUsedAsync([a, b, c, a]);
        CollectionAssert.AreEqual(new[] { a, c, b }, client.RecentUseds.Select(item => item.FilePath).ToArray());
        byte[] saved = File.ReadAllBytes(Constants.CommonPaths.RecentUsedPath);
        await client.UpdateRecetUsedAsync([]);
        await client.UpdateRecetUsedAsync(null!);
        CollectionAssert.AreEqual(saved, File.ReadAllBytes(Constants.CommonPaths.RecentUsedPath));
        await client.DeleteRecetUsedAsync(client.RecentUseds[1]);
        environment.StartSettingsServer();
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        CollectionAssert.AreEqual(new[] { a, b }, restarted.RecentUseds.Select(item => item.FilePath).ToArray());
        restarted.RecentUseds.Clear();
        restarted.Save<List<IRecentUsed>>();
        await restarted.LoadAsync<List<IRecentUsed>>();
        Assert.IsEmpty(restarted.RecentUseds);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RpcWriteFailure_IsReported_AndKeepsThePersistedRulesAvailableForRetry(bool asynchronous) {
        using var environment = new WorkflowEnvironment();
        environment.StartSettingsServer();
        using var client = new UserSettingsClient(environment.CreateChannel());
        byte[] original = File.ReadAllBytes(Constants.CommonPaths.AppRulesPath);
        client.AppRules.Clear();
        client.AppRules.Add(new ApplicationRules("retry.exe", AppWpRunRulesEnum.Pause));
        using (var locked = File.Open(Constants.CommonPaths.AppRulesPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            if (asynchronous) await Assert.ThrowsAsync<RpcException>(() => client.SaveAsync<List<IApplicationRules>>());
            else Assert.Throws<RpcException>(() => client.Save<List<IApplicationRules>>());
        }
        CollectionAssert.AreEqual(original, File.ReadAllBytes(Constants.CommonPaths.AppRulesPath));
        await client.SaveAsync<List<IApplicationRules>>();
        environment.StartSettingsServer();
        using var restarted = new UserSettingsClient(environment.CreateChannel());
        Assert.HasCount(1, restarted.AppRules);
        Assert.AreEqual("retry.exe", restarted.AppRules[0].AppName);
        Assert.AreEqual(AppWpRunRulesEnum.Pause, restarted.AppRules[0].Rule);
    }

    [TestMethod]
    public async Task UnsupportedDataTypes_FailWithoutReplacingSavedSettings() {
        using var environment = new WorkflowEnvironment();
        environment.StartSettingsServer();
        using var client = new UserSettingsClient(environment.CreateChannel());
        byte[] original = File.ReadAllBytes(Constants.CommonPaths.UserSettingsPath);
        Assert.Throws<InvalidCastException>(() => client.Load<int>());
        await Assert.ThrowsAsync<InvalidCastException>(() => client.LoadAsync<int>());
        Assert.Throws<InvalidCastException>(() => client.Save<int>());
        await Assert.ThrowsAsync<InvalidCastException>(() => client.SaveAsync<int>());
        CollectionAssert.AreEqual(original, File.ReadAllBytes(Constants.CommonPaths.UserSettingsPath));
    }

    private static void AssertRules(UserSettingsClient client) {
        Assert.HasCount(2, client.AppRules);
        CollectionAssert.AreEqual(new[] { "编辑器.exe", "video.exe" }, client.AppRules.Select(rule => rule.AppName).ToArray());
        CollectionAssert.AreEqual(new[] { AppWpRunRulesEnum.KeepRun, AppWpRunRulesEnum.Pause }, client.AppRules.Select(rule => rule.Rule).ToArray());
    }
}
