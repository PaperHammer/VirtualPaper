using Moq;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.UIComponent.Data;

namespace VirtualPaper.UI.Test.T_Intelligent {
    // ====================================================================
    //  ConfigSpaceViewModel
    //  — 初始状态 / RefreshCardComponentData / 命令不为 null / Dispose
    // ====================================================================
    [TestClass]
    public class ConfigSpaceViewModelTests {
        private ConfigSpaceViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new ConfigSpaceViewModel();
        }

        [TestCleanup]
        public void Cleanup() {
            _vm.Dispose();
        }

        // 初始状态

        [TestMethod]
        [Description("构造后 PreviousStepCommand / NextStepCommand 应不为 null")]
        public void Constructor_Commands_AreNotNull() {
            Assert.IsNotNull(_vm.PreviousStepCommand, "PreviousStepCommand should not be null");
            Assert.IsNotNull(_vm.NextStepCommand, "NextStepCommand should not be null");
        }

        [TestMethod]
        [Description("构造后各文本属性应为 null，BtnVisible / IsNextEnable 为 false")]
        public void Constructor_DefaultPropertyValues() {
            Assert.IsNull(_vm.PreviousStepBtnText);
            Assert.IsNull(_vm.NextStepBtnText);
            Assert.IsFalse(_vm.IsNextEnable);
            Assert.IsFalse(_vm.BtnVisible);
        }

        // ── RefreshCardComponentData ──────────────────────────────────────

        [TestMethod]
        [Description("_cardComponent 为 null 时 RefreshCardComponentData 不应抛出，属性保持默认")]
        public void RefreshCardComponentData_WhenCardComponentIsNull_DoesNotThrow() {
            // Act — 若抛出则 MSTest 自动标记失败
            _vm.RefreshCardComponentData();

            // 属性应保持不变
            Assert.IsNull(_vm.PreviousStepBtnText);
            Assert.IsNull(_vm.NextStepBtnText);
        }

        [TestMethod]
        [Description("绑定 CardComponent 后调用 RefreshCardComponentData，属性应从 CardComponent 同步")]
        public void RefreshCardComponentData_WithCardComponent_SyncsProperties() {
            var card = new Mock<ICardComponent>();
            card.Setup(c => c.PreviousStepBtnText).Returns("Back");
            card.Setup(c => c.NextStepBtnText).Returns("Next");
            card.Setup(c => c.IsNextEnable).Returns(true);
            card.Setup(c => c.BtnVisible).Returns(true);
            _vm._cardComponent = card.Object;

            _vm.RefreshCardComponentData();

            Assert.AreEqual("Back", _vm.PreviousStepBtnText);
            Assert.AreEqual("Next", _vm.NextStepBtnText);
            Assert.IsTrue(_vm.IsNextEnable);
            Assert.IsTrue(_vm.BtnVisible);
        }

        [TestMethod]
        [Description("RefreshCardComponentData 多次调用应始终以 CardComponent 最新值为准")]
        public void RefreshCardComponentData_CalledTwice_ReflectsLatestValues() {
            var card = new Mock<ICardComponent>();
            card.SetupSequence(c => c.NextStepBtnText)
                .Returns("Next")
                .Returns("Submit");
            _vm._cardComponent = card.Object;

            _vm.RefreshCardComponentData();
            Assert.AreEqual("Next", _vm.NextStepBtnText);

            _vm.RefreshCardComponentData();
            Assert.AreEqual("Submit", _vm.NextStepBtnText);
        }

        // ── PropertyChanged 通知 ─────────────────────────────────────────

        [TestMethod]
        [Description("设置 PreviousStepBtnText 应触发 PropertyChanged")]
        public void PreviousStepBtnText_Setter_RaisesPropertyChanged() {
            var changed = false;
            _vm.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(_vm.PreviousStepBtnText)) changed = true;
            };

            _vm.PreviousStepBtnText = "Back";

            Assert.IsTrue(changed);
        }

        [TestMethod]
        [Description("设置 BtnVisible 应触发 PropertyChanged")]
        public void BtnVisible_Setter_RaisesPropertyChanged() {
            var changed = false;
            _vm.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(_vm.BtnVisible)) changed = true;
            };

            _vm.BtnVisible = true;

            Assert.IsTrue(changed);
        }

        // ── Dispose ───────────────────────────────────────────────────────

        [TestMethod]
        [Description("Dispose 后 PreviousStepCommand / NextStepCommand 应被置为 null")]
        public void Dispose_NullifiesCommands() {
            _vm.Dispose();

            Assert.IsNull(_vm.PreviousStepCommand, "PreviousStepCommand should be null after Dispose");
            Assert.IsNull(_vm.NextStepCommand, "NextStepCommand should be null after Dispose");
        }

        [TestMethod]
        [Description("Dispose 连续调用不应抛出异常")]
        public void Dispose_CalledTwice_DoesNotThrow() {
            _vm.Dispose();
            _vm.Dispose(); // 第二次调用不应抛出
        }
    }
}
