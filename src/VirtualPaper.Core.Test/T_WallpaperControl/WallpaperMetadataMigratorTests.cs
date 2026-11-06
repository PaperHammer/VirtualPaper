using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using static VirtualPaper.Common.Errors;

namespace VirtualPaper.Core.Test.T_WallpaperControl;

[TestClass]
public class WallpaperMetadataMigratorTests {
    private string _root = null!;
    private string BasicPath => Path.Combine(_root, Constants.Field.WpBasicDataFileName);

    [TestInitialize]
    public void Initialize() => _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "vp-migration-" + Guid.NewGuid().ToString("N"))).FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    [TestMethod]
    [DataRow("1")]
    [DataRow("17")]
    public async Task OlderFormats_UseTheExistingRebuildEntry(string version) {
        WriteBasic(version);
        int calls = 0;
        var result = await WallpaperMetadataMigrator.MigrateAsync(_root, data => {
            calls++;
            Assert.AreEqual(version, data.AppInfo.FileVersion);
            return data;
        });
        Assert.AreEqual(1, calls);
        Assert.AreEqual("existing-wallpaper", result.WallpaperUid);
    }

    [TestMethod]
    public async Task CurrentVersion_DoesNotRewriteOrRebuild() {
        WriteBasic(Constants.CoreField.FileVersion);
        byte[] before = File.ReadAllBytes(BasicPath);
        // Read access is available; any attempt to rewrite this file must fail.
        using var readOnly = File.Open(BasicPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var result = await WallpaperMetadataMigrator.MigrateAsync(_root, RejectLegacy);
        Assert.AreEqual(Constants.CoreField.FileVersion, result.AppInfo.FileVersion);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(BasicPath));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("D18")]
    [DataRow("invalid")]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("2147483648")]
    public async Task InvalidVersion_IsRejectedWithoutWriting(string version) {
        WriteBasic(version);
        byte[] before = File.ReadAllBytes(BasicPath);
        await Assert.ThrowsAsync<InvalidDataException>(() => WallpaperMetadataMigrator.MigrateAsync(_root, RejectLegacy));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(BasicPath));
    }

    [TestMethod]
    public async Task FutureVersion_IsRejectedWithoutDowngrading() {
        WriteBasic((int.Parse(Constants.CoreField.FileVersion) + 1).ToString());
        byte[] before = File.ReadAllBytes(BasicPath);
        await Assert.ThrowsAsync<NotSupportedException>(() => WallpaperMetadataMigrator.MigrateAsync(_root, RejectLegacy));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(BasicPath));
    }

    [TestMethod]
    public async Task FutureRuntime_IsRejectedBeforeAnyMetadataIsWritten() {
        WriteBasic("18");
        string first = WriteRuntime("display-a", "18");
        string newer = WriteRuntime("display-b", "20");
        string[] paths = [BasicPath, first, newer];
        var before = paths.Select(File.ReadAllBytes).ToArray();
        await Assert.ThrowsAsync<NotSupportedException>(() => WallpaperMetadataMigrator.MigrateAsync(_root, RejectLegacy));
        for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]));
    }

    [TestMethod]
    public async Task FailedBasicWrite_CanResumeAfterRuntimeMigration() {
        WriteBasic("18");
        string runtimePath = WriteRuntime("display", "18");
        byte[] before = File.ReadAllBytes(BasicPath);
        using (var readOnly = File.Open(BasicPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            await Assert.ThrowsAsync<FileAccessException>(() => WallpaperMetadataMigrator.MigrateAsync(_root, RejectLegacy));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(BasicPath));
            Assert.AreEqual("19", WpRuntimeData.Load(runtimePath).AppInfo.FileVersion);
        }
        var result = await WallpaperMetadataMigrator.MigrateAsync(_root, RejectLegacy);
        Assert.AreEqual("19", result.AppInfo.FileVersion);
        Assert.AreEqual("19", WpBasicData.Load(BasicPath).AppInfo.FileVersion);
    }

    [TestMethod]
    public async Task Cancellation_DoesNotWriteOrRebuild() {
        WriteBasic("18");
        byte[] before = File.ReadAllBytes(BasicPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => WallpaperMetadataMigrator.MigrateAsync(_root, RejectLegacy, cancellation.Token));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(BasicPath));
    }

    private void WriteBasic(string version) => JsonSaver.Save(BasicPath, new WpBasicData {
        WallpaperUid = "existing-wallpaper", FolderPath = _root, AppInfo = new() { FileVersion = version }
    }, WpBasicDataContext.Default);

    private string WriteRuntime(string monitor, string version) {
        string path = Path.Combine(_root, monitor, RuntimeType.RImage.ToString(), Constants.Field.WpRuntimeDataFileName);
        JsonSaver.Save(path, new WpRuntimeData {
            FolderPath = _root, MonitorContent = monitor, RType = RuntimeType.RImage, AppInfo = new() { FileVersion = version }
        }, WpRuntimeDataContext.Default);
        return path;
    }

    private static IWpBasicData RejectLegacy(WpBasicData _) => throw new AssertFailedException("Legacy rebuild must not run for this version.");
}
