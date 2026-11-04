using System.Security.Cryptography;
using Grpc.Core;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Core.Test.T_Workflows;

[TestClass]
[DoNotParallelize]
[TestCategory("Workflow")]
public class WallpaperWorkflowTests {
    [TestMethod]
    public async Task Import_Index_Apply_Close_Delete_AndReopenLibrary() {
        using var environment = new WallpaperWorkflowEnvironment();
        var data = await environment.ImportAsync();
        Assert.IsTrue(data.IsAvailable());
        Assert.IsTrue(File.Exists(data.ThumbnailPath));
        CollectionAssert.AreEqual(environment.SourceHash, SHA256.HashData(File.ReadAllBytes(data.FilePath)));
        var (library, index) = await environment.OpenLibraryAsync();
        Assert.AreEqual(data.WallpaperUid, index.Query(0, 10).Single().Uid);
        library.FilterByTitle(data.Title);
        Assert.HasCount(1, library.LibraryWallpapers);

        var applied = await environment.Client.SetWallpaperAsync(environment.Monitor, data, RuntimeType.RImage, false,
            null, null, null, null, CancellationToken.None);
        Assert.IsTrue(applied.IsFinished);
        Assert.HasCount(1, environment.Control.Wallpapers);
        Assert.IsFalse(string.IsNullOrEmpty(await environment.Client.GetPlayerStartArgsByMonitorIdAsync(environment.Monitor.DeviceId, CancellationToken.None)));
        await environment.SettingsClient.LoadAsync<List<IWallpaperLayout>>();
        Assert.AreEqual(data.FolderPath, environment.SettingsClient.WallpaperLayouts.Single().FolderPath);

        await environment.Client.CloseWallpaperAsync(environment.Monitor);
        Assert.IsEmpty(environment.Control.Wallpapers);
        await environment.SettingsClient.LoadAsync<List<IWallpaperLayout>>();
        Assert.IsEmpty(environment.SettingsClient.WallpaperLayouts);
        environment.Players.Single().Verify(p => p.Close(), Times.Once);

        await library.DeleteAsync(library.LibraryWallpapers.Single());
        Assert.IsFalse(Directory.Exists(data.FolderPath));
        Assert.IsEmpty(library.LibraryWallpapers);
        Assert.IsEmpty(index.Query(0, 10));
        var reopened = await environment.OpenLibraryAsync();
        Assert.IsEmpty(reopened.Index.Query(0, 10));
        Assert.IsEmpty(reopened.Library.LibraryWallpapers);
        CollectionAssert.AreEqual(environment.SourceHash, SHA256.HashData(File.ReadAllBytes(environment.SourcePath)));
    }

    [TestMethod]
    public async Task CancelDelete_AndRejectInUseDelete_PreserveFilesAndIndex() {
        using var environment = new WallpaperWorkflowEnvironment();
        var data = await environment.ImportAsync();
        var (library, index) = await environment.OpenLibraryAsync();
        environment.DialogResult = DialogResult.Secondary;
        await library.DeleteAsync(library.LibraryWallpapers.Single());
        Assert.IsTrue(File.Exists(data.FilePath));
        Assert.HasCount(1, index.Query(0, 10));

        environment.DialogResult = DialogResult.Primary;
        var applied = await environment.Client.SetWallpaperAsync(environment.Monitor, data, RuntimeType.RImage, false,
            null, null, null, null, CancellationToken.None);
        Assert.IsTrue(applied.IsFinished);
        await library.DeleteAsync(library.LibraryWallpapers.Single());
        Assert.IsTrue(File.Exists(data.FilePath));
        Assert.HasCount(1, library.LibraryWallpapers);
        Assert.HasCount(1, index.Query(0, 10));
        Assert.HasCount(1, environment.Control.Wallpapers);
    }

    [TestMethod]
    public async Task InvalidImport_RemovesPartialLibraryFolder() {
        using var environment = new WallpaperWorkflowEnvironment();
        await Assert.ThrowsAsync<RpcException>(async () =>
            await environment.Client.CreateBasicDataAsync(Path.Combine(environment.Environment.Root, "missing.png"), FileType.FImage, CancellationToken.None));
        Assert.IsEmpty(Directory.GetDirectories(environment.SettingsClient.Settings.WallpaperDir));
        Assert.IsEmpty(environment.Control.Wallpapers);
    }

    [TestMethod]
    [DataRow(WallpaperArrangement.Per)]
    [DataRow(WallpaperArrangement.Expand)]
    [DataRow(WallpaperArrangement.Duplicate)]
    public async Task PlayerStartFailure_DoesNotReportSuccessOrPersistRunningLayout(WallpaperArrangement arrangement) {
        using var environment = new WallpaperWorkflowEnvironment();
        var data = await environment.ImportAsync();
        environment.Environment.Settings.Settings.WallpaperArrangement = arrangement;
        environment.FailPlayerStart = true;
        var result = await environment.Client.SetWallpaperAsync(environment.Monitor, data, RuntimeType.RImage, false,
            null, null, null, null, CancellationToken.None);
        Assert.IsFalse(result.IsFinished);
        Assert.IsEmpty(environment.Control.Wallpapers);
        await environment.SettingsClient.LoadAsync<List<IWallpaperLayout>>();
        Assert.IsEmpty(environment.SettingsClient.WallpaperLayouts);
    }
}
