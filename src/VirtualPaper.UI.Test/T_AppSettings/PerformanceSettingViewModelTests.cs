using Moq;
using VirtualPaper.AppSettingsPanel.ViewModels;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UI.Test.T_AppSettings {
    [TestClass]
    public class PerformanceSettingViewModelTests {
        private Mock<IUserSettingsClient> _userSettingsClient = null!;
        private Mock<ISettings> _settings = null!;
        private PerformanceSettingViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _userSettingsClient = new Mock<IUserSettingsClient>();
            _settings = new Mock<ISettings>();

            _settings.SetupProperty(s => s.AppFullscreen, AppWpRunRulesEnum.Silence);
            _settings.SetupProperty(s => s.AppFocus, AppWpRunRulesEnum.Silence);
            _settings.SetupProperty(s => s.BatteryPoweredn, AppWpRunRulesEnum.Silence);
            _settings.SetupProperty(s => s.PowerSaving, AppWpRunRulesEnum.Silence);
            _settings.SetupProperty(s => s.RemoteDesktop, AppWpRunRulesEnum.Silence);
            _settings.SetupProperty(s => s.StatuMechanism, StatuMechanismEnum.Per);
            _settings.SetupProperty(s => s.IsAudioOnlyOnDesktop, false);

            _userSettingsClient.Setup(u => u.Settings).Returns(_settings.Object);

            _vm = new PerformanceSettingViewModel(_userSettingsClient.Object);
        }

        // ── AppWpRunRulesEnum setter 参数化测试 ───────────────────────

        [TestMethod]
        [DataRow("SelectedFullScreenPlayStatuIndex", nameof(ISettings.AppFullscreen))]
        [DataRow("SelectedFocusPlayStatuIndex", nameof(ISettings.AppFocus))]
        [DataRow("SelectedBatteryPowerednPlayStatuIndex", nameof(ISettings.BatteryPoweredn))]
        [DataRow("SelectedPowerSavingPlayStatuIndex", nameof(ISettings.PowerSaving))]
        [DataRow("SelectedRemoteDesktopPlayStatuIndex", nameof(ISettings.RemoteDesktop))]
        public void RunRulesSetter_WhenValueUnchanged_DoesNotCallSave(
            string vmProp, string settingsProp) {
            // 当前值为 Silence(0)，再设一次 0
            SetVmProperty(_vm, vmProp, 0);

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Never);
        }

        [TestMethod]
        [DataRow("SelectedFullScreenPlayStatuIndex", nameof(ISettings.AppFullscreen))]
        [DataRow("SelectedFocusPlayStatuIndex", nameof(ISettings.AppFocus))]
        [DataRow("SelectedBatteryPowerednPlayStatuIndex", nameof(ISettings.BatteryPoweredn))]
        [DataRow("SelectedPowerSavingPlayStatuIndex", nameof(ISettings.PowerSaving))]
        [DataRow("SelectedRemoteDesktopPlayStatuIndex", nameof(ISettings.RemoteDesktop))]
        public void RunRulesSetter_WhenValueChanged_CallsSave(
            string vmProp, string settingsProp) {
            // 从 Silence(0) 改为 Pause(1)
            SetVmProperty(_vm, vmProp, 1);

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Once);
        }

        [TestMethod]
        [DataRow("SelectedFullScreenPlayStatuIndex", nameof(ISettings.AppFullscreen), 1)]
        [DataRow("SelectedFocusPlayStatuIndex", nameof(ISettings.AppFocus), 2)]
        [DataRow("SelectedBatteryPowerednPlayStatuIndex", nameof(ISettings.BatteryPoweredn), 1)]
        [DataRow("SelectedPowerSavingPlayStatuIndex", nameof(ISettings.PowerSaving), 2)]
        [DataRow("SelectedRemoteDesktopPlayStatuIndex", nameof(ISettings.RemoteDesktop), 1)]
        public void RunRulesSetter_WhenValueChanged_UpdatesSettingsProperty(
            string vmProp, string settingsProp, int newIndex) {
            SetVmProperty(_vm, vmProp, newIndex);

            var actual = (int)GetSettingsProperty(_settings.Object, settingsProp);
            Assert.AreEqual(newIndex, actual);
        }

        // ── StatuMechanism setter ─────────────────────────────────────

        [TestMethod]
        public void SelectedStatuMechanismIndex_WhenValueUnchanged_DoesNotCallSave() {
            _settings.Object.StatuMechanism = StatuMechanismEnum.Per; // index = 0
            _vm.SelectedStatuMechanismPlayStatuIndex = 0;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Never);
        }

        [TestMethod]
        public void SelectedStatuMechanismIndex_WhenValueChanged_CallsSave() {
            _settings.Object.StatuMechanism = StatuMechanismEnum.Per; // index = 0
            _vm.SelectedStatuMechanismPlayStatuIndex = 1;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Once);
        }

        [TestMethod]
        public void SelectedStatuMechanismIndex_WhenValueChanged_UpdatesSettings() {
            _settings.Object.StatuMechanism = StatuMechanismEnum.Per;
            _vm.SelectedStatuMechanismPlayStatuIndex = 1;

            Assert.AreEqual(StatuMechanismEnum.All, _settings.Object.StatuMechanism);
        }

        // ── IsAudioOnlyOnDesktop setter ───────────────────────────────

        [TestMethod]
        public void IsAudioOnlyOnDesktop_WhenValueUnchanged_DoesNotCallSave() {
            _settings.Object.IsAudioOnlyOnDesktop = false;
            _vm.IsAudioOnlyOnDesktop = false;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Never);
        }

        [TestMethod]
        public void IsAudioOnlyOnDesktop_WhenValueChanged_CallsSave() {
            _settings.Object.IsAudioOnlyOnDesktop = false;
            _vm.IsAudioOnlyOnDesktop = true;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Once);
        }

        [TestMethod]
        public void IsAudioOnlyOnDesktop_WhenTrue_AudioStatuShowsOn() {
            _vm.IsAudioOnlyOnDesktop = true;

            Assert.AreEqual(
                LanguageUtil.GetI18n(Constants.I18n.Text_On),
                _vm.AudioStatu);
        }

        [TestMethod]
        public void IsAudioOnlyOnDesktop_WhenFalse_AudioStatuShowsOff() {
            _vm.IsAudioOnlyOnDesktop = false;

            Assert.AreEqual(
                LanguageUtil.GetI18n(Constants.I18n.Text_Off),
                _vm.AudioStatu);
        }

        // ── 辅助方法 ─────────────────────────────────────────────────

        private static void SetVmProperty(object vm, string propName, int value) {
            var prop = vm.GetType().GetProperty(propName);
            Assert.IsNotNull(prop, $"属性 {propName} 未找到");
            prop.SetValue(vm, value);
        }

        private static object GetSettingsProperty(ISettings settings, string propName) {
            var prop = typeof(ISettings).GetProperty(propName);
            Assert.IsNotNull(prop, $"ISettings 属性 {propName} 未找到");
            return prop.GetValue(settings)!;
        }
    }
}
