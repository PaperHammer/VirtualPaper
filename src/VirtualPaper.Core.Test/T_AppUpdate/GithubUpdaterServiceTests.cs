using Moq;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Models.Events;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Core.Test.T_AppUpdate {
    [TestClass]
    public class GithubUpdaterServiceTests {
        private Mock<IGithubReleaseClient> _mockClient = null!;
        private Mock<IVersionComparer> _mockComparer = null!;
        private Mock<IAppBuildService> _mockBuildService = null!;
        private GithubUpdaterService _service = null!;

        private static readonly Uri FakeUri = new("https://fake/setup.exe");
        private static readonly Uri FakeShaUri = new("https://fake/SHA256.txt");
        private static readonly Version FakeVersion = new(0, 0, 0, 0);
        private const string FakeChangelog = "- bug fix";

        private static ReleaseInfo CreateFakeReleaseInfo() => new() {
            InstallerUri = FakeUri,
            InstallerShaUri = FakeShaUri,
            Version = FakeVersion,
            Changelog = FakeChangelog
        };

        [TestInitialize]
        public void TestInitialize() {
            _mockClient = new Mock<IGithubReleaseClient>();
            _mockComparer = new Mock<IVersionComparer>();
            _mockBuildService = new Mock<IAppBuildService>();

            _mockClient
                .Setup(c => c.GetLatestRelease(It.IsAny<bool>()))
                .ReturnsAsync(CreateFakeReleaseInfo());

            _service = new GithubUpdaterService(_mockClient.Object, _mockComparer.Object, _mockBuildService.Object);
        }

        // -------------------------------------------------------
        // CheckUpdate - 版本比较分支
        // -------------------------------------------------------

        [TestMethod]
        public async Task CheckUpdate_WhenNewerVersion_ShouldReturnAvailable() {
            _mockComparer.Setup(c => c.CompareAssemblyVersion(FakeVersion)).Returns(1);

            var result = await _service.CheckUpdate(fetchDelay: 0);

            Assert.AreEqual(AppUpdateStatus.Available, result);
            Assert.AreEqual(AppUpdateStatus.Available, _service.Status);
        }

        [TestMethod]
        public async Task CheckUpdate_WhenSameVersion_ShouldReturnUptodate() {
            _mockComparer.Setup(c => c.CompareAssemblyVersion(FakeVersion)).Returns(0);

            var result = await _service.CheckUpdate(fetchDelay: 0);

            Assert.AreEqual(AppUpdateStatus.Uptodate, result);
            Assert.AreEqual(AppUpdateStatus.Uptodate, _service.Status);
        }

        [TestMethod]
        public async Task CheckUpdate_WhenOlderVersion_ShouldReturnInvalid() {
            _mockComparer.Setup(c => c.CompareAssemblyVersion(FakeVersion)).Returns(-1);

            var result = await _service.CheckUpdate(fetchDelay: 0);

            Assert.AreEqual(AppUpdateStatus.Invalid, result);
            Assert.AreEqual(AppUpdateStatus.Invalid, _service.Status);
        }

        [TestMethod]
        public async Task CheckUpdate_WhenExceptionThrown_ShouldReturnError() {
            _mockClient
                .Setup(c => c.GetLatestRelease(It.IsAny<bool>()))
                .ThrowsAsync(new HttpRequestException("network error"));

            var result = await _service.CheckUpdate(fetchDelay: 0);

            Assert.AreEqual(AppUpdateStatus.Error, result);
            Assert.AreEqual(AppUpdateStatus.Error, _service.Status);
        }

        // -------------------------------------------------------
        // CheckUpdate - LastCheckTime 更新
        // -------------------------------------------------------

        [TestMethod]
        public async Task CheckUpdate_AfterSuccess_ShouldUpdateLastCheckTime() {
            _mockComparer.Setup(c => c.CompareAssemblyVersion(FakeVersion)).Returns(0);
            var before = DateTime.Now;

            await _service.CheckUpdate(fetchDelay: 0);

            Assert.IsTrue(_service.LastReleaseInfo?.CheckedTime >= before);
        }

        [TestMethod]
        public async Task CheckUpdate_WhenExceptionThrown_ShouldStillUpdateLastCheckTime() {
            _mockClient
                .Setup(c => c.GetLatestRelease(It.IsAny<bool>()))
                .ThrowsAsync(new Exception());
            var before = DateTime.Now;

            await _service.CheckUpdate(fetchDelay: 0);

            Assert.IsTrue(_service.LastReleaseInfo?.CheckedTime >= before);
        }

        // -------------------------------------------------------
        // CheckUpdate - UpdateChecked 事件触发
        // -------------------------------------------------------

        [TestMethod]
        public async Task CheckUpdate_AfterSuccess_ShouldFireUpdateCheckedEvent() {
            _mockComparer.Setup(c => c.CompareAssemblyVersion(FakeVersion)).Returns(1);
            AppUpdaterEventArgs? received = null;
            _service.UpdateChecked += (_, args) => received = args;

            await _service.CheckUpdate(fetchDelay: 0);

            Assert.IsNotNull(received);
            Assert.AreEqual(AppUpdateStatus.Available, received.UpdateStatus);
            Assert.AreEqual(FakeVersion, received.Release?.Version);
            Assert.AreEqual(FakeUri, received.Release?.InstallerUri);
            Assert.AreEqual(FakeShaUri, received.Release?.InstallerShaUri);
            Assert.AreEqual(FakeChangelog, received.Release?.Changelog);
        }

        [TestMethod]
        public async Task CheckUpdate_WhenExceptionThrown_ShouldStillFireUpdateCheckedEvent() {
            _mockClient
                .Setup(c => c.GetLatestRelease(It.IsAny<bool>()))
                .ThrowsAsync(new Exception());
            AppUpdaterEventArgs? received = null;
            _service.UpdateChecked += (_, args) => received = args;

            await _service.CheckUpdate(fetchDelay: 0);

            Assert.IsNotNull(received);
            Assert.AreEqual(AppUpdateStatus.Error, received.UpdateStatus);
        }

        // -------------------------------------------------------
        // Start / Stop
        // -------------------------------------------------------

        [TestMethod]
        public void Stop_WhenNotStarted_ShouldNotThrow() {
            _service.Stop();
        }

        [TestMethod]
        public void Start_ThenStop_ShouldNotThrow() {
            _service.Start();
            _service.Stop();
        }
    }
}
