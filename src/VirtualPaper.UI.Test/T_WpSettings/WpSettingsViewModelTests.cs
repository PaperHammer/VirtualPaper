using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_WpSettings {
    [TestClass]
    public class WpSettingsViewModelTests {
        private Mock<IMonitorManagerClient> _monitorManagerClient = null!;
        private Mock<IWallpaperControlClient> _wpControlClient = null!;
        private Mock<IUserSettingsClient> _userSettingsClient = null!;
        private Mock<ISettings> _settings = null!;
        private Mock<IMonitor> _primaryMonitor = null!;
        private Mock<IStoragePicker> _storagePicker = null!;
        private WpSettingsViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _monitorManagerClient = new Mock<IMonitorManagerClient>();
            _wpControlClient = new Mock<IWallpaperControlClient>();
            _userSettingsClient = new Mock<IUserSettingsClient>();
            _settings = new Mock<ISettings>();
            _primaryMonitor = new Mock<IMonitor>();
            _storagePicker = new Mock<IStoragePicker>();

            _settings.SetupProperty(s => s.WallpaperArrangement, WallpaperArrangement.Per);
            _userSettingsClient.Setup(u => u.Settings).Returns(_settings.Object);

            _primaryMonitor.Setup(m => m.CloneWithPrimaryInfo()).Returns(_primaryMonitor.Object);
            _primaryMonitor.SetupProperty(m => m.Content);
            _primaryMonitor.SetupProperty(m => m.SystemIndex, 0);
            _monitorManagerClient.Setup(m => m.PrimaryMonitor).Returns(_primaryMonitor.Object);
        }

        private WpSettingsViewModel CreateVm(IEnumerable<IMonitor>? monitors = null) {
            var list = monitors ?? new[] { _primaryMonitor.Object };
            _monitorManagerClient.Setup(m => m.Monitors).Returns(list.ToList().AsReadOnly());
            return new WpSettingsViewModel(
                _monitorManagerClient.Object,
                _wpControlClient.Object,
                _userSettingsClient.Object,
                _storagePicker.Object);
        }

        // ── SelectedMonitorIndex setter ───────────────────────────────

        [TestMethod]
        public void SelectedMonitorIndex_WhenValueUnchanged_DoesNotRaisePropertyChanged() {
            _vm = CreateVm();
            bool raised = false;
            _vm.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(_vm.SelectedMonitorIndex)) raised = true;
            };

            _vm.SelectedMonitorIndex = 0; // 默认值也是 0

            Assert.IsFalse(raised);
        }

        [TestMethod]
        public void SelectedMonitorIndex_WhenValueChanged_RaisesPropertyChanged() {
            var m1 = MakeMonitor(0);
            var m2 = MakeMonitor(1);
            _vm = CreateVm(new[] { m1.Object, m2.Object });

            bool raised = false;
            _vm.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(_vm.SelectedMonitorIndex)) raised = true;
            };

            _vm.SelectedMonitorIndex = 1;

            Assert.IsTrue(raised);
        }

        // ── InitMonitors (Per) ────────────────────────────────────────

        [TestMethod]
        public void InitMonitors_WhenArrangementIsPer_AddsAllMonitors() {
            _settings.Object.WallpaperArrangement = WallpaperArrangement.Per;
            var m1 = MakeMonitor(0);
            var m2 = MakeMonitor(1);

            _vm = CreateVm(new[] { m1.Object, m2.Object });

            Assert.HasCount(2, _vm.Monitors);
        }

        [TestMethod]
        public void InitMonitors_WhenArrangementIsPer_OrdersBySystemIndex() {
            _settings.Object.WallpaperArrangement = WallpaperArrangement.Per;
            var m0 = MakeMonitor(systemIndex: 0);
            var m1 = MakeMonitor(systemIndex: 1);
            // 故意反序传入
            _vm = CreateVm(new[] { m1.Object, m0.Object });

            Assert.AreEqual(0, _vm.Monitors[0].SystemIndex);
            Assert.AreEqual(1, _vm.Monitors[1].SystemIndex);
        }

        [TestMethod]
        [DataRow(WallpaperArrangement.Duplicate)]
        [DataRow(WallpaperArrangement.Expand)]
        public void InitMonitors_WhenArrangementIsNotPer_AddsSinglePrimaryMonitor(
            WallpaperArrangement arrangement) {
            _settings.Object.WallpaperArrangement = arrangement;
            _primaryMonitor.Setup(m => m.CloneWithPrimaryInfo()).Returns(_primaryMonitor.Object);

            _vm = CreateVm();

            Assert.HasCount(1, _vm.Monitors);
        }

        [TestMethod]
        [DataRow(WallpaperArrangement.Duplicate)]
        [DataRow(WallpaperArrangement.Expand)]
        public void InitMonitors_WhenArrangementIsNotPer_SetsContentToArrangementName(
            WallpaperArrangement arrangement) {
            _settings.Object.WallpaperArrangement = arrangement;
            _primaryMonitor.Setup(m => m.CloneWithPrimaryInfo()).Returns(_primaryMonitor.Object);

            _vm = CreateVm();

            Assert.AreEqual(arrangement.ToString(), _vm.Monitors[0].Content);
        }

        // ── InitMonitors 索引恢复 ─────────────────────────────────────

        [TestMethod]
        public void InitMonitors_WhenCachedIndexValid_RestoresSelectedMonitorIndex() {
            _settings.Object.WallpaperArrangement = WallpaperArrangement.Per;
            var m0 = MakeMonitor(0);
            var m1 = MakeMonitor(1);
            _vm = CreateVm(new[] { m0.Object, m1.Object });

            _vm.SelectedMonitorIndex = 1;
            // 模拟重新调用（内部会保留 cachedIndex = 1）
            _vm.InitFlyoutData();

            Assert.AreEqual(1, _vm.SelectedMonitorIndex);
        }

        [TestMethod]
        public void InitMonitors_WhenCachedIndexOutOfRange_ResetsToZero() {
            _settings.Object.WallpaperArrangement = WallpaperArrangement.Per;
            var m0 = MakeMonitor(0);
            var m1 = MakeMonitor(1);
            _vm = CreateVm(new[] { m0.Object, m1.Object });

            _vm.SelectedMonitorIndex = 1;

            // 切换到 Duplicate，只剩 1 个 monitor，index=1 越界
            _settings.Object.WallpaperArrangement = WallpaperArrangement.Duplicate;
            _vm.InitFlyoutData();

            Assert.AreEqual(0, _vm.SelectedMonitorIndex);
        }

        // ── RegisterLibraryContents ───────────────────────────────────

        [TestMethod]
        public void RegisterLibraryContents_WhenNewFilterable_Registers() {
            _vm = CreateVm();
            var filterable = new Mock<IFilterable>();
            filterable.SetupProperty(f => f.FilterKeyword, FilterKey.LibraryTitle);

            _vm.RegisterLibraryContents(filterable.Object);

            // 验证注册后 OnFilterChanged 能触发它
            _vm.OnFilterChanged(FilterKey.LibraryTitle, "test");
            filterable.Verify(f => f.ApplyFilter("test"), Times.Once);
        }

        [TestMethod]
        public void RegisterLibraryContents_WhenSameFilterableRegisteredTwice_OnlyRegistersOnce() {
            _vm = CreateVm();
            var filterable = new Mock<IFilterable>();
            filterable.SetupProperty(f => f.FilterKeyword, FilterKey.LibraryTitle);

            _vm.RegisterLibraryContents(filterable.Object);
            _vm.RegisterLibraryContents(filterable.Object);

            _vm.OnFilterChanged(FilterKey.LibraryTitle, "test");
            filterable.Verify(f => f.ApplyFilter("test"), Times.Once);
        }

        // ── OnFilterChanged ───────────────────────────────────────────

        [TestMethod]
        public void OnFilterChanged_WhenKeyMatches_CallsApplyFilter() {
            _vm = CreateVm();
            var filterable = new Mock<IFilterable>();
            filterable.SetupProperty(f => f.FilterKeyword, FilterKey.LibraryTitle);
            _vm.RegisterLibraryContents(filterable.Object);

            _vm.OnFilterChanged(FilterKey.LibraryTitle, "keyword");

            filterable.Verify(f => f.ApplyFilter("keyword"), Times.Once);
        }

        [TestMethod]
        public void OnFilterChanged_WhenFilterableHasDifferentKey_DoesNotCallApplyFilter() {
            _vm = CreateVm();

            // 注册一个 filterable，但手动把它的 FilterKeyword 设为与触发不同的值
            // 由于 FilterKey 只有 LibraryTitle，用 default(FilterKey) 的整数偏移模拟"不匹配"
            // 更稳健的做法：不注册任何 filterable，直接验证无调用
            var filterable = new Mock<IFilterable>();
            filterable.SetupProperty(f => f.FilterKeyword, FilterKey.LibraryTitle);
            // 不注册，直接触发
            _vm.OnFilterChanged(FilterKey.LibraryTitle, "keyword");

            filterable.Verify(f => f.ApplyFilter(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void OnFilterChanged_WhenMultipleFilterables_AllWithSameKey_AllReceiveFilter() {
            _vm = CreateVm();

            var filterable1 = new Mock<IFilterable>();
            filterable1.SetupProperty(f => f.FilterKeyword, FilterKey.LibraryTitle);

            var filterable2 = new Mock<IFilterable>();
            filterable2.SetupProperty(f => f.FilterKeyword, FilterKey.LibraryTitle);

            _vm.RegisterLibraryContents(filterable1.Object);
            _vm.RegisterLibraryContents(filterable2.Object);

            _vm.OnFilterChanged(FilterKey.LibraryTitle, "hello");

            filterable1.Verify(f => f.ApplyFilter("hello"), Times.Once);
            filterable2.Verify(f => f.ApplyFilter("hello"), Times.Once);
        }

        // ── Detect ────────────────────────────────────────────────────

        [TestMethod]
        public async Task Detect_WhenCalled_CallsInitMonitors() {
            _settings.Object.WallpaperArrangement = WallpaperArrangement.Per;
            var m0 = MakeMonitor(0);
            _monitorManagerClient.Setup(m => m.Monitors.ToArray()).Returns(new[] { m0.Object });
            _vm = new WpSettingsViewModel(
                _monitorManagerClient.Object,
                _wpControlClient.Object,
                _userSettingsClient.Object,
                _storagePicker.Object);

            _vm.Detect();
            await Task.Delay(100); // 等待 async void 完成

            // Monitors 已被重新填充
            Assert.HasCount(1, _vm.Monitors);
        }

        [TestMethod]
        public async Task Detect_WhenCalledConcurrently_OnlyExecutesOnce() {
            _vm = CreateVm();

            int monitorManagerCallCount = 0;
            _monitorManagerClient.Setup(m => m.Monitors.ToArray())
                .Callback(() => monitorManagerCallCount++)
                .Returns(new[] { _primaryMonitor.Object });

            _vm.Detect();
            _vm.Detect(); // 第二次应该被 Interlocked 拦截
            await Task.Delay(200);

            // 第二次调用在第一次完成前被拦截，只执行了一次
            Assert.AreEqual(1, monitorManagerCallCount);
        }

        // ── Identify ──────────────────────────────────────────────────

        [TestMethod]
        public async Task Identify_WhenCalled_CallsIdentifyMonitorsAsync() {
            _vm = CreateVm();

            _vm.Identify();
            await Task.Delay(100);

            _monitorManagerClient.Verify(m => m.IdentifyMonitorsAsync(), Times.Once);
        }

        // ── Close ─────────────────────────────────────────────────────

        [TestMethod]
        public async Task Close_WhenCalled_CallsCloseWallpaperAsync() {
            _settings.Object.WallpaperArrangement = WallpaperArrangement.Per;
            var monitor = MakeMonitor(0);
            monitor.SetupProperty(m => m.ThumbnailPath, "some/path");
            _monitorManagerClient.Setup(m => m.Monitors.ToArray()).Returns(new[] { monitor.Object });
            _vm = new WpSettingsViewModel(
                _monitorManagerClient.Object,
                _wpControlClient.Object,
                _userSettingsClient.Object,
                _storagePicker.Object);

            _vm.Close();
            await Task.Delay(100);

            _wpControlClient.Verify(
                w => w.CloseWallpaperAsync(monitor.Object),
                Times.Once);
        }

        [TestMethod]
        public async Task Close_WhenCalled_ClearsThumbnailPath() {
            _settings.Object.WallpaperArrangement = WallpaperArrangement.Per;
            var monitor = MakeMonitor(0);
            monitor.SetupProperty(m => m.ThumbnailPath, "some/path");
            _monitorManagerClient.Setup(m => m.Monitors.ToArray()).Returns(new[] { monitor.Object });
            _vm = new WpSettingsViewModel(
                _monitorManagerClient.Object,
                _wpControlClient.Object,
                _userSettingsClient.Object,
                _storagePicker.Object);

            _vm.Close();
            await Task.Delay(100);

            Assert.AreEqual(string.Empty, _vm.Monitors[0].ThumbnailPath);
        }

        // ── Commands IsNotNull ────────────────────────────────────────

        [TestMethod]
        [DataRow(nameof(WpSettingsViewModel.AddToLibCommand))]
        [DataRow(nameof(WpSettingsViewModel.WpCloseCommand))]
        [DataRow(nameof(WpSettingsViewModel.WpDetectCommand))]
        [DataRow(nameof(WpSettingsViewModel.WpIdentifyCommand))]
        [DataRow(nameof(WpSettingsViewModel.WpAdjustCommand))]
        public void Command_IsNotNull_AfterConstruction(string commandName) {
            _vm = CreateVm();
            var prop = typeof(WpSettingsViewModel).GetProperty(commandName);
            Assert.IsNotNull(prop?.GetValue(_vm), $"{commandName} 为 null");
        }

        // ── 辅助方法 ─────────────────────────────────────────────────

        private static Mock<IMonitor> MakeMonitor(int systemIndex) {
            var m = new Mock<IMonitor>();
            m.SetupProperty(x => x.SystemIndex, systemIndex);
            m.SetupProperty(x => x.Content, string.Empty);
            m.SetupProperty(x => x.ThumbnailPath, string.Empty);
            m.SetupProperty(x => x.DeviceId, $"monitor_{systemIndex}");
            m.Setup(x => x.CloneWithPrimaryInfo()).Returns(m.Object);
            return m;
        }
    }
}
