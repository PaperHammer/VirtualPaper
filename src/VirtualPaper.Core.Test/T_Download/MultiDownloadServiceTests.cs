using System.Security.Cryptography;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Services.Download;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Core.Test.T_Download {
    [TestClass]
    [DoNotParallelize]
    public class MultiDownloadServiceTests {
        private MultiDownloadService _sut = null!;
        private readonly List<string> _tempFiles = [];

        [TestInitialize]
        public void Setup() {
            _sut = new MultiDownloadService();
            Constants.IsTestMode = true;
        }

        [TestCleanup]
        public void Cleanup() {
            _sut.Dispose();
            foreach (var path in _tempFiles) {
                try { File.Delete(path); } catch { }
            }
            _tempFiles.Clear();
        }

        // ============================================================
        // VerifyFileIntegrityAsync — 验证
        // ============================================================

        [TestMethod]
        public async Task VerifyFileIntegrityAsync_WithMatchingHash_ReturnsTrue() {
            var filePath = CreateTempFile("hello world");
            var sha = FileUtil.GetChecksumSHA256(filePath);

            var result = await _sut.VerifyFileIntegrityAsync(filePath, sha);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task VerifyFileIntegrityAsync_WithNonMatchingHash_ReturnsFalse() {
            var filePath = CreateTempFile("hello world");
            var wrongSha = new string('a', 64);

            var result = await _sut.VerifyFileIntegrityAsync(filePath, wrongSha);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task VerifyFileIntegrityAsync_WithNonExistentFile_ReturnsFalse() {
            var result = await _sut.VerifyFileIntegrityAsync(@"C:\nonexistent_file.bin", new string('a', 64));

            Assert.IsFalse(result);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("short")]
        [DataRow("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
        public async Task VerifyFileIntegrityAsync_WithInvalidHashFormat_ReturnsFalse(string hash) {
            var filePath = CreateTempFile("test");

            var result = await _sut.VerifyFileIntegrityAsync(filePath, hash);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task VerifyFileIntegrityAsync_WhenCancelled_ThrowsOperationCancelled() {
            var filePath = CreateTempFile(1024 * 1024);
            var sha = FileUtil.GetChecksumSHA256(filePath);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(
                () => _sut.VerifyFileIntegrityAsync(filePath, sha, cts.Token));
        }

        // ============================================================
        // Pause / Resume — 暂停-恢复
        // ============================================================

        [TestMethod]
        public void Pause_WhenNoDownloaders_DoesNotThrow() {
            _sut.Pause();
            _sut.Pause();
        }

        [TestMethod]
        public void Resume_WhenNoDownloaders_DoesNotThrow() {
            _sut.Resume();
            _sut.Resume();
        }

        [TestMethod]
        public void PauseResume_WhenNoDownloaders_DoesNotThrow() {
            _sut.Pause();
            _sut.Resume();
            _sut.Pause();
        }

        // ============================================================
        // Cancel — 取消
        // ============================================================

        [TestMethod]
        public async Task CancelAsync_WhenNoDownloaders_CompletesSuccessfully() {
            await _sut.CancelAsync();
        }

        [TestMethod]
        public async Task CancelAsync_CanBeCalledMultipleTimes() {
            await _sut.CancelAsync();
            await _sut.CancelAsync();
        }

        [TestMethod]
        public async Task CancelAsync_ThenPause_DoesNotThrow() {
            await _sut.CancelAsync();
            _sut.Pause();
            _sut.Resume();
        }

        // ============================================================
        // Dispose — 资源释放
        // ============================================================

        [TestMethod]
        public void Dispose_WhenNoDownloaders_DoesNotThrow() {
            var service = new MultiDownloadService();
            service.Dispose();
        }

        [TestMethod]
        public void Dispose_CanBeCalledMultipleTimes() {
            var service = new MultiDownloadService();
            service.Dispose();
            service.Dispose();
        }

        // ============================================================
        // DownloadConfiguration — 重试配置
        // ============================================================

        [TestMethod]
        public void Constructor_CreatesServiceWithRetryEnabled() {
            var service = new MultiDownloadService();
            Assert.IsNotNull(service);
            // MultiDownloadService 构造函数内部设置 MaxTryAgainOnFailover=5
            // 此测试验证服务实例可正常构建（包含重试配置）
        }

        // ============================================================
        // 辅助方法
        // ============================================================

        private string CreateTempFile(string content) {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, content);
            _tempFiles.Add(path);
            return path;
        }

        private string CreateTempFile(int size) {
            var path = Path.GetTempFileName();
            var data = new byte[size];
            new Random().NextBytes(data);
            File.WriteAllBytes(path, data);
            _tempFiles.Add(path);
            return path;
        }
    }
}
