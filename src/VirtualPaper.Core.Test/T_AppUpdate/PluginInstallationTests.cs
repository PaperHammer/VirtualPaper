using System.IO.Compression;
using System.Text.Json;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Cores.AppUpdate.Specific;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Services.Download;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Core.Test.T_AppUpdate;

[TestClass]
[DoNotParallelize]
public class PluginInstallationTests {
    private string _root = null!, _application = null!, _oldRoot = null!;
    private bool _oldTestMode;
    private MultiDownloadService _download = null!;
    private Mock<IJobService> _jobs = null!;
    private Mock<IAppBuildService> _build = null!;
    private PluginsUpdateService _service = null!;
    private const string OldManifest = """{"AppBuildNumber":"old"}""";
    private const string NewManifest = """{"AppBuildNumber":"new"}""";

    [TestInitialize]
    public void Initialize() {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.PluginTests-" + Guid.NewGuid().ToString("N"))).FullName;
        _application = Directory.CreateDirectory(Path.Combine(_root, "application")).FullName;
        _oldRoot = Constants.CommonPaths.TestRootDir;
        _oldTestMode = Constants.IsTestMode;
        Constants.IsTestMode = true;
        Constants.CommonPaths.TestRootDir = Path.Combine(_root, "data");
        Assert.IsTrue(FileUtil.IsPathWithinDirectory(_root, Constants.CommonPaths.AppDataDir), "Plugin tests require an isolated profile.");
        Directory.CreateDirectory(Constants.CommonPaths.AppDataDir);
        _download = new MultiDownloadService();
        _jobs = new Mock<IJobService>();
        _build = new Mock<IAppBuildService>();
        _service = new PluginsUpdateService(_download, _jobs.Object, _build.Object, Mock.Of<IWindowService>(), _application);
        UpdateLock.RegisterAll();
        UpdateLock.ReleaseAll();
    }

