using Moq;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Cores.AppUpdate.Specific;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Models.Events;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace VirtualPaper.Core.Test.T_AppUpdate {
    [TestClass]
    public class AppUpdaterWindowViewModelTests {
        [TestInitialize]
        public void Initialize() {
            _downloadService = new Mock<IDownloadService>();
            _dialogService = new Mock<IContentDialogService>();
            _updateService = new Mock<IInstallerUpdateService>();
            _appUpdaterService = new Mock<IAppUpdaterService>();
            _serviceProvider = new Mock<IServiceProvider>();
            _serviceProvider
                .Setup(provider => provider.GetService(typeof(IInstallerUpdateService)))
                .Returns(_updateService.Object);
            _appUpdaterService
                .Setup(service => service.CheckUpdateAsync(It.IsAny<int>()))
                .ReturnsAsync(AppUpdateStatus.Uptodate);

            _viewModel = new AppUpdaterWindowViewModel(
                _downloadService.Object,
                _dialogService.Object,
                _serviceProvider.Object,
                _appUpdaterService.Object,
                _ => Task.FromResult(ContentDialogResult.None));
        }

        [TestMethod]
        public void ReceiveParameter_InitializesInstallerReleaseAndReadyState() {
            var release = CreateRelease();

            _viewModel.ReceiveParameter(release);

            Assert.AreEqual("1.2.3", _viewModel.Version);
            Assert.AreEqual("changes", _viewModel.ChangeLog);
            Assert.AreEqual(DownloadState.Ready, _viewModel.CurrentState);
            Assert.IsFalse(_viewModel.IsPluginsUpdate);
            Assert.AreEqual(0f, _viewModel.Progress);
        }

        [TestMethod]
        public async Task ActionCommand_WhenDownloadAndVerificationSucceed_CompletesUpdate() {
            _updateService
                .Setup(service => service.DownloadUpdateAsync(
                    It.IsAny<ReleaseInfo>(),
                    It.IsAny<IProgress<DownloadProgress>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _updateService
                .Setup(service => service.VerifyUpdateAsync(
                    It.IsAny<ReleaseInfo>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            int flashCount = 0;
            _viewModel.RequestFlashTaskbar = () => flashCount++;
            _viewModel.ReceiveParameter(CreateRelease());

            await _viewModel.OnActionCommandAsync();

            Assert.AreEqual(DownloadState.Completed, _viewModel.CurrentState);
            Assert.IsFalse(_viewModel.IsIndeterminate);
            Assert.IsTrue(_viewModel.ActionButtonEnable);
            Assert.AreEqual(1, flashCount);
            _updateService.Verify(service => service.DownloadUpdateAsync(
                It.IsAny<ReleaseInfo>(),
                It.IsAny<IProgress<DownloadProgress>>(),
                It.IsAny<CancellationToken>()), Times.Once);
            _updateService.Verify(service => service.VerifyUpdateAsync(
                It.IsAny<ReleaseInfo>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ActionCommand_WhenDownloadReturnsFalse_SetsDownloadFailed() {
            _updateService
                .Setup(service => service.DownloadUpdateAsync(
                    It.IsAny<ReleaseInfo>(),
                    It.IsAny<IProgress<DownloadProgress>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _viewModel.ReceiveParameter(CreateRelease());

            await _viewModel.OnActionCommandAsync();

            Assert.AreEqual(DownloadState.DownloadFailed, _viewModel.CurrentState);
            Assert.IsTrue(_viewModel.IsError);
            _updateService.Verify(service => service.VerifyUpdateAsync(
                It.IsAny<ReleaseInfo>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task ActionCommand_WhenVerificationReturnsFalse_SetsVerifyFailed() {
            _updateService
                .Setup(service => service.DownloadUpdateAsync(
                    It.IsAny<ReleaseInfo>(),
                    It.IsAny<IProgress<DownloadProgress>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _updateService
                .Setup(service => service.VerifyUpdateAsync(
                    It.IsAny<ReleaseInfo>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _viewModel.ReceiveParameter(CreateRelease());

            await _viewModel.OnActionCommandAsync();

            Assert.AreEqual(DownloadState.VerifyFailed, _viewModel.CurrentState);
            Assert.IsTrue(_viewModel.IsError);
            Assert.IsFalse(_viewModel.IsIndeterminate);
        }

        [TestMethod]
        public async Task ActionCommand_WhenDownloadThrows_SetsDownloadFailed() {
            _updateService
                .Setup(service => service.DownloadUpdateAsync(
                    It.IsAny<ReleaseInfo>(),
                    It.IsAny<IProgress<DownloadProgress>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("download failed"));
            _viewModel.ReceiveParameter(CreateRelease());

            await _viewModel.OnActionCommandAsync();

            Assert.AreEqual(DownloadState.DownloadFailed, _viewModel.CurrentState);
            Assert.IsTrue(_viewModel.IsError);
        }

        [TestMethod]
        public async Task ActionCommand_WhenVerificationThrows_SetsVerifyFailed() {
            _updateService
                .Setup(service => service.DownloadUpdateAsync(
                    It.IsAny<ReleaseInfo>(),
                    It.IsAny<IProgress<DownloadProgress>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _updateService
                .Setup(service => service.VerifyUpdateAsync(
                    It.IsAny<ReleaseInfo>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidDataException("verification failed"));
            _viewModel.ReceiveParameter(CreateRelease());

            await _viewModel.OnActionCommandAsync();

            Assert.AreEqual(DownloadState.VerifyFailed, _viewModel.CurrentState);
            Assert.IsTrue(_viewModel.IsError);
            Assert.IsFalse(_viewModel.IsIndeterminate);
        }

        [TestMethod]
        public async Task Cancel_DuringDownloadCancelsOperationAndLeavesPausedState() {
            var enteredDownload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _updateService
                .Setup(service => service.DownloadUpdateAsync(
                    It.IsAny<ReleaseInfo>(),
                    It.IsAny<IProgress<DownloadProgress>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (ReleaseInfo release, IProgress<DownloadProgress> progress, CancellationToken token) => {
                    enteredDownload.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return true;
                });
            _viewModel.ReceiveParameter(CreateRelease());

            var action = _viewModel.OnActionCommandAsync();
            await enteredDownload.Task;
            _viewModel.Cancel();
            await action;

            Assert.AreEqual(DownloadState.Paused, _viewModel.CurrentState);
            Assert.IsTrue(_viewModel.ActionButtonEnable);
            _updateService.Verify(service => service.VerifyUpdateAsync(
                It.IsAny<ReleaseInfo>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task ActionCommand_WithoutReleaseParameterDoesNothing() {
            await _viewModel.OnActionCommandAsync();

            Assert.AreEqual(DownloadState.None, _viewModel.CurrentState);
            _updateService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task ActionCommand_PausesAndResumesDownloadService() {
            _viewModel.CurrentState = DownloadState.Downloading;

            await _viewModel.OnActionCommandAsync();

            Assert.AreEqual(DownloadState.Paused, _viewModel.CurrentState);
            _downloadService.Verify(service => service.Pause(), Times.Once);

            await _viewModel.OnActionCommandAsync();

            Assert.AreEqual(DownloadState.Downloading, _viewModel.CurrentState);
            _downloadService.Verify(service => service.Resume(), Times.Once);
        }

        private static ReleaseInfo CreateRelease() => new() {
            Version = new Version(1, 2, 3),
            Changelog = "changes",
            InstallerUri = new Uri("https://example.invalid/setup.exe"),
            InstallerShaUri = new Uri("https://example.invalid/SHA256.txt"),
        };

        private Mock<IDownloadService> _downloadService = null!;
        private Mock<IContentDialogService> _dialogService = null!;
        private Mock<IInstallerUpdateService> _updateService = null!;
        private Mock<IAppUpdaterService> _appUpdaterService = null!;
        private Mock<IServiceProvider> _serviceProvider = null!;
        private AppUpdaterWindowViewModel _viewModel = null!;
    }
}
