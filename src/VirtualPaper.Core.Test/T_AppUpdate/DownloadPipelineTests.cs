using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Cores.AppUpdate.Models;
using VirtualPaper.Cores.AppUpdate.Specific;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Core.Test.T_AppUpdate {
    [TestClass]
    [DoNotParallelize]
    public class DownloadPipelineTests {
        private FakeDownloadService _fakeDownload = null!;
        private Mock<IJobService> _mockJob = null!;
        private Mock<IAppBuildService> _mockAppBuild = null!;

        private static readonly byte[] TestContent = "VirtualPaper_test_payload"u8.ToArray();
        private static readonly string ValidSha256 = Convert.ToHexString(SHA256.HashData(TestContent)).ToLowerInvariant();

        [TestInitialize]
        public void Setup() {
            Constants.IsTestMode = true;
            _fakeDownload = new FakeDownloadService(TestContent);
            _mockJob = new Mock<IJobService>();
            _mockAppBuild = new Mock<IAppBuildService>();
            UpdateLock.RegisterAll();
        }

        [TestCleanup]
        public void Cleanup() {
            _fakeDownload.Dispose();
            UpdateLock.ReleaseAll();
            try { Directory.Delete(Constants.CommonPaths.AppDataDir, true); } catch { }
        }

        // ============================================================
        // 下载 — UpdateServiceBase.DownloadFileAsync
        // ============================================================

        [TestMethod]
        public async Task DownloadFileAsync_Success_ReturnsFilePath() {
            var progressValues = new List<DownloadProgress>();
            var progress = new Progress<DownloadProgress>(p => progressValues.Add(p));
            var service = CreateInstallerService();

            var (success, filePath, error) = await service.DownloadFilePublicAsync(progress, CancellationToken.None);

            Assert.IsTrue(success);
            Assert.IsNotNull(filePath);
            Assert.IsTrue(File.Exists(filePath));
            Assert.IsNull(error);
            Assert.IsTrue(progressValues.Count > 0, "应报告进度");
            Assert.IsTrue(progressValues.Exists(p => p.Percent >= 100), "应包含完成进度");
        }

        [TestMethod]
        public async Task DownloadFileAsync_ThrowsExceptionDuringDownload_ReturnsError() {
            _fakeDownload.ShouldThrow = new HttpRequestException("Simulated network failure");
            var service = CreateInstallerService();

            var (success, _, error) = await service.DownloadFilePublicAsync(null, CancellationToken.None);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
            StringAssert.Contains(error, "Simulated network failure");
        }

        [TestMethod]
        public async Task DownloadFileAsync_CancelledBeforeDownload_ThrowsOperationCancelled() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var service = CreateInstallerService();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => service.DownloadFilePublicAsync(null, cts.Token));
        }

        [TestMethod]
        public async Task DownloadFileAsync_CreatesTargetDirectory() {
            var service = CreateInstallerService();
            var targetDir = Path.Combine(Constants.CommonPaths.PendingInstallerUpdateDir, "nested");

            var (success, _, _) = await service.DownloadFilePublicToDirAsync(targetDir, CancellationToken.None);

            Assert.IsTrue(success);
            Assert.IsTrue(Directory.Exists(targetDir));
        }

        // ============================================================
        // 验证 — InstallerUpdateService.VerifyUpdateAsync
        // ============================================================

        [TestMethod]
        public async Task VerifyUpdateAsync_Success_ShaMatches() {
            var service = CreateInstallerService();
            // Download first, then verify
            await service.DownloadUpdatePublicAsync();

            var result = await service.VerifyUpdatePublicAsync();

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task VerifyUpdateAsync_ShaMismatch_ReturnsFalse() {
            var service = CreateInstallerService();
            await service.DownloadUpdatePublicAsync();
            _fakeDownload.Sha256Content = new string('f', 64);
            _fakeDownload.OverrideVerifyResult = false;

            var result = await service.VerifyUpdatePublicAsync();

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task VerifyUpdateAsync_NoInstallerPath_ReturnsFalse() {
            var service = CreateInstallerService();

            var result = await service.VerifyUpdatePublicAsync();

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task VerifyUpdateAsync_ShaDownloadFails_ReturnsFalse() {
            var service = CreateInstallerService();
            await service.DownloadUpdatePublicAsync();
            _fakeDownload.ShaDownloadError = new HttpRequestException("SHA download failed");

            var result = await service.VerifyUpdatePublicAsync();

            Assert.IsFalse(result);
        }

        // ============================================================
        // Pause / Resume / Cancel 传播
        // ============================================================

        [TestMethod]
        public void Pause_Resume_Cancel_PropagateToDownloadService() {
            var service = CreateInstallerService();

            _fakeDownload.Pause();
            _fakeDownload.Resume();
            _fakeDownload.CancelAsync();

            Assert.IsTrue(_fakeDownload.PauseCalled);
            Assert.IsTrue(_fakeDownload.ResumeCalled);
            Assert.IsTrue(_fakeDownload.CancelCalled);
        }

        // ============================================================
        // 自动触发 — PluginsUpdateService.CheckAndRecoverAsync
        // ============================================================

        [TestMethod]
        public async Task CheckAndRecover_NoPendingDir_ReturnsFalse() {
            var service = CreatePluginsService();

            var result = await service.CheckAndRecoverAsync(CancellationToken.None);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task CheckAndRecover_PendingStatus_ReturnsTrue() {
            var service = CreatePluginsService();
            CreatePluginsUpdateFlag(UpdateStatus.Pending);

            var result = await service.CheckAndRecoverAsync(CancellationToken.None);

            Assert.IsTrue(result);
            Assert.IsTrue(File.Exists(Constants.CommonPaths.UpdateFlagPath));
        }

        [TestMethod]
        public async Task CheckAndRecover_InProgressStatus_TriggersRollbackAndReturnFalse() {
            var service = CreatePluginsService();
            CreatePluginsUpdateFlag(UpdateStatus.InProgress);
            CreateBackupDirWithContent();

            var result = await service.CheckAndRecoverAsync(CancellationToken.None);

            Assert.IsFalse(result);
            Assert.IsFalse(File.Exists(Constants.CommonPaths.UpdateFlagPath));
        }

        [TestMethod]
        public async Task CheckAndRecover_CompletedStatus_CleanupAndReturnFalse() {
            var service = CreatePluginsService();
            CreatePluginsUpdateFlag(UpdateStatus.Completed);

            var result = await service.CheckAndRecoverAsync(CancellationToken.None);

            Assert.IsFalse(result);
            Assert.IsFalse(Directory.Exists(Constants.CommonPaths.PendingPluginsUpdateDir));
        }

        [TestMethod]
        public async Task CheckAndRecover_NoFlagButPendingDir_CleanupAndReturnFalse() {
            var service = CreatePluginsService();
            Directory.CreateDirectory(Constants.CommonPaths.PendingPluginsUpdateDir);

            var result = await service.CheckAndRecoverAsync(CancellationToken.None);

            Assert.IsFalse(result);
            Assert.IsFalse(Directory.Exists(Constants.CommonPaths.PendingPluginsUpdateDir));
        }

        // ============================================================
        // 回滚 — InProgress → Rollback 恢复备份
        // ============================================================

        [TestMethod]
        public async Task Rollback_WithBackup_RollbackCleansUp() {
            var service = CreatePluginsService();
            CreatePluginsUpdateFlag(UpdateStatus.InProgress);
            CreateBackupDirWithContent();

            await service.CheckAndRecoverAsync(CancellationToken.None);

            Assert.IsFalse(Directory.Exists(Constants.CommonPaths.PendingPluginsUpdateDir));
            Assert.IsFalse(Directory.Exists(Constants.CommonPaths.UpdateBackupDir));
        }

        [TestMethod]
        public async Task Rollback_NoBackup_CleansUpPendingDir() {
            var service = CreatePluginsService();
            CreatePluginsUpdateFlag(UpdateStatus.InProgress);

            await service.CheckAndRecoverAsync(CancellationToken.None);

            Assert.IsFalse(Directory.Exists(Constants.CommonPaths.PendingPluginsUpdateDir));
        }

        // ============================================================
        // 插件验证链 — 下载→SHA验证→解压→交叉校验
        // ============================================================

        [TestMethod]
        public async Task VerifyPluginUpdateAsync_Success_FullChain() {
            var service = CreatePluginsService();
            SetupPluginDownloadVerifySuccess(ValidSha256);

            var info = new ReleaseInfo { PluginPatchSha256Uri = new Uri("https://fake/sha.txt") };

            var result = await service.VerifyUpdateAsync(info, CancellationToken.None);

            Assert.IsTrue(result);
            Assert.IsTrue(File.Exists(Constants.CommonPaths.UpdateFlagPath));
        }

        [TestMethod]
        public async Task VerifyPluginUpdateAsync_CrossVerifyFailure_ReturnsFalse() {
            _fakeDownload.OverrideVerifyResult = false;
            var service = CreatePluginsService();
            var pendingDir = Constants.CommonPaths.PendingPluginsUpdateDir;
            Directory.CreateDirectory(pendingDir);
            var patchZip = Path.Combine(pendingDir, "plugins_patch.zip");
            File.WriteAllBytes(patchZip, TestContent);

            var info = new ReleaseInfo { PluginPatchSha256Uri = new Uri("https://fake/sha.txt") };

            var result = await service.VerifyUpdateAsync(info, CancellationToken.None);

            Assert.IsFalse(result);
        }

        // ============================================================
        // 辅助方法
        // ============================================================

        private TestInstallerUpdateService CreateInstallerService() =>
            new(_fakeDownload);

        private PluginsUpdateService CreatePluginsService() =>
            new(_fakeDownload, _mockJob.Object, _mockAppBuild.Object, null!);

        private void SetupPluginDownloadVerifySuccess(string sha) {
            _fakeDownload.Sha256Content = sha;
            _fakeDownload.OverrideVerifyResult = true;

            var pendingDir = Constants.CommonPaths.PendingPluginsUpdateDir;
            var extractDir = Constants.CommonPaths.PluginPatchExtractDir;

            Directory.CreateDirectory(pendingDir);
            var patchZipPath = Path.Combine(pendingDir, "plugins_patch.zip");

            using (var ms = new MemoryStream()) {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true)) {
                    var manifestJson = JsonSerializer.Serialize(new PendingUpdateManifest {
                        Plugins = new Dictionary<string, PendingPluginInfo> {
                            ["TestPlugin"] = new() { BuildNumber = "100", Asset = "plugin_test.zip", Sha256 = sha }
                        }
                    }, UpdateManifestContext.Default.PendingUpdateManifest);
                    WriteZipEntry(archive, "pending_update_plugins_manifest.json", manifestJson);

                    var appCompJson = JsonSerializer.Serialize(new AppCompManifest {
                        AppBuildNumber = "2024.1",
                        Plugins = new Dictionary<string, string> { ["TestPlugin"] = "100" }
                    }, UpdateManifestContext.Default.AppCompManifest);
                    WriteZipEntry(archive, "app_comp_manifest.json", appCompJson);

                    using var pms = new MemoryStream();
                    using (var pArchive = new ZipArchive(pms, ZipArchiveMode.Create, true)) {
                        WriteZipEntry(pArchive, "TestPlugin/dummy.dll", "test_content");
                    }
                    pms.Seek(0, SeekOrigin.Begin);
                    WriteZipEntry(archive, "plugin_test.zip", pms.ToArray());
                }
                ms.Seek(0, SeekOrigin.Begin);
                File.WriteAllBytes(patchZipPath, ms.ToArray());
            }

            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(patchZipPath, extractDir, true);
        }

        private static void WriteZipEntry(ZipArchive archive, string entryName, string content) {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
            using var sw = new StreamWriter(entry.Open());
            sw.Write(content);
        }

        private static void WriteZipEntry(ZipArchive archive, string entryName, byte[] content) {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
            using var s = entry.Open();
            s.Write(content);
        }

        private static void CreatePluginsUpdateFlag(UpdateStatus status) {
            var pendingDir = Constants.CommonPaths.PendingPluginsUpdateDir;
            Directory.CreateDirectory(pendingDir);
            var flag = new PluginsUpdateFlag {
                Status = status,
                AppBuildNumber = "1.0.0",
                Plugins = new Dictionary<string, PluginFlagInfo> {
                    ["TestPlugin"] = new() {
                        Target = Path.Combine("Plugins", "TestPlugin"),
                        Build = "100",
                        Files = new List<FileHashInfo> { new() { Name = "plugin_test.zip", Sha256 = ValidSha256 } }
                    }
                }
            };
            var json = JsonSerializer.Serialize(flag, UpdateFlagContext.Default.PluginsUpdateFlag);
            File.WriteAllText(Constants.CommonPaths.UpdateFlagPath, json);
        }

        private static string CreateBackupDirWithContent() {
            var backupDir = Constants.CommonPaths.UpdateBackupDir;
            Directory.CreateDirectory(backupDir);
            var pluginBackupDir = Path.Combine(backupDir, "TestPlugin");
            Directory.CreateDirectory(pluginBackupDir);
            File.WriteAllText(Path.Combine(pluginBackupDir, "original.dll"), "backup_content");
            return pluginBackupDir;
        }
    }

    // ============================================================
    // Fake IDownloadService — 替代 Moq 支持 IAsyncEnumerable
    // ============================================================

    internal class FakeDownloadService : IDownloadService, IDisposable {
        private readonly byte[] _defaultContent;

        public Exception? ShouldThrow { get; set; }
        public string? Sha256Content { get; set; }
        public bool? OverrideVerifyResult { get; set; }
        public Exception? ShaDownloadError { get; set; }
        public bool PauseCalled { get; private set; }
        public bool ResumeCalled { get; private set; }
        public bool CancelCalled { get; private set; }

        public FakeDownloadService(byte[] defaultContent) {
            _defaultContent = defaultContent;
            Sha256Content = Convert.ToHexString(SHA256.HashData(defaultContent)).ToLowerInvariant();
        }

        public async IAsyncEnumerable<DownloadProgress> DownloadAsync(
            Uri uri, string saveFilePath,
            [EnumeratorCancellation] CancellationToken token) {
            token.ThrowIfCancellationRequested();

            if (ShouldThrow != null)
                throw ShouldThrow;

            var total = (long)_defaultContent.Length;
            var progressItems = new List<DownloadProgress> {
                new(0, 0, TimeSpan.Zero, 0, total),
                new(50, 1, TimeSpan.FromSeconds(0.5), total / 2, total),
                new(100, 1, TimeSpan.Zero, total, total),
            };

            foreach (var p in progressItems) {
                token.ThrowIfCancellationRequested();
                yield return p;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(saveFilePath)!);
            await File.WriteAllBytesAsync(saveFilePath, _defaultContent, token);
        }

        public async IAsyncEnumerable<DownloadProgress> DownloadMultipleAsync(
            IEnumerable<(Uri uri, string saveFilePath)> downloads,
            [EnumeratorCancellation] CancellationToken token) {
            foreach (var (uri, path) in downloads) {
                token.ThrowIfCancellationRequested();
                await foreach (var p in DownloadAsync(uri, path, token))
                    yield return p;
            }
        }

        public async Task<string> DownloadShaTxtAsync(Uri shaUri, CancellationToken token) {
            if (ShaDownloadError != null) throw ShaDownloadError;
            return Sha256Content!;
        }

        public async Task<bool> VerifyFileIntegrityAsync(
            string filePath, string expectedSha256, CancellationToken token = default) {
            if (OverrideVerifyResult.HasValue)
                return OverrideVerifyResult.Value;
            if (!File.Exists(filePath)) return false;
            var actual = await Task.Run(() => FileUtil.GetChecksumSHA256(filePath), token);
            return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
        }

        public void Pause() => PauseCalled = true;
        public void Resume() => ResumeCalled = true;
        public Task CancelAsync() { CancelCalled = true; return Task.CompletedTask; }
        public void Dispose() { }
    }

    // ============================================================
    // Test subclasses
    // ============================================================

    internal class TestInstallerUpdateService(IDownloadService ds) : InstallerUpdateService(ds) {
        public Task<(bool Success, string? FilePath, string? Error)> DownloadFilePublicAsync(
            IProgress<DownloadProgress>? progress, CancellationToken token) {
            return DownloadFileAsync(
                new Uri("https://fake/test.exe"),
                Constants.CommonPaths.PendingInstallerUpdateDir,
                "setup.exe",
                progress,
                token);
        }

        public async Task<(bool Success, string? FilePath, string? Error)> DownloadFilePublicToDirAsync(
            string targetDir, CancellationToken token) {
            return await DownloadFileAsync(
                new Uri("https://fake/test.exe"), targetDir, "setup.exe", null, token);
        }

        public async Task<bool> DownloadUpdatePublicAsync(CancellationToken token = default) {
            var info = new ReleaseInfo {
                InstallerUri = new Uri("https://fake/setup.exe"),
            };
            return await DownloadUpdateAsync(info, null, token);
        }

        public async Task<bool> VerifyUpdatePublicAsync() {
            var info = new ReleaseInfo {
                InstallerShaUri = new Uri("https://fake/SHA256.txt"),
                InstallerUri = new Uri("https://fake/setup.exe"),
            };
            return await VerifyUpdateAsync(info, CancellationToken.None);
        }
    }
}
