using System.Windows;
using Moq;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Core.Test.T_WindowsService;

[TestClass]
public class WindowServiceConsumerTests {
    private Mock<IWindowService> _mockWindowService = null!;

    [TestInitialize]
    public void TestInitialize() {
        _mockWindowService = new Mock<IWindowService>();
    }

    #region TryGet Tests

    [TestMethod]
    public void TryGet_WhenWindowNotOpen_ShouldReturnFalse() {
        // Setup: TryGet 返回 false，out 参数为 null
        _mockWindowService
            .Setup(s => s.TryGet(out It.Ref<FakeWindow?>.IsAny))
            .Returns(false);

        var service = _mockWindowService.Object;
        var result = service.TryGet<FakeWindow>(out var window);

        Assert.IsFalse(result);
        Assert.IsNull(window);
    }

    [STATestMethod]
    public void TryGet_WhenWindowIsOpen_ShouldReturnTrueAndInstance() {
        var expected = new FakeWindow();
        _mockWindowService
            .Setup(s => s.TryGet(out expected))
            .Returns(true);

        var service = _mockWindowService.Object;
        var result = service.TryGet<FakeWindow>(out var window);

        Assert.IsTrue(result);
        Assert.AreSame(expected, window);
    }

    #endregion

    #region Show Tests

    [TestMethod]
    public void Show_ShouldBeCallable() {
        var service = _mockWindowService.Object;

        // 不抛出异常即通过
        service.Show<FakeWindow>();

        _mockWindowService.Verify(s => s.Show<FakeWindow>(null), Times.Once);
    }

    [TestMethod]
    public void Show_WithParameter_ShouldPassParameter() {
        var param = new object();
        var service = _mockWindowService.Object;

        service.Show<FakeWindow>(param);

        _mockWindowService.Verify(s => s.Show<FakeWindow>(param), Times.Once);
    }

    #endregion

    #region ShowDialogAsync Tests

    [TestMethod]
    public async Task ShowDialogAsync_WhenConfirmed_ShouldReturnTrue() {
        _mockWindowService
            .Setup(s => s.ShowDialogAsync<FakeWindow>(null))
            .ReturnsAsync(true);

        var result = await _mockWindowService.Object.ShowDialogAsync<FakeWindow>();

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ShowDialogAsync_WhenCancelled_ShouldReturnFalse() {
        _mockWindowService
            .Setup(s => s.ShowDialogAsync<FakeWindow>(null))
            .ReturnsAsync(false);

        var result = await _mockWindowService.Object.ShowDialogAsync<FakeWindow>();

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ShowDialogAsync_WithParameter_ShouldPassParameter() {
        var param = "test-param";
        _mockWindowService
            .Setup(s => s.ShowDialogAsync<FakeWindow>(param))
            .ReturnsAsync(true);

        var result = await _mockWindowService.Object.ShowDialogAsync<FakeWindow>(param);

        Assert.IsTrue(result);
        _mockWindowService.Verify(s => s.ShowDialogAsync<FakeWindow>(param), Times.Once);
    }

    #endregion
}

/// <summary>
/// 仅用作泛型类型标识，测试中不会被实例化
/// </summary>
public class FakeWindow : Window { }