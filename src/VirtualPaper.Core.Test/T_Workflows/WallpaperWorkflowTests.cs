using System.Security.Cryptography;
using System.Text.Json;
using Grpc.Core;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.DataAssistor;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Core.Test.T_Workflows;

[TestClass]
[DoNotParallelize]
[TestCategory("Workflow")]
public class WallpaperWorkflowTests {
    [TestMethod]
    public async Task MovedLibrary_ReindexesExistingViewModel_AndUsesSavedDirectory() {
        using var environment = new WallpaperWorkflowEnvironment();
        await environment.ImportAsync();
        await MoveImportedWallpaperToBuiltInLibraryAsync(environment);
        var (library, index) = await environment.OpenLibraryAsync();
        string previousUser = environment.SettingsClient.Settings.WallpaperDir;
        string previousBuiltIn = environment.SettingsClient.Settings.BuiltInWallpapersDir;
        string targetUser = Path.Combine(environment.Environment.Root, "moved library", "wallpapers");
        string targetBuiltIn = Constants.CommonPaths.GetBuiltInWallpapersDir(targetUser);
        VirtualPaper.Common.Utils.Files.FileUtil.CopyDirectory(previousUser, targetUser, true);
        VirtualPaper.Common.Utils.Files.FileUtil.CopyDirectory(previousBuiltIn, targetBuiltIn, true);
        environment.SettingsClient.Settings.WallpaperDir = targetUser;
        environment.SettingsClient.Settings.BuiltInWallpapersDir = targetBuiltIn;
        await environment.SettingsClient.SaveAsync<ISettings>();

        await library.InitContentAsync();

        var entries = index.Query(0, 10);
        Assert.HasCount(2, entries);
        Assert.IsTrue(entries.All(entry => entry.FolderPath.StartsWith(Path.GetDirectoryName(targetUser)!, StringComparison.OrdinalIgnoreCase)));
        await environment.SettingsClient.LoadAsync<ISettings>();
        Assert.AreEqual(targetUser, environment.SettingsClient.Settings.WallpaperDir);
        Assert.AreEqual(targetBuiltIn, environment.SettingsClient.Settings.BuiltInWallpapersDir);
    }

    [TestMethod]
    public async Task LayoutDirectoryChange_DoesNotRewriteSiblingPathsWithTheSamePrefix() {
        using var environment = new WallpaperWorkflowEnvironment();
        string source = environment.SettingsClient.Settings.WallpaperDir;
        string target = Path.Combine(environment.Environment.Root, "moved", "wallpapers");
        var own = new WallpaperLayout(Path.Combine(source, "own"), "", "", "RImage");
        var sibling = new WallpaperLayout(Path.Combine(source + "-backup", "other"), "", "", "RImage");
        environment.Environment.Settings.WallpaperLayouts.Add(own);
        environment.Environment.Settings.WallpaperLayouts.Add(sibling);
        await environment.Client.ChangeWallpaperLayoutFolderPathAsync(source, target);
        Assert.AreEqual(Path.Combine(target, "own"), own.FolderPath);
        Assert.AreEqual(Path.Combine(source + "-backup", "other"), sibling.FolderPath);
    }

