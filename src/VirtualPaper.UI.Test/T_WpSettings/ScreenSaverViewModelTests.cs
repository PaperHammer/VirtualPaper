using Moq;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_WpSettings {
    [TestClass]
    public class ScreenSaverViewModelTests {
        private Mock<IUserSettingsClient> _userSettingsClient = null!;
        private Mock<IScrCommandsClient> _scrCommandsClient = null!;
        private Mock<ISettings> _settings = null!;
        private ScreenSaverViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _userSettingsClient = new Mock<IUserSettingsClient>();
            _scrCommandsClient = new Mock<IScrCommandsClient>();
            _settings = new Mock<ISettings>();

            _settings.SetupProperty(s => s.IsScreenSaverOn, false);
            _settings.SetupProperty(s => s.IsRunningLock, false);
            _settings.SetupProperty(s => s.WaitingTime, 1);
            _settings.SetupProperty(s => s.ScreenSaverEffect, ScrEffect.None);
            _settings.Setup(s => s.WhiteListScr).Returns(new List<ProcInfo>());

            _userSettingsClient.Setup(u => u.Settings).Returns(_settings.Object);

            _vm = new ScreenSaverViewModel(
                _userSettingsClient.Object,
                _scrCommandsClient.Object);
        }

        [TestCleanup]
        public void Cleanup() {
            _vm.Dispose();
        }

        // ── IsScreenSaverOn setter ────────────────────────────────────

        [TestMethod]
        public void IsScreenSaverOn_WhenSetTrue_CallsScrCommandsStart() {
            _vm.IsScreenSaverOn = true;

            _scrCommandsClient.Verify(s => s.Start(), Times.Once);
        }

        [TestMethod]
        public void IsScreenSaverOn_WhenSetFalse_CallsScrCommandsStop() {
            // 先设为 true 再设为 false，确保触发 Stop
            _settings.Object.IsScreenSaverOn = true;
            _vm.IsScreenSaverOn = true; // 先同步内部状态
            _scrCommandsClient.Invocations.Clear();

            _vm.IsScreenSaverOn = false;

            _scrCommandsClient.Verify(s => s.Stop(), Times.Once);
        }

        [TestMethod]
        public void IsScreenSaverOn_WhenValueUnchanged_DoesNotCallSave() {
            _settings.Object.IsScreenSaverOn = false;
            _vm.IsScreenSaverOn = false; // 与 Settings 相同

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Never);
        }

        [TestMethod]
        public void IsScreenSaverOn_WhenValueChanged_CallsSave() {
            _settings.Object.IsScreenSaverOn = false;

            _vm.IsScreenSaverOn = true;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Once);
        }

        [TestMethod]
        public void IsScreenSaverOn_WhenSetTrue_ScreenSaverStatuShowsOn() {
            _vm.IsScreenSaverOn = true;

            Assert.AreEqual(
                LanguageUtil.GetI18n(Constants.I18n.Text_On),
                _vm.ScreenSaverStatu);
        }

        [TestMethod]
        public void IsScreenSaverOn_WhenSetFalse_ScreenSaverStatuShowsOff() {
            _vm.IsScreenSaverOn = false;

            Assert.AreEqual(
                LanguageUtil.GetI18n(Constants.I18n.Text_Off),
                _vm.ScreenSaverStatu);
        }

        // ── IsRunningLock setter ──────────────────────────────────────

        [TestMethod]
        public void IsRunningLock_WhenValueChanged_CallsChangeLockStatu() {
            _settings.Object.IsRunningLock = false;

            _vm.IsRunningLock = true;

            _scrCommandsClient.Verify(s => s.ChangeLockStatu(true), Times.Once);
        }

        [TestMethod]
        public void IsRunningLock_WhenValueUnchanged_DoesNotCallSave() {
            _settings.Object.IsRunningLock = false;
            _vm.IsRunningLock = false;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Never);
        }

        [TestMethod]
        public void IsRunningLock_WhenValueChanged_CallsSave() {
            _settings.Object.IsRunningLock = false;

            _vm.IsRunningLock = true;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Once);
        }

        // ── WaitingTime setter ────────────────────────────────────────

        [TestMethod]
        public void WaitingTime_WhenValueUnchanged_DoesNotCallSave() {
            _settings.Object.WaitingTime = 5;
            _vm.WaitingTime = 5;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Never);
        }

        [TestMethod]
        public void WaitingTime_WhenValueChanged_CallsSave() {
            _settings.Object.WaitingTime = 1;

            _vm.WaitingTime = 10;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Once);
        }

        [TestMethod]
        public void WaitingTime_WhenValueChanged_UpdatesSettings() {
            _settings.Object.WaitingTime = 1;

            _vm.WaitingTime = 15;

            Assert.AreEqual(15, _settings.Object.WaitingTime);
        }

        // ── SeletedEffectIndx setter ──────────────────────────────────

        [TestMethod]
        public void SeletedEffectIndx_WhenValueUnchanged_DoesNotCallSave() {
            _settings.Object.ScreenSaverEffect = ScrEffect.None; // index = 0
            _vm.SeletedEffectIndx = 0;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Never);
        }

        [TestMethod]
        public void SeletedEffectIndx_WhenValueChanged_CallsSave() {
            _settings.Object.ScreenSaverEffect = ScrEffect.None; // index = 0

            _vm.SeletedEffectIndx = 1;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Once);
        }

        [TestMethod]
        public void SeletedEffectIndx_WhenValueChanged_UpdatesSettings() {
            _settings.Object.ScreenSaverEffect = ScrEffect.None;

            _vm.SeletedEffectIndx = (int)ScrEffect.Bubble;

            Assert.AreEqual(ScrEffect.Bubble, _settings.Object.ScreenSaverEffect);
        }

        // ── AddToWhiteListScr ─────────────────────────────────────────

        [TestMethod]
        public async Task AddToWhiteListScr_AddsToProcsFiltered() {
            var proc = new ProcInfo("notepad", @"C:\Windows\notepad.exe", "icon.png");

            await InvokeAddToWhiteListScrAsync(proc);

            Assert.HasCount(1, _vm.ProcsFiltered);
            Assert.AreSame(proc, _vm.ProcsFiltered[0]);
        }

        [TestMethod]
        public async Task AddToWhiteListScr_AddsToInternalWhiteList() {
            var proc = new ProcInfo("notepad", @"C:\Windows\notepad.exe", "icon.png");

            await InvokeAddToWhiteListScrAsync(proc);

            Assert.Contains(proc, _vm._whiteListScr);
        }

        [TestMethod]
        public async Task AddToWhiteListScr_CallsScrCommandsAddToWhiteList() {
            var proc = new ProcInfo("notepad", @"C:\Windows\notepad.exe", "icon.png");

            await InvokeAddToWhiteListScrAsync(proc);

            _scrCommandsClient.Verify(s => s.AddToWhiteList("notepad"), Times.Once);
        }

        [TestMethod]
        public async Task AddToWhiteListScr_CallsUserSettingsSave() {
            var proc = new ProcInfo("notepad", @"C:\Windows\notepad.exe", "icon.png");

            await InvokeAddToWhiteListScrAsync(proc);

            _userSettingsClient.Verify(u => u.Save<ISettings>(), Times.Once);
        }

        // ── RemoveFromWhiteScr ────────────────────────────────────────

        [TestMethod]
        public async Task RemoveFromWhiteScr_RemovesFromProcsFiltered() {
            var proc = new ProcInfo("notepad", @"C:\Windows\notepad.exe", "icon.png");
            await InvokeAddToWhiteListScrAsync(proc);

            await InvokeRemoveFromWhiteScrAsync(proc);

            Assert.HasCount(0, _vm.ProcsFiltered);
        }

        [TestMethod]
        public async Task RemoveFromWhiteScr_RemovesFromInternalWhiteList() {
            var proc = new ProcInfo("notepad", @"C:\Windows\notepad.exe", "icon.png");
            await InvokeAddToWhiteListScrAsync(proc);

            await InvokeRemoveFromWhiteScrAsync(proc);

            Assert.DoesNotContain(proc, _vm._whiteListScr);
        }

        [TestMethod]
        public async Task RemoveFromWhiteScr_CallsScrCommandsRemoveFromWhiteList() {
            var proc = new ProcInfo("notepad", @"C:\Windows\notepad.exe", "icon.png");
            await InvokeAddToWhiteListScrAsync(proc);
            _scrCommandsClient.Invocations.Clear();

            await InvokeRemoveFromWhiteScrAsync(proc);

            _scrCommandsClient.Verify(s => s.RemoveFromWhiteList("notepad"), Times.Once);
        }

        [TestMethod]
        public async Task RemoveFromWhiteScr_CallsUserSettingsSave() {
            var proc = new ProcInfo("notepad", @"C:\Windows\notepad.exe", "icon.png");
            await InvokeAddToWhiteListScrAsync(proc);
            _userSettingsClient.Invocations.Clear();

            await InvokeRemoveFromWhiteScrAsync(proc);

            _userSettingsClient.Verify(u => u.Save<ISettings>(), Times.Once);
        }

        // ── UpdateScrSettginsAsync ────────────────────────────────────

        [TestMethod]
        public async Task UpdateScrSettginsAsync_LoadsSettingsFromClient() {
            await _vm.UpdateScrSettginsAsync();

            _userSettingsClient.Verify(u => u.LoadAsync<ISettings>(), Times.Once);
        }

        [TestMethod]
        public async Task UpdateScrSettginsAsync_SyncsIsScreenSaverOnFromSettings() {
            _settings.Object.IsScreenSaverOn = true;

            await _vm.UpdateScrSettginsAsync();

            Assert.IsTrue(_vm.IsScreenSaverOn);
        }

        [TestMethod]
        public async Task UpdateScrSettginsAsync_SyncsIsRunningLockFromSettings() {
            _settings.Object.IsRunningLock = true;

            await _vm.UpdateScrSettginsAsync();

            Assert.IsTrue(_vm.IsRunningLock);
        }

        [TestMethod]
        public async Task UpdateScrSettginsAsync_SyncsSeletedEffectIndxFromSettings() {
            _settings.Object.ScreenSaverEffect = ScrEffect.Bubble;

            await _vm.UpdateScrSettginsAsync();

            Assert.AreEqual((int)ScrEffect.Bubble, _vm.SeletedEffectIndx);
        }

        // ── StopListenForClients ──────────────────────────────────────

        [TestMethod]
        public void StopListenForClients_WhenCalled_DoesNotThrow() {
            // 验证取消操作本身不抛出
            _vm.StopListenForClients();
        }

        // ── Dispose ───────────────────────────────────────────────────

        [TestMethod]
        public void Dispose_WhenCalledTwice_DoesNotThrow() {
            _vm.Dispose();
            _vm.Dispose(); // 第二次不应抛出
        }

        [TestMethod]
        public void Dispose_CancelsCtsListen() {
            // ListenForClients 内部依赖 _ctsListen，Dispose 后再 Stop 不应出错
            _vm.Dispose();
            _vm.StopListenForClients(); // 不抛出即通过
        }

        // ── Effects 集合初始化 ─────────────────────────────────────────

        [TestMethod]
        public void Effects_AfterConstruction_HasTwoItems() {
            Assert.HasCount(2, _vm.Effects);
        }

        // ── 辅助方法 ──────────────────────────────────────────────────

        /// <summary>通过反射调用 private async void AddToWhiteListScr</summary>
        private async Task InvokeAddToWhiteListScrAsync(ProcInfo proc) {
            var method = typeof(ScreenSaverViewModel)
                .GetMethod("AddToWhiteListScr",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "AddToWhiteListScr 方法未找到");
            method.Invoke(_vm, new object[] { proc });
            await Task.Delay(50); // async void，等待 Task.Run 完成
        }

        /// <summary>通过反射调用 internal async void RemoveFromWhiteScr</summary>
        private async Task InvokeRemoveFromWhiteScrAsync(ProcInfo proc) {
            var method = typeof(ScreenSaverViewModel)
                .GetMethod("RemoveFromWhiteScr",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "RemoveFromWhiteScr 方法未找到");
            method.Invoke(_vm, new object[] { proc });
            await Task.Delay(50); // async void，等待 Task.Run 完成
        }
    }
}
