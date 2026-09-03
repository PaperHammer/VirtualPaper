using VirtualPaper.UIComponent.Templates;
using Windows.System;

namespace VirtualPaper.UIComponent.Test.T_UIComponent {
    [TestClass]
    public class ArcWindowKeyboardPolicyTests {
        [TestMethod]
        [DataRow(VirtualKey.Tab)]
        [DataRow(VirtualKey.F6)]
        public void GlobalNavigationKey_IsRecognized(VirtualKey key) {
            Assert.IsTrue(ArcWindowKeyboardPolicy.IsGlobalNavigationKey(key));
        }

        [TestMethod]
        [DataRow(VirtualKey.Enter)]
        [DataRow(VirtualKey.Space)]
        [DataRow(VirtualKey.Left)]
        [DataRow(VirtualKey.Right)]
        [DataRow(VirtualKey.Up)]
        [DataRow(VirtualKey.Down)]
        [DataRow(VirtualKey.Home)]
        [DataRow(VirtualKey.End)]
        [DataRow(VirtualKey.PageUp)]
        [DataRow(VirtualKey.PageDown)]
        [DataRow(VirtualKey.Escape)]
        public void ControlInteractionKey_IsRecognized(VirtualKey key) {
            Assert.IsTrue(ArcWindowKeyboardPolicy.IsControlInteractionKey(key));
        }

        [TestMethod]
        [DataRow(VirtualKey.A)]
        [DataRow(VirtualKey.Number1)]
        [DataRow(VirtualKey.Back)]
        [DataRow(VirtualKey.Delete)]
        public void TextEditingKey_IsNotClassifiedAsWindowNavigation(VirtualKey key) {
            Assert.IsFalse(ArcWindowKeyboardPolicy.IsGlobalNavigationKey(key));
            Assert.IsFalse(ArcWindowKeyboardPolicy.IsControlInteractionKey(key));
        }
    }
}
