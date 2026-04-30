using Moq;
using VirtualPaper.Common.Events;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Core.Test.T_AppUpdate {
    [TestClass]
    public class GithubUpdaterServiceTests {
        private Mock<IGithubReleaseClient> _mockClient = null!;
        private Mock<IVersionComparer> _mockComparer = null!;
        private GithubUpdaterService _service = null!;

        private static readonly Uri FakeUri = new("https://fake/setup.exe");
        private static readonly Uri FakeShaUri = new("https://fake/SHA256.txt");
        private static readonly Version FakeVersion = new(1, 2, 3, 4);
        private const string FakeChangelog = "- bug fix";

        [TestInitialize]
        public void TestInitialize() {
            _mockClient = new Mock<IGithubReleaseClient>();
            _mockComparer = new Mock<IVersionComparer>();

            _mockClient
                .Setup(c => c.GetLatestRelease(It.IsAny<bool>()))
                .ReturnsAsync((FakeUri, FakeShaUri, FakeVersion, FakeChangelog));

            _service = new GithubUpdaterService(_mockClient.Object, _mockComparer.Object);
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

            Assert.IsTrue(_service.LastCheckTime >= before);
        }

        [TestMethod]
        public async Task CheckUpdate_WhenExceptionThrown_ShouldStillUpdateLastCheckTime() {
            _mockClient
                .Setup(c => c.GetLatestRelease(It.IsAny<bool>()))
                .ThrowsAsync(new Exception());
            var before = DateTime.Now;

            await _service.CheckUpdate(fetchDelay: 0);

            Assert.IsTrue(_service.LastCheckTime >= before);
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
            Assert.AreEqual(FakeVersion, received.UpdateVersion);
            Assert.AreEqual(FakeUri, received.UpdateUri);
            Assert.AreEqual(FakeShaUri, received.UpdateSHAUri);
            Assert.AreEqual(FakeChangelog, received.ChangeLog);
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
