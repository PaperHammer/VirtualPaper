using Moq;
using VirtualPaper.Cores.TrayControl;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Core.Test.T_Tray {
    [TestClass]
    [TestCategory("Backend")]
    public class TrayCommandTests {
        private Mock<IPipeClientFactory> _factory = null!;
        private Mock<IPipeClient> _pipeClient = null!;
        private TrayCommand _sut = null!;

        [TestInitialize]
        public void Setup() {
            _pipeClient = new Mock<IPipeClient>();
            _factory = new Mock<IPipeClientFactory>();

            // 默认：Connect 成功，Writer 返回可写流
            _factory
                .Setup(f => f.Create("localhost", "TRAY_CMD"))
                .Returns(_pipeClient.Object);

            _pipeClient
                .Setup(p => p.ConnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _pipeClient
                .Setup(p => p.CreateWriter())
                .Returns(new StreamWriter(Stream.Null) { AutoFlush = true });

            _sut = new TrayCommand(_factory.Object);
        }

        // ----------------------------------------------------------------
        // 正常路径
        // ----------------------------------------------------------------

        [TestMethod]
        [Description("SendMsgToUIAsync should connect to the named pipe with correct parameters")]
        public async Task SendMsgToUIAsync_ShouldConnectToPipe_WithCorrectName() {
            // Act
            await _sut.SendMsgToUIAsync("hello");

            // Assert
            _factory.Verify(
                f => f.Create("localhost", "TRAY_CMD"),
                Times.Once,
                "Should connect to pipe named TRAY_CMD on localhost");
        }

        [TestMethod]
        [Description("SendMsgToUIAsync should write the message to the pipe writer")]
        public async Task SendMsgToUIAsync_ShouldWriteMessageToPipe() {
            // Arrange
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

            _pipeClient.Setup(p => p.CreateWriter()).Returns(writer);

            // Act
            await _sut.SendMsgToUIAsync("TEST_MSG");

            // Assert
            stream.Position = 0;
            var content = await new StreamReader(stream).ReadToEndAsync();
            Assert.Contains("TEST_MSG", content,
                "Message content should be written to the pipe");
            stream.Dispose();
        }

        [TestMethod]
        [Description("SendMsgToUIAsync should call WaitForPipeDrain after writing")]
        public async Task SendMsgToUIAsync_ShouldCallWaitForPipeDrain() {
            // Act
            await _sut.SendMsgToUIAsync("any");

            // Assert
            _pipeClient.Verify(p => p.WaitForPipeDrain(), Times.Once,
                "Should wait for pipe drain after writing");
        }

        [TestMethod]
        [Description("SendMsgToUIAsync should dispose the pipe client after completion")]
        public async Task SendMsgToUIAsync_ShouldDisposePipeClient() {
            // Act
            await _sut.SendMsgToUIAsync("any");

            // Assert
            _pipeClient.Verify(p => p.Dispose(), Times.Once,
                "Pipe client should be disposed after use");
        }

        // ----------------------------------------------------------------
        // 异常路径
        // ----------------------------------------------------------------

        [TestMethod]
        [Description("SendMsgToUIAsync should not throw when ConnectAsync times out")]
        public async Task SendMsgToUIAsync_WhenConnectTimesOut_ShouldNotThrow() {
            // Arrange
            _pipeClient
                .Setup(p => p.ConnectAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException("Pipe connect timeout"));

            // Act & Assert: async void 下异常会吞掉，Task 版本应安全处理
            await _sut.SendMsgToUIAsync("msg");
            // 不抛出即通过
        }

        [TestMethod]
        [Description("SendMsgToUIAsync should not throw when pipe is broken")]
        public async Task SendMsgToUIAsync_WhenPipeBroken_ShouldNotThrow() {
            // Arrange
            _pipeClient
                .Setup(p => p.ConnectAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Pipe is broken"));

            // Act & Assert
            await _sut.SendMsgToUIAsync("msg");
        }

        [TestMethod]
        [Description("SendMsgToUIAsync should not throw when CancellationToken is cancelled")]
        public async Task SendMsgToUIAsync_WhenCancelled_ShouldNotThrow() {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _pipeClient
                .Setup(p => p.ConnectAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await _sut.SendMsgToUIAsync("msg", cts.Token);
        }

        [TestMethod]
        [Description("SendMsgToUIAsync should not create a writer when ConnectAsync fails")]
        public async Task SendMsgToUIAsync_WhenConnectFails_ShouldNotCallCreateWriter() {
            // Arrange
            _pipeClient
                .Setup(p => p.ConnectAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException());

            // Act
            await _sut.SendMsgToUIAsync("msg");

            // Assert
            _pipeClient.Verify(p => p.CreateWriter(), Times.Never,
                "Writer should not be created if connection failed");
        }

        // ----------------------------------------------------------------
        // 边界输入
        // ----------------------------------------------------------------

        [TestMethod]
        [Description("SendMsgToUIAsync should handle empty string without throwing")]
        public async Task SendMsgToUIAsync_WithEmptyMessage_ShouldNotThrow() {
            await _sut.SendMsgToUIAsync(string.Empty);
        }

        [TestMethod]
        [Description("SendMsgToUIAsync should handle null message without throwing")]
        public async Task SendMsgToUIAsync_WithNullMessage_ShouldNotThrow() {
            await _sut.SendMsgToUIAsync(null!);
        }
    }
}
