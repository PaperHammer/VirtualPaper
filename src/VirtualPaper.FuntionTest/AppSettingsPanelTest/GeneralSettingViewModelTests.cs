using Moq;
using VirtualPaper.AppSettingsPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;

namespace VirtualPaper.FuntionTest.AppSettingsPanelTest {
    [TestClass]
    public class GeneralSettingViewModelTests {
        // 定义 Mock 对象
        private Mock<IWallpaperControlClient> _mockWpClient;
        private Mock<IUserSettingsClient> _mockUserSettings;
        private Mock<IAppUpdaterClient> _mockUpdaterClient;

        private GeneralSettingViewModel _viewModel;

        [TestInitialize]
        public void Setup() {
            // 初始化 Mocks
            _mockWpClient = new Mock<IWallpaperControlClient>();
            _mockUserSettings = new Mock<IUserSettingsClient>();
            _mockUpdaterClient = new Mock<IAppUpdaterClient>();

            _viewModel = new GeneralSettingViewModel(_mockUpdaterClient.Object, _mockUserSettings.Object, _mockWpClient.Object);
        }


        [TestMethod]
        [Description("测试 AppVersionText 是否能正确拼接版本号")]
        public void AppVersionText_ShouldReturnFormattedString() {
            var version = new Version("0.3.1.0");
            _mockWpClient.Setup(x => x.AssemblyVersion).Returns(version);

            Assert.StartsWith("v" + version.ToString(), _viewModel.AppVersionText);
        }

        [TestMethod]
        public void AutoStartStatu_ShouldRaisePropertyChanged() {
            bool eventRaised = false;
            _viewModel.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(_viewModel.AutoStartStatu)) {
                    eventRaised = true;
                }
            };

            _viewModel.AutoStartStatu = "Running";

            Assert.IsTrue(eventRaised, "修改 AutoStartStatu 应该触发 PropertyChanged 事件");
            Assert.AreEqual("Running", _viewModel.AutoStartStatu);
        }

        [TestMethod]
        public void CurrentVersionState_ShouldUpdateCorrectly() {
            
        }
    }
}
