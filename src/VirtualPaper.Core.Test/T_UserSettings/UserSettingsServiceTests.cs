using System.Reflection;
using System.Text.Json.Serialization;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Models.Cores;
using VirtualPaper.Services;
using Monitor = VirtualPaper.Models.Cores.Monitor;

namespace VirtualPaper.Core.Test.T_UserSettings {
    [TestClass]
    public class UserSettingsServiceTests {
        private Mock<IMonitorManager> _mockMonitorManager = null!;
        private Mock<IJsonSaver> _mockJsonSaver = null!;
        private Monitor _primaryMonitor = null!;

        [TestInitialize]
        public void TestInitialize() {
            _primaryMonitor = new Monitor { IsPrimary = true };

            _mockMonitorManager = new Mock<IMonitorManager>();
            _mockMonitorManager.Setup(m => m.PrimaryMonitor).Returns(_primaryMonitor);
            _mockMonitorManager.Setup(m => m.Monitors).Returns([_primaryMonitor]);

            _mockJsonSaver = new Mock<IJsonSaver>();
            _mockJsonSaver
                .Setup(s => s.Load<Settings>(It.IsAny<string>(), It.IsAny<JsonSerializerContext>()))
                .Returns(new Settings());
        }

        // Load<T> 非法类型应抛出 InvalidCastException
        [TestMethod]
        public void Load_WithUnsupportedType_ShouldThrowInvalidCastException() {
            var service = CreateService();

            Assert.Throws<InvalidCastException>(() => service.Load<string>());
        }

        // Save<T> 非法类型应抛出 InvalidCastException
        [TestMethod]
        public void Save_WithUnsupportedType_ShouldThrowInvalidCastException() {
            var service = CreateService();

            Assert.Throws<InvalidCastException>(() => service.Save<string>());
        }

        // Load<ISettings> 失败时降级为默认 Settings
        [TestMethod]
        public void Load_Settings_WhenJsonCorrupted_ShouldFallbackToDefault() {
            _mockJsonSaver
                .Setup(s => s.Load<Settings>(It.IsAny<string>(), It.IsAny<JsonSerializerContext>()))
                .Throws<Exception>();

            var service = CreateService();

            Assert.IsNotNull(service.Settings);
            // 降级后得到 new Settings()，不应为 null
        }

        // Load<List<IApplicationRules>> 失败时降级包含默认规则
        [TestMethod]
        public void Load_AppRules_WhenJsonCorrupted_ShouldFallbackToDefaultRule() {
            _mockJsonSaver
                .Setup(s => s.Load<List<ApplicationRules>>(It.IsAny<string>(), It.IsAny<JsonSerializerContext>()))
                .Throws<Exception>();

            var service = CreateService();

            Assert.HasCount(1, service.AppRules);
            Assert.AreEqual(Constants.CoreField.AppName, ((ApplicationRules)service.AppRules[0]).AppName);
        }

        // Load<List<IWallpaperLayout>> 失败时降级为空列表
        [TestMethod]
        public void Load_WallpaperLayouts_WhenJsonCorrupted_ShouldFallbackToEmptyList() {
            _mockJsonSaver
                .Setup(s => s.Load<List<WallpaperLayout>>(It.IsAny<string>(), It.IsAny<JsonSerializerContext>()))
                .Throws<Exception>();

            var service = CreateService();

            Assert.IsEmpty(service.WallpaperLayouts);
        }

        // SelectedMonitor：Settings 中存有匹配的 Monitor 时应使用它
        [TestMethod]
        public void Constructor_WhenSettingsMonitorMatchesKnown_ShouldUseMatchedMonitor() {
            var savedMonitor = Mock.Of<Monitor>();
            var settings = new Settings { SelectedMonitor = savedMonitor };

            _mockMonitorManager.Setup(m => m.Monitors).Returns([savedMonitor, _primaryMonitor]);
            _mockJsonSaver
                .Setup(s => s.Load<Settings>(It.IsAny<string>(), It.IsAny<JsonSerializerContext>()))
                .Returns(settings);

            var service = CreateService();

            Assert.AreEqual(savedMonitor, service.Settings.SelectedMonitor);
        }

        // SelectedMonitor：Settings 中 Monitor 不在列表时应回退到 Primary
        [TestMethod]
        public void Constructor_WhenSettingsMonitorNotFound_ShouldFallbackToPrimary() {
            var unknownMonitor = Mock.Of<Monitor>();
            var settings = new Settings { SelectedMonitor = unknownMonitor };

            _mockMonitorManager.Setup(m => m.Monitors).Returns([_primaryMonitor]); // 不包含 unknownMonitor
            _mockJsonSaver
                .Setup(s => s.Load<Settings>(It.IsAny<string>(), It.IsAny<JsonSerializerContext>()))
                .Returns(settings);

            var service = CreateService();

            Assert.AreEqual(_primaryMonitor, service.Settings.SelectedMonitor);
        }

        // 版本号不同时应更新版本相关字段并标记 IsUpdated
        [TestMethod]
        public void Constructor_WhenAppVersionDiffers_ShouldUpdateVersionFields() {
            var settings = new Settings { AppVersion = "0.0.0.0" }; // 旧版本
            _mockJsonSaver
                .Setup(s => s.Load<Settings>(It.IsAny<string>(), It.IsAny<JsonSerializerContext>()))
                .Returns(settings);

            var service = CreateService();

            Assert.IsTrue(service.Settings.IsUpdated);
            Assert.AreEqual(
                Assembly.GetAssembly(typeof(UserSettingsService))!.GetName().Version!.ToString(),
                service.Settings.AppVersion);
        }

        // 版本号相同时不应标记 IsUpdated
        [TestMethod]
        public void Constructor_WhenAppVersionMatches_ShouldNotMarkAsUpdated() {
            var currentVersion = Assembly.GetAssembly(typeof(UserSettingsService))!.GetName().Version!.ToString();
            var settings = new Settings { AppVersion = currentVersion, IsUpdated = false };
            _mockJsonSaver
                .Setup(s => s.Load<Settings>(It.IsAny<string>(), It.IsAny<JsonSerializerContext>()))
                .Returns(settings);

            var service = CreateService();

            Assert.IsFalse(service.Settings.IsUpdated);
        }

        private UserSettingsService CreateService() =>
            new UserSettingsService(_mockMonitorManager.Object, _mockJsonSaver.Object);
    }
}