    [TestCleanup]
    public void Cleanup() {
        _download.Dispose();
        UpdateLock.ReleaseAll();
        Constants.CommonPaths.TestRootDir = _oldRoot;
        Constants.IsTestMode = _oldTestMode;
        Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task SuccessfulUpdate_ReplacesBytesAndManifest_RemovesObsoletePlugin_AndReleasesLocks() {
        Prepare(removeObsolete: true);
        var stages = new List<PluginsUpdateProgress>();
        var result = await _service.ExecutePendingUpdateAsync(new InlineProgress<PluginsUpdateProgress>(stages.Add));
        Assert.IsTrue(result.Success, result.ErrorMessage);
        AssertUpdated();
        Assert.IsFalse(Directory.Exists(Path.Combine(_application, "Plugins", "Obsolete")));
        Assert.IsFalse(Directory.Exists(Constants.CommonPaths.PendingPluginsUpdateDir));
        Assert.IsTrue(stages.Any(stage => stage.Stage == PluginsUpdateStage.Completed && stage.Percent == 100));
        _jobs.Verify(j => j.StopPlugin(PluginName.UI), Times.Once);
        _jobs.Verify(j => j.StopPlugin(PluginName.PlayerWeb), Times.Once);
        _jobs.Verify(j => j.PluginsUpdateFinished(), Times.Once);
        _build.Verify(b => b.Refresh(), Times.Once);
        Assert.IsFalse(UpdateLock.IsAnyLocked);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("hash")]
    public async Task VerificationFailure_DoesNotModifyPluginsOrManifest(string failure) {
        Prepare();
        var original = Snapshot();
        string zip = Path.Combine(Constants.CommonPaths.PluginPatchExtractDir, "PlayerWeb.zip");
        if (failure == "missing") File.Delete(zip);
        else File.AppendAllText(zip, "modified after verification");
        var result = await _service.ExecutePendingUpdateAsync();
        Assert.IsFalse(result.Success);
        AssertOriginal(original);
        _jobs.Verify(j => j.StopPlugin(It.IsAny<PluginName>()), Times.Never);
        _jobs.Verify(j => j.PluginsUpdateFinished(), Times.Once);
        Assert.IsFalse(UpdateLock.IsAnyLocked);
    }

    [TestMethod]
    public async Task SecondPluginFails_RestoresFirstPluginAndNestedFiles_WithoutLeavingNewPayloads() {
        Prepare(invalidSecondZip: true);
        var original = Snapshot();
        bool firstReplaced = false;
        var result = await _service.ExecutePendingUpdateAsync(new InlineProgress<PluginsUpdateProgress>(p => {
            if (p.Stage == PluginsUpdateStage.Replacing && p.Percent > 0) {
                Assert.AreEqual("new-UI", File.ReadAllText(Target("UI", "main.bin")));
                firstReplaced = true;
            }
        }));
        Assert.IsTrue(firstReplaced, "The test must fail after a real first replacement.");
        Assert.IsFalse(result.Success);
        AssertOriginal(original);
        Assert.IsFalse(Directory.Exists(Constants.CommonPaths.PendingPluginsUpdateDir));
        Assert.IsTrue(File.Exists(Constants.CommonPaths.UpdateFailedNoticePath));
        Assert.IsFalse(UpdateLock.IsAnyLocked);
    }

    [TestMethod]
    public async Task BackupFailure_DoesNotRestoreAnIncompleteBackupOverOriginalFiles() {
        Prepare();
        var original = Snapshot();
        // main.bin is copied first; a nested file prevents completing the backup.
        using (var locked = new FileStream(Target("UI", "assets/icon.bin"), FileMode.Open, FileAccess.Read, FileShare.None)) {
            Assert.IsFalse((await _service.ExecutePendingUpdateAsync()).Success);
        }
        AssertOriginal(original);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailureAfterManifestUpdate_RestoresManifestsRemovedPluginsAndNewPluginState(bool newPlayer) {
        Prepare(removeObsolete: true, newPlayer: newPlayer);
        var original = Snapshot();
        _build.SetupSequence(b => b.Refresh()).Throws(new IOException("Failure after writing manifests")).Pass();
        Assert.IsFalse((await _service.ExecutePendingUpdateAsync()).Success);
        AssertOriginal(original);
        Assert.IsFalse(UpdateLock.IsAnyLocked);
    }

    [TestMethod]
    public async Task FailedRollback_KeepsBackupAndFlag_AndCanRecoverAfterTheLockIsReleased() {
        Prepare(invalidSecondZip: true);
        var original = Snapshot();
        FileStream? locked = null;
        try {
            var result = await _service.ExecutePendingUpdateAsync(new InlineProgress<PluginsUpdateProgress>(p => {
                if (p.Stage == PluginsUpdateStage.Replacing && p.Percent > 0 && locked == null)
                    locked = new FileStream(Target("UI", "main.bin"), FileMode.Open, FileAccess.Read, FileShare.Read);
            }));
            Assert.IsFalse(result.Success);
            Assert.AreEqual("old-UI", File.ReadAllText(Path.Combine(Constants.CommonPaths.UpdateBackupDir, "UI", "main.bin")));
            Assert.AreEqual(UpdateStatus.InProgress, JsonSerializer.Deserialize(File.ReadAllText(Constants.CommonPaths.UpdateFlagPath),
                UpdateFlagContext.Default.PluginsUpdateFlag)!.Status);
        }
        finally { locked?.Dispose(); }
        Assert.IsFalse(await _service.CheckAndRecoverAsync());
        AssertOriginal(original);
        Assert.IsFalse(Directory.Exists(Constants.CommonPaths.PendingPluginsUpdateDir));
    }

    [TestMethod]
    public async Task CancelAfterFirstReplacement_RestoresOriginalsAndRecordsFailure() {
        Prepare();
        var original = Snapshot();
        using var cancellation = new CancellationTokenSource();
        var result = await _service.ExecutePendingUpdateAsync(new InlineProgress<PluginsUpdateProgress>(p => {
            if (p.Stage == PluginsUpdateStage.Replacing && p.Percent > 0) cancellation.Cancel();
        }), cancellation.Token);
        Assert.IsFalse(result.Success);
        AssertOriginal(original);
        Assert.IsTrue(File.Exists(Constants.CommonPaths.UpdateFailedNoticePath));
        Assert.IsFalse(UpdateLock.IsAnyLocked);
    }

    [TestMethod]
    public async Task CleanupFailureAfterCommit_DoesNotRollBackTheSuccessfullyInstalledUpdate() {
        Prepare();
        FileStream? locked = null;
        try {
            var result = await _service.ExecutePendingUpdateAsync(new InlineProgress<PluginsUpdateProgress>(p => {
                if (p.Stage == PluginsUpdateStage.Replacing && p.Percent > 0 && locked == null)
                    locked = new FileStream(Path.Combine(Constants.CommonPaths.UpdateBackupDir, "UI", "main.bin"),
                        FileMode.Open, FileAccess.Read, FileShare.Read);
            }));
            Assert.IsTrue(result.Success, result.ErrorMessage);
            AssertUpdated();
        }
        finally { locked?.Dispose(); }
        await _service.CheckAndRecoverAsync();
        AssertUpdated();
        Assert.IsFalse(Directory.Exists(Constants.CommonPaths.PendingPluginsUpdateDir));
    }

    private void Prepare(bool invalidSecondZip = false, bool removeObsolete = false, bool newPlayer = false) {
        var flag = new PluginsUpdateFlag { Status = UpdateStatus.Pending };
        Directory.CreateDirectory(Constants.CommonPaths.PluginPatchExtractDir);
        foreach (string plugin in new[] { "UI", "PlayerWeb" }) {
            if (plugin != "PlayerWeb" || !newPlayer) {
                Write(Target(plugin, "main.bin"), "old-" + plugin);
                Write(Target(plugin, "assets/icon.bin"), "old-icon-" + plugin);
            }
            string zip = Path.Combine(Constants.CommonPaths.PluginPatchExtractDir, plugin + ".zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create)) {
                using (var writer = new StreamWriter(archive.CreateEntry(plugin + "/main.bin").Open())) writer.Write("new-" + plugin);
                using (var writer = new StreamWriter(archive.CreateEntry(plugin + "/assets/new.bin").Open())) writer.Write("new-icon");
                if (plugin == "PlayerWeb" && invalidSecondZip) {
                    using var writer = new StreamWriter(archive.CreateEntry("Unexpected/other.bin").Open()); writer.Write("invalid layout");
                }
            }
            flag.Plugins.Add(plugin, new PluginFlagInfo { Target = Path.Combine("Plugins", plugin),
                Files = [new FileHashInfo { Name = plugin + ".zip", Sha256 = FileUtil.GetChecksumSHA256(zip) }] });
        }
        if (removeObsolete) {
            Write(Target("Obsolete", "original.bin"), "must survive rollback");
            flag.RemovedPlugins.Add("Obsolete");
        }
        Write(Path.Combine(_application, "app_comp_manifest.json"), OldManifest);
        Write(Path.Combine(Constants.CommonPaths.AppDataDir, "app_comp_manifest.json"), OldManifest);
        Write(Path.Combine(Constants.CommonPaths.PluginPatchExtractDir, "app_comp_manifest.json"), NewManifest);
        File.WriteAllText(Constants.CommonPaths.UpdateFlagPath, JsonSerializer.Serialize(flag, UpdateFlagContext.Default.PluginsUpdateFlag));
    }

    private Dictionary<string, string> Snapshot() => Directory.GetFiles(_application, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(_application, path), path => Convert.ToBase64String(File.ReadAllBytes(path)));

    private void AssertOriginal(Dictionary<string, string> original) {
        var actual = Snapshot();
        CollectionAssert.AreEquivalent(original.Keys.ToArray(), actual.Keys.ToArray());
        foreach (var pair in original) Assert.AreEqual(pair.Value, actual[pair.Key], pair.Key);
        Assert.AreEqual(OldManifest, File.ReadAllText(Path.Combine(Constants.CommonPaths.AppDataDir, "app_comp_manifest.json")));
    }

    private void AssertUpdated() {
        foreach (string plugin in new[] { "UI", "PlayerWeb" }) {
            Assert.AreEqual("new-" + plugin, File.ReadAllText(Target(plugin, "main.bin")));
            Assert.AreEqual("new-icon", File.ReadAllText(Target(plugin, "assets/new.bin")));
            Assert.IsFalse(File.Exists(Target(plugin, "assets/icon.bin")));
        }
        Assert.AreEqual(NewManifest, File.ReadAllText(Path.Combine(_application, "app_comp_manifest.json")));
        Assert.AreEqual(NewManifest, File.ReadAllText(Path.Combine(Constants.CommonPaths.AppDataDir, "app_comp_manifest.json")));
    }

    private string Target(string plugin, string file) => Path.Combine(_application, "Plugins", plugin, file);
    private static void Write(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); }
}