    [TestMethod]
    public async Task BuiltInLibrary_IsScannedAlongsideUserLibrary_AndDeletesIndependently() {
        using var environment = new WallpaperWorkflowEnvironment();
        var userWallpaper = await environment.ImportAsync();
        var builtInWallpaper = await MoveImportedWallpaperToBuiltInLibraryAsync(environment);

        var (library, index) = await environment.OpenLibraryAsync();
        CollectionAssert.AreEquivalent(new[] { userWallpaper.WallpaperUid, builtInWallpaper.WallpaperUid },
            index.Query(0, 10).Select(entry => entry.Uid).ToArray());
        await library.DeleteAsync(builtInWallpaper);

        Assert.IsFalse(Directory.Exists(builtInWallpaper.FolderPath));
        Assert.IsTrue(File.Exists(userWallpaper.FilePath));
        var reopened = await environment.OpenLibraryAsync();
        Assert.AreEqual(userWallpaper.WallpaperUid, reopened.Index.Query(0, 10).Single().Uid);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PreviewApply_PersistsEffectsBesideTheActualWallpaper(bool builtIn) {
        using var environment = new WallpaperWorkflowEnvironment();
        var data = builtIn ? await MoveImportedWallpaperToBuiltInLibraryAsync(environment) : await environment.ImportAsync();
        var preview = environment.Control.CreateRuntimeData(data.FilePath, data.FolderPath,
            RuntimeType.RImage, null, true, environment.Monitor.Content)!;
        const string effect = "{\"KeepMyEffect\":73}";
        File.WriteAllText(preview.WpEffectFilePathUsing, effect);

        var applied = await environment.Client.SetWallpaperAsync(environment.Monitor, data, RuntimeType.RImage, true,
            null, preview.WpEffectFilePathUsing, preview.WpEffectFilePathTemplate,
            preview.WpEffectFilePathTemporary, CancellationToken.None);
        Assert.IsTrue(applied.IsFinished);
        string runtimePath = Path.Combine(data.FolderPath, environment.Monitor.Content,
            RuntimeType.RImage.ToString(), Constants.Field.WpRuntimeDataFileName);
        var runtime = WpRuntimeData.Load(runtimePath);
        Assert.AreEqual(data.FolderPath, runtime.FolderPath);
        Assert.AreEqual(effect, File.ReadAllText(runtime.WpEffectFilePathUsing));
        foreach (string path in new[] { runtime.WpEffectFilePathUsing, runtime.WpEffectFilePathTemporary, runtime.WpEffectFilePathTemplate }) {
            Assert.IsTrue(path.StartsWith(data.FolderPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), path);
        }
        if (builtIn) {
            Assert.IsFalse(Directory.Exists(Path.Combine(environment.SettingsClient.Settings.WallpaperDir,
                Path.GetFileName(data.FolderPath))), "Built-in preview settings leaked into the user library.");
        }
        await environment.SettingsClient.LoadAsync<List<IWallpaperLayout>>();
        Assert.AreEqual(data.FolderPath, environment.SettingsClient.WallpaperLayouts.Single().FolderPath);
    }

    private static async Task<IWpBasicData> MoveImportedWallpaperToBuiltInLibraryAsync(WallpaperWorkflowEnvironment environment) {
        var imported = await environment.ImportAsync();
        string folder = Path.Combine(environment.SettingsClient.Settings.BuiltInWallpapersDir, Path.GetFileName(imported.FolderPath));
        VirtualPaper.Common.Utils.Files.FileUtil.CopyDirectory(imported.FolderPath, folder, true);
        Directory.Delete(imported.FolderPath, recursive: true);
        return WpBasicData.Load(Path.Combine(folder, Constants.Field.WpBasicDataFileName));
    }

    [TestMethod]
    [DataRow(FileType.FImage)]
    [DataRow(FileType.FWebZip)]
    public async Task Upgrade18ThroughRpc_PreservesContentAndConvertsEveryRuntime(FileType type) {
        using var environment = new WallpaperWorkflowEnvironment();
        var data = await environment.ImportAsync();
        data.WallpaperUid = "existing-user-wallpaper";
        data.Title = "My saved title";
        data.FType = type;
        if (type == FileType.FWebZip) {
            data.FilePath = Path.Combine(data.FolderPath, "index.html");
            File.WriteAllText(data.FilePath, "<html>Existing Web wallpaper</html>");
            File.WriteAllText(Path.Combine(data.FolderPath, "project.json"), "{\"title\":\"My saved title\",\"file\":\"index.html\",\"customField\":42}");
        }
        data.AppInfo.FileVersion = "18";
        string basicPath = Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName);
        JsonSaver.Save(basicPath, data, WpBasicDataContext.Default);
        string depth = Path.Combine(data.FolderPath, "depth-existing.png");
        File.WriteAllText(depth, "preserve the existing depth cache");
        RuntimeType[] runtimeTypes = type == FileType.FImage ? [RuntimeType.RImage, RuntimeType.RImage3D] : [RuntimeType.RWeb];
        foreach (string monitor in new[] { "display-a", "display-b" }) {
            foreach (var runtimeType in runtimeTypes) {
                var runtime = environment.Control.CreateRuntimeData(data.FilePath, data.FolderPath, runtimeType,
                    runtimeType == RuntimeType.RImage3D ? depth : null, false, monitor);
                runtime.AppInfo.FileVersion = "18";
                File.WriteAllText(runtime.WpEffectFilePathUsing, "{\"KeepMyEffect\":73}");
                JsonSaver.Save(Path.Combine(data.FolderPath, monitor, runtimeType.ToString(), Constants.Field.WpRuntimeDataFileName),
                    runtime, WpRuntimeDataContext.Default);
            }
        }
        var contentHashes = Directory.GetFiles(data.FolderPath, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != Constants.Field.WpBasicDataFileName && Path.GetFileName(path) != Constants.Field.WpRuntimeDataFileName)
            .ToDictionary(path => path, path => SHA256.HashData(File.ReadAllBytes(path)));
        var legacyLibrary = await environment.OpenLibraryAsync();
        Assert.AreEqual("18", legacyLibrary.Library.LibraryWallpapers.Single().AppInfo.FileVersion);
        Assert.AreEqual(data.FilePath, legacyLibrary.Library.LibraryWallpapers.Single().FilePath);

        var response = await environment.Client.UpdateBasicDataAsync(data.FolderPath, data.FolderName, data.FilePath, type);
        var upgraded = DataAssist.GrpcToBasicData(response!);
        Assert.AreEqual(data.WallpaperUid, upgraded.WallpaperUid);
        Assert.AreEqual(data.Title, upgraded.Title);
        Assert.AreEqual(data.CreatedTime, upgraded.CreatedTime);
        Assert.AreEqual("19", upgraded.AppInfo.FileVersion);
        AssertStoredRelative(basicPath, "FolderPath", "FilePath", "ThumbnailPath");
        foreach (string runtimePath in Directory.GetFiles(data.FolderPath, Constants.Field.WpRuntimeDataFileName, SearchOption.AllDirectories)) {
            AssertStoredRelative(runtimePath, "FolderPath", "DepthFilePath", "WpEffectFilePathUsing", "WpEffectFilePathTemplate", "WpEffectFilePathTemporary");
            var runtime = WpRuntimeData.Load(runtimePath);
            Assert.AreEqual("19", runtime.AppInfo.FileVersion);
            Assert.AreEqual("{\"KeepMyEffect\":73}", File.ReadAllText(runtime.WpEffectFilePathUsing));
        }
        foreach (var (path, hash) in contentHashes) CollectionAssert.AreEqual(hash, SHA256.HashData(File.ReadAllBytes(path)), path);
        // Clicking Update again must be idempotent, including for Web folders.
        await environment.Client.UpdateBasicDataAsync(upgraded.FolderPath, upgraded.FolderName, upgraded.FilePath, type);
        var reopened = await environment.OpenLibraryAsync();
        Assert.AreEqual(data.WallpaperUid, reopened.Library.LibraryWallpapers.Single().WallpaperUid);
        Assert.AreEqual("19", reopened.Library.LibraryWallpapers.Single().AppInfo.FileVersion);
    }

