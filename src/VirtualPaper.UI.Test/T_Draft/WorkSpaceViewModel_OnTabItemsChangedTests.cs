using Moq;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Navigation;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;
using Windows.Foundation.Collections;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class WorkSpaceViewModel_OnTabItemsChangedTests {
        private WorkSpaceViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new WorkSpaceViewModel(
                Mock.Of<IUserSettingsClient>(),
                Mock.Of<IGlobalDialogService>());
        }

        private static IVectorChangedEventArgs MakeArgs(CollectionChange change, uint index) {
            var mock = new Mock<IVectorChangedEventArgs>();
            mock.Setup(a => a.CollectionChange).Returns(change);
            mock.Setup(a => a.Index).Returns(index);
            return mock.Object;
        }

        // ── 空集合 ────────────────────────────────────────────────────────

        [TestMethod]
        public void OnTabItemsChanged_EmptyCollection_SetsMinusOne() {
            // TabViewItems 为空，无论任何事件类型都应重置为 -1
            var args = MakeArgs(CollectionChange.ItemInserted, 0);

            _vm.OnTabItemsChanged(null!, args);

            Assert.AreEqual(-1, _vm.SelectedTabIndex);
        }

        // ── ItemInserted ──────────────────────────────────────────────────

        [TestMethod]
        public void OnTabItemsChanged_ItemInserted_SetsSelectedToInsertedIndex() {
            var mockTabItem1 = new Mock<IArcTabViewItem>();
            var mockTabItem2 = new Mock<IArcTabViewItem>();

            _vm.TabViewItems.Add(mockTabItem1.Object);
            _vm.TabViewItems.Add(mockTabItem2.Object);
            var args = MakeArgs(CollectionChange.ItemInserted, 1);

            _vm.OnTabItemsChanged(null!, args);

            Assert.AreEqual(1, _vm.SelectedTabIndex);
        }

        [TestMethod]
        public void OnTabItemsChanged_ItemInserted_FirstItem_SetsSelectedToZero() {
            var mockTabItem1 = new Mock<IArcTabViewItem>();
            _vm.TabViewItems.Add(mockTabItem1.Object);
            var args = MakeArgs(CollectionChange.ItemInserted, 0);

            _vm.OnTabItemsChanged(null!, args);

            Assert.AreEqual(0, _vm.SelectedTabIndex);
        }

        // ── ItemRemoved：移除当前选中项 ───────────────────────────────────

        [TestMethod]
        public void OnTabItemsChanged_ItemRemoved_WasSelected_HasPrevious_SelectsPrevious() {
            var mockTabItem1 = new Mock<IArcTabViewItem>();
            var mockTabItem2 = new Mock<IArcTabViewItem>();

            _vm.TabViewItems.Add(mockTabItem1.Object);
            _vm.TabViewItems.Add(mockTabItem2.Object);
            _vm.SelectedTabIndex = 1;

            _vm.TabViewItems.RemoveAt(1);
            var args = MakeArgs(CollectionChange.ItemRemoved, 1);
            _vm.OnTabItemsChanged(null!, args);

            Assert.AreEqual(0, _vm.SelectedTabIndex);
        }

        [TestMethod]
        public void OnTabItemsChanged_ItemRemoved_WasSelectedFirst_HasNext_SelectsZero() {
            var mockTabItem1 = new Mock<IArcTabViewItem>();
            var mockTabItem2 = new Mock<IArcTabViewItem>();

            _vm.TabViewItems.Add(mockTabItem1.Object);
            _vm.TabViewItems.Add(mockTabItem2.Object);
            _vm.SelectedTabIndex = 0;

            _vm.TabViewItems.RemoveAt(0);
            var args = MakeArgs(CollectionChange.ItemRemoved, 0);
            _vm.OnTabItemsChanged(null!, args);

            // 前一个不存在，选后一个（newIndex 从 -1 → 0）
            Assert.AreEqual(0, _vm.SelectedTabIndex);
        }

        [TestMethod]
        public void OnTabItemsChanged_ItemRemoved_LastItem_SetsMinusOne() {
            var mockTabItem1 = new Mock<IArcTabViewItem>();
            _vm.TabViewItems.Add(mockTabItem1.Object);
            _vm.SelectedTabIndex = 0;

            _vm.TabViewItems.RemoveAt(0);
            var args = MakeArgs(CollectionChange.ItemRemoved, 0);
            _vm.OnTabItemsChanged(null!, args);

            Assert.AreEqual(-1, _vm.SelectedTabIndex);
        }

        // ── ItemRemoved：移除非选中项（在选中项之前）──────────────────────

        [TestMethod]
        public void OnTabItemsChanged_ItemRemoved_BeforeSelected_DecrementsIndex() {
            var mockTabItem1 = new Mock<IArcTabViewItem>();
            var mockTabItem2 = new Mock<IArcTabViewItem>();
            var mockTabItem3 = new Mock<IArcTabViewItem>();

            _vm.TabViewItems.Add(mockTabItem1.Object);
            _vm.TabViewItems.Add(mockTabItem2.Object);
            _vm.TabViewItems.Add(mockTabItem3.Object);
            _vm.SelectedTabIndex = 2;

            _vm.TabViewItems.RemoveAt(0);
            var args = MakeArgs(CollectionChange.ItemRemoved, 0);
            _vm.OnTabItemsChanged(null!, args);

            Assert.AreEqual(1, _vm.SelectedTabIndex);
        }

        // ── ItemRemoved：移除非选中项（在选中项之后）──────────────────────

        [TestMethod]
        public void OnTabItemsChanged_ItemRemoved_AfterSelected_IndexUnchanged() {
            var mockTabItem1 = new Mock<IArcTabViewItem>();
            var mockTabItem2 = new Mock<IArcTabViewItem>();
            var mockTabItem3 = new Mock<IArcTabViewItem>();

            _vm.TabViewItems.Add(mockTabItem1.Object);
            _vm.TabViewItems.Add(mockTabItem2.Object);
            _vm.TabViewItems.Add(mockTabItem3.Object);
            _vm.SelectedTabIndex = 0;

            _vm.TabViewItems.RemoveAt(2);
            var args = MakeArgs(CollectionChange.ItemRemoved, 2);
            _vm.OnTabItemsChanged(null!, args);

            Assert.AreEqual(0, _vm.SelectedTabIndex);
        }

        // ── Reset ─────────────────────────────────────────────────────────

        [TestMethod]
        public void OnTabItemsChanged_Reset_NonEmptyCollection_SelectsZero() {
            var mockTabItem1 = new Mock<IArcTabViewItem>();
            var mockTabItem2 = new Mock<IArcTabViewItem>();

            _vm.TabViewItems.Add(mockTabItem1.Object);
            _vm.TabViewItems.Add(mockTabItem2.Object);
            var args = MakeArgs(CollectionChange.Reset, 0);

            _vm.OnTabItemsChanged(null!, args);

            Assert.AreEqual(0, _vm.SelectedTabIndex);
        }

        [TestMethod]
        public void OnTabItemsChanged_Reset_EmptyCollection_SetsMinusOne() {
            var args = MakeArgs(CollectionChange.Reset, 0);

            _vm.OnTabItemsChanged(null!, args);

            Assert.AreEqual(-1, _vm.SelectedTabIndex);
        }

        // ── ItemChanged（不处理）─────────────────────────────────────────

        [TestMethod]
        public void OnTabItemsChanged_ItemChanged_IndexUnchanged() {
            var mockTabItem1 = new Mock<IArcTabViewItem>();
            _vm.TabViewItems.Add(mockTabItem1.Object);
            _vm.SelectedTabIndex = 0;
            var args = MakeArgs(CollectionChange.ItemChanged, 0);

            _vm.OnTabItemsChanged(null!, args);

            Assert.AreEqual(0, _vm.SelectedTabIndex);
        }
    }
}
