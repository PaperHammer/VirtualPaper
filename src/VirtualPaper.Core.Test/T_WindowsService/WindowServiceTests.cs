using System.Windows;
using Moq;
using VirtualPaper.Services;

namespace VirtualPaper.Core.Test.T_WindowsService {
    [TestClass]
    public class WindowServiceTests {
        private Mock<IServiceProvider> _mockServiceProvider = null!;
        private WindowService _service = null!;

        [TestInitialize]
        public void TestInitialize() {
            _mockServiceProvider = new Mock<IServiceProvider>();
            _service = new WindowService(_mockServiceProvider.Object);
        }

        // TryGet：窗口不存在时应返回 false，out 为 null
        [TestMethod]
        public void TryGet_WhenWindowNotOpen_ShouldReturnFalse() {
            var result = _service.TryGet<FakeWindow>(out var window);

            Assert.IsFalse(result);
            Assert.IsNull(window);
        }

        // TryGet：Show 后应能 TryGet 到实例
        [STATestMethod]
        public void TryGet_AfterShow_ShouldReturnTrue() {
            var fakeWindow = new FakeWindow();
            _mockServiceProvider
                .Setup(sp => sp.GetService(typeof(FakeWindow)))
                .Returns(fakeWindow);

            _service.Show<FakeWindow>();

            var result = _service.TryGet<FakeWindow>(out var window);

            Assert.IsTrue(result);
            Assert.AreSame(fakeWindow, window);
        }

        // Show：窗口关闭后，TryGet 应返回 false（自动清理）
        [STATestMethod]
        public void Show_AfterWindowClosed_ShouldRemoveFromRegistry() {
            var fakeWindow = new FakeWindow();
            _mockServiceProvider
                .Setup(sp => sp.GetService(typeof(FakeWindow)))
                .Returns(fakeWindow);

            _service.Show<FakeWindow>();

            // 模拟关闭事件
            fakeWindow.Close();

            var result = _service.TryGet<FakeWindow>(out _);
            Assert.IsFalse(result);
        }

        // Show：已存在实例时不应重复创建（GetService 只调用一次）
        [STATestMethod]
        public void Show_WhenWindowAlreadyOpen_ShouldNotCreateNewInstance() {
            var fakeWindow = new FakeWindow();
            _mockServiceProvider
                .Setup(sp => sp.GetService(typeof(FakeWindow)))
                .Returns(fakeWindow);

            _service.Show<FakeWindow>();
            _service.Show<FakeWindow>(); // 第二次调用

            _mockServiceProvider.Verify(sp => sp.GetService(typeof(FakeWindow)), Times.Once);
        }

        // InjectParameter：DataContext 实现接口时应调用 ReceiveParameter
        [STATestMethod]
        public void Show_WhenDataContextIsReceiver_ShouldInjectParameter() {
            var fakeWindow = new FakeWindowWithReceiver();
            var receiver = new FakeReceiver();
            fakeWindow.DataContext = receiver;

            _mockServiceProvider
                .Setup(sp => sp.GetService(typeof(FakeWindowWithReceiver)))
                .Returns(fakeWindow);

            var param = new object();
            _service.Show<FakeWindowWithReceiver>(param);

            Assert.AreSame(param, receiver.ReceivedParameter);
        }

        // InjectParameter：parameter 为 null 时不应调用 ReceiveParameter
        [STATestMethod]
        public void Show_WhenParameterIsNull_ShouldNotCallReceiveParameter() {
            var fakeWindow = new FakeWindowWithReceiver();
            var receiver = new FakeReceiver();
            fakeWindow.DataContext = receiver;

            _mockServiceProvider
                .Setup(sp => sp.GetService(typeof(FakeWindowWithReceiver)))
                .Returns(fakeWindow);

            _service.Show<FakeWindowWithReceiver>(null);

            Assert.IsNull(receiver.ReceivedParameter);
        }

        // InjectParameter：DataContext 未实现接口时不应抛出异常
        [STATestMethod]
        public void Show_WhenDataContextIsNotReceiver_ShouldNotThrow() {
            var fakeWindow = new FakeWindow();
            fakeWindow.DataContext = new object(); // 普通对象，不实现接口

            _mockServiceProvider
                .Setup(sp => sp.GetService(typeof(FakeWindow)))
                .Returns(fakeWindow);

            // 不应抛出
            _service.Show<FakeWindow>(new object());
        }
    }

    public class FakeWindow : Window { }

    public class FakeWindowWithReceiver : Window, IWindowParameterReceiver {
        public object? ReceivedParameter { get; private set; }
        public void ReceiveParameter(object? parameter) => ReceivedParameter = parameter;
    }

    public class FakeReceiver : IWindowParameterReceiver {
        public object? ReceivedParameter { get; private set; }
        public void ReceiveParameter(object? parameter) => ReceivedParameter = parameter;
    }
}