    [TestMethod]
    public async Task Upgrade18WithBrokenRuntime_ReportsFailureAndPreservesOriginalFiles() {
        using var environment = new WallpaperWorkflowEnvironment();
        var data = await environment.ImportAsync();
        data.AppInfo.FileVersion = "18";
        string basicPath = Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName);
        JsonSaver.Save(basicPath, data, WpBasicDataContext.Default);
        string brokenPath = Path.Combine(data.FolderPath, "display", "RImage", Constants.Field.WpRuntimeDataFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(brokenPath)!);
        File.WriteAllText(brokenPath, "broken runtime JSON");
        byte[] before = File.ReadAllBytes(basicPath);

        await Assert.ThrowsAsync<RpcException>(async () =>
            await environment.Client.UpdateBasicDataAsync(data.FolderPath, data.FolderName, data.FilePath, data.FType));

        CollectionAssert.AreEqual(before, File.ReadAllBytes(basicPath));
        Assert.AreEqual("broken runtime JSON", File.ReadAllText(brokenPath));
        Assert.IsTrue(File.Exists(data.FilePath));
        Assert.IsTrue(File.Exists(data.ThumbnailPath));
    }

    private static void AssertStoredRelative(string metadataPath, params string[] names) {
        using var json = JsonDocument.Parse(File.ReadAllText(metadataPath));
        Assert.AreEqual("19", json.RootElement.GetProperty("AppInfo").GetProperty("FileVersion").GetString());
        foreach (string name in names) Assert.IsFalse(Path.IsPathRooted(json.RootElement.GetProperty(name).GetString()!), name);
    }

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
