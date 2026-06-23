using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using VirtualPaper.WpSettingsPanel.Utils;

namespace VirtualPaper.UI.Test.T_WpSettings {
    [TestClass]
    public class WpTypeFilterItemTests {
        [TestMethod]
        public void Constructor_SetsFTypesAndLabel() {
            var item = new WpTypeFilterItem([FileType.FImage, FileType.FGif], "TestLabel");

            Assert.HasCount(2, item.FTypes);
            Assert.AreEqual(FileType.FImage, item.FTypes[0]);
            Assert.AreEqual(FileType.FGif, item.FTypes[1]);
            Assert.AreEqual("TestLabel", item.Label);
        }

        [TestMethod]
        public void IsSelected_DefaultIsFalse() {
            var item = new WpTypeFilterItem([FileType.FImage], "img");
            Assert.IsFalse(item.IsSelected);
        }

        [TestMethod]
        public void IsSelected_WhenSetToTrue_RaisesPropertyChanged() {
            var item = new WpTypeFilterItem([FileType.FImage], "img");
            bool raised = false;
            item.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(item.IsSelected)) raised = true;
            };

            item.IsSelected = true;

            Assert.IsTrue(raised);
            Assert.IsTrue(item.IsSelected);
        }

        [TestMethod]
        public void IsSelected_WhenSetToSameValue_DoesNotRaisePropertyChanged() {
            var item = new WpTypeFilterItem([FileType.FImage], "img");
            item.IsSelected = false; // 与默认值相同

            bool raised = false;
            item.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(item.IsSelected)) raised = true;
            };

            item.IsSelected = false; // 再次设置相同值

            Assert.IsFalse(raised);
        }

        [TestMethod]
        public void IsSelected_CanToggleTrueFalseTrue() {
            var item = new WpTypeFilterItem([FileType.FVideo], "vid");

            item.IsSelected = true;
            Assert.IsTrue(item.IsSelected);

            item.IsSelected = false;
            Assert.IsFalse(item.IsSelected);

            item.IsSelected = true;
            Assert.IsTrue(item.IsSelected);
        }
    }
}
