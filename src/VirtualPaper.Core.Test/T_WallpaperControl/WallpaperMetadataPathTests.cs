using System.IO;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores;

namespace VirtualPaper.Core.Test.T_WallpaperControl;

[TestClass]
public class WallpaperMetadataPathTests {
    private string _root = null!;

    [TestInitialize]
    public void Initialize() => _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "vp-metadata-" + Guid.NewGuid().ToString("N"))).FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    [TestMethod]
    [DataRow(FileType.FImage)]
    [DataRow(FileType.FGif)]
    [DataRow(FileType.FVideo)]
    [DataRow(FileType.FWebZip)]
    public async Task BasicSave_StoresRelativePathsWithoutChangingTheLiveObject(FileType type) {
        var data = CreateBasic(type);
        string metadata = Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName);
        await data.SaveAsync();

        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(metadata));
        Assert.AreEqual(".", json.RootElement.GetProperty("FolderPath").GetString());
        Assert.AreEqual("assets/image.png", json.RootElement.GetProperty("FilePath").GetString());
        Assert.AreEqual("preview.gif", json.RootElement.GetProperty("ThumbnailPath").GetString());
        Assert.IsTrue(Path.IsPathFullyQualified(data.FilePath));
        var loaded = await WpBasicData.LoadAsync(metadata);
        Assert.AreEqual(data.FilePath, loaded!.FilePath);
        Assert.AreEqual(data.ThumbnailPath, loaded.ThumbnailPath);
        Assert.AreEqual(data.WallpaperUid, loaded.WallpaperUid);
    }

    [TestMethod]
    public async Task CopyingTheWholeFolder_PreservesBasicAndRuntimePaths() {
        var basic = CreateBasic();
        await basic.SaveAsync();
        var runtime = CreateRuntime(basic.FolderPath);
        runtime.Save();
        string runtimeRelative = Path.Combine(runtime.MonitorContent, runtime.RType.ToString(), Constants.Field.WpRuntimeDataFileName);
        string originalRuntimePath = Path.Combine(basic.FolderPath, runtimeRelative);
        using (var json = JsonDocument.Parse(File.ReadAllText(originalRuntimePath))) {
            foreach (string name in new[] { "FolderPath", "DepthFilePath", "WpEffectFilePathTemplate", "WpEffectFilePathUsing", "WpEffectFilePathTemporary" }) {
                string stored = json.RootElement.GetProperty(name).GetString()!;
                Assert.IsFalse(Path.IsPathRooted(stored), name);
                Assert.AreEqual(typeof(WpRuntimeData).GetProperty(name)!.GetValue(runtime),
                    Path.GetFullPath(stored, Path.GetDirectoryName(originalRuntimePath)!));
            }
            Assert.AreEqual("display", json.RootElement.GetProperty("MonitorContent").GetString());
        }

        string moved = Path.Combine(_root, "another library", "scene");
        Directory.CreateDirectory(Path.GetDirectoryName(moved)!);
        Directory.Move(basic.FolderPath, moved);
        var restoredBasic = WpBasicData.Load(Path.Combine(moved, Constants.Field.WpBasicDataFileName));
        var restoredRuntime = await WpRuntimeData.LoadAsync(Path.Combine(moved, runtimeRelative));

        Assert.AreEqual(moved, restoredBasic.FolderPath);
        Assert.AreEqual("pixels", File.ReadAllText(restoredBasic.FilePath));
        Assert.AreEqual("depth", File.ReadAllText(restoredRuntime.DepthFilePath));
        Assert.AreEqual("effect", File.ReadAllText(restoredRuntime.WpEffectFilePathUsing));
        Assert.AreEqual(moved, restoredRuntime.FolderPath);
        Assert.IsFalse(Directory.Exists(basic.FolderPath));
    }

    [TestMethod]
    public async Task LegacyAbsoluteFiles_ReadNormallyAndBecomeRelativeWhenSaved() {
        var basic = CreateBasic();
        basic.AppInfo.FileVersion = "18";
        string basicPath = Path.Combine(basic.FolderPath, Constants.Field.WpBasicDataFileName);
        JsonSaver.Save(basicPath, basic, WpBasicDataContext.Default);
        var runtime = CreateRuntime(basic.FolderPath);
        runtime.AppInfo.FileVersion = "18";
        string runtimePath = Path.Combine(runtime.FolderPath, runtime.MonitorContent, runtime.RType.ToString(), Constants.Field.WpRuntimeDataFileName);
        JsonSaver.Save(runtimePath, runtime, WpRuntimeDataContext.Default);
        string originalBasic = File.ReadAllText(basicPath);
        string originalRuntime = File.ReadAllText(runtimePath);

        var restoredBasic = WpBasicData.Load(basicPath);
        var restoredRuntime = new WpRuntimeData();
        restoredRuntime.Read(runtimePath);
        var asyncRuntime = await WpRuntimeData.LoadAsync(runtimePath);
        Assert.AreEqual(restoredRuntime.DepthFilePath, asyncRuntime.DepthFilePath);
        Assert.AreEqual(restoredRuntime.WpEffectFilePathUsing, asyncRuntime.WpEffectFilePathUsing);
        Assert.AreEqual(basic.FilePath, restoredBasic.FilePath);
        Assert.AreEqual(runtime.DepthFilePath, restoredRuntime.DepthFilePath);
        Assert.AreEqual(runtime.MonitorContent, restoredRuntime.MonitorContent);
        Assert.AreEqual("18", restoredBasic.AppInfo.FileVersion);
        Assert.AreEqual("18", restoredRuntime.AppInfo.FileVersion);
        Assert.AreEqual(originalBasic, File.ReadAllText(basicPath));
        Assert.AreEqual(originalRuntime, File.ReadAllText(runtimePath));
        await restoredBasic.SaveAsync();
        restoredRuntime.Save();
        using var basicJson = JsonDocument.Parse(File.ReadAllText(basicPath));
        using var runtimeJson = JsonDocument.Parse(File.ReadAllText(runtimePath));
        Assert.IsFalse(Path.IsPathRooted(basicJson.RootElement.GetProperty("FilePath").GetString()!));
        Assert.IsFalse(Path.IsPathRooted(runtimeJson.RootElement.GetProperty("DepthFilePath").GetString()!));
        Assert.AreEqual("19", basicJson.RootElement.GetProperty("AppInfo").GetProperty("FileVersion").GetString());
        Assert.AreEqual("19", runtimeJson.RootElement.GetProperty("AppInfo").GetProperty("FileVersion").GetString());
    }

    [TestMethod]
    public async Task FailedSave_DoesNotAdvanceTheLiveMetadataVersion() {
        var basic = CreateBasic();
        basic.AppInfo.FileVersion = "18";
        string basicPath = Path.Combine(basic.FolderPath, Constants.Field.WpBasicDataFileName);
        JsonSaver.Save(basicPath, basic, WpBasicDataContext.Default);
        using (var readOnly = File.Open(basicPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            await Assert.ThrowsAsync<VirtualPaper.Common.Errors.FileAccessException>(() => basic.SaveMetadataAsync("19"));
        }
        Assert.AreEqual("18", basic.AppInfo.FileVersion);
        Assert.AreEqual("18", WpBasicData.Load(basicPath).AppInfo.FileVersion);

        var runtime = CreateRuntime(basic.FolderPath);
        runtime.AppInfo.FileVersion = "18";
        string runtimePath = Path.Combine(runtime.FolderPath, runtime.MonitorContent, runtime.RType.ToString(), Constants.Field.WpRuntimeDataFileName);
        JsonSaver.Save(runtimePath, runtime, WpRuntimeDataContext.Default);
        using (var readOnly = File.Open(runtimePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            Assert.Throws<VirtualPaper.Common.Errors.FileAccessException>(() => runtime.Save("19"));
        }
        Assert.AreEqual("18", runtime.AppInfo.FileVersion);
        Assert.AreEqual("18", WpRuntimeData.Load(runtimePath).AppInfo.FileVersion);
    }

    [TestMethod]
    public async Task EmptyOptionalPaths_RemainEmpty() {
        var basic = CreateBasic();
        basic.ThumbnailPath = "";
        await basic.SaveAsync();
        Assert.AreEqual("", WpBasicData.Load(Path.Combine(basic.FolderPath, Constants.Field.WpBasicDataFileName)).ThumbnailPath);
        var runtime = CreateRuntime(basic.FolderPath);
        runtime.DepthFilePath = "";
        runtime.Save();
        Assert.AreEqual("", WpRuntimeData.Load(Path.Combine(runtime.FolderPath, runtime.MonitorContent,
            runtime.RType.ToString(), Constants.Field.WpRuntimeDataFileName)).DepthFilePath);
    }

    [TestMethod]
    public async Task MoveTo_LeavesRuntimeMetadataPortable() {
        var basic = CreateBasic();
        await basic.SaveAsync();
        var runtime = CreateRuntime(basic.FolderPath);
        runtime.Save();
        string original = basic.FolderPath;
        string destination = Path.Combine(_root, "moved");
        await basic.MoveToAsync(destination);
        Directory.Delete(original, true);

        var restored = WpRuntimeData.Load(Path.Combine(destination, runtime.MonitorContent, runtime.RType.ToString(), Constants.Field.WpRuntimeDataFileName));
        Assert.AreEqual(destination, restored.FolderPath);
        Assert.AreEqual("effect", File.ReadAllText(restored.WpEffectFilePathUsing));
    }

    private WpBasicData CreateBasic(FileType type = FileType.FImage) {
        string folder = Path.Combine(_root, "scene");
        Directory.CreateDirectory(Path.Combine(folder, "assets"));
        File.WriteAllText(Path.Combine(folder, "assets", "image.png"), "pixels");
        File.WriteAllText(Path.Combine(folder, "preview.gif"), "preview");
        return new() {
            WallpaperUid = "portable-scene", FType = type, AppInfo = new() { AppVersion = "1.0.0" },
            FolderName = "scene", FolderPath = folder,
            FilePath = Path.Combine(folder, "assets", "image.png"), ThumbnailPath = Path.Combine(folder, "preview.gif")
        };
    }

    private static WpRuntimeData CreateRuntime(string folder) {
        string effects = Path.Combine(folder, "display", RuntimeType.RImage3D.ToString());
        Directory.CreateDirectory(effects);
        string depth = Path.Combine(folder, "depth-cache.png");
        File.WriteAllText(depth, "depth");
        foreach (string name in new[] { "template.json", "using.json", "temporary.json" }) File.WriteAllText(Path.Combine(effects, name), "effect");
        return new() {
            FolderPath = folder, MonitorContent = "display", RType = RuntimeType.RImage3D,
            DepthFilePath = depth, WpEffectFilePathTemplate = Path.Combine(effects, "template.json"),
            WpEffectFilePathUsing = Path.Combine(effects, "using.json"), WpEffectFilePathTemporary = Path.Combine(effects, "temporary.json")
        };
    }
}
