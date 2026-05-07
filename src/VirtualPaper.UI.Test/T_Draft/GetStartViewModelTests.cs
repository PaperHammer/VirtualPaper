using Moq;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UI.Test.Utils;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class GetStartViewModelTests {
        private Mock<IUserSettingsClient> _userSettingsClient = null!;
        private GetStartViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            CrossThreadInvoker.Initialize(new T_UiSynchronizationContext());
            _userSettingsClient = new Mock<IUserSettingsClient>();
            _userSettingsClient.Setup(u => u.RecentUseds)
                .Returns(new List<IRecentUsed>());

            _vm = new GetStartViewModel(_userSettingsClient.Object);
        }

        // ── InitCollection ────────────────────────────────────────────

        [TestMethod]
        public void InitCollection_WhenCalled_PopulatesRecentUseds() {
            var item1 = MakeRecentUsed("Alpha.jpg", @"C:\wp\Alpha.jpg");
            var item2 = MakeRecentUsed("Beta.jpg", @"C:\wp\Beta.jpg");
            _userSettingsClient.Setup(u => u.RecentUseds)
                .Returns(new List<IRecentUsed> { item1, item2 });

            _vm.InitCollection();

            Assert.HasCount(2, _vm.RecentUseds);
        }

        [TestMethod]
        public void InitCollection_WhenCalledTwice_DoesNotDuplicate() {
            var item = MakeRecentUsed("Alpha.jpg", @"C:\wp\Alpha.jpg");
            _userSettingsClient.Setup(u => u.RecentUseds)
                .Returns(new List<IRecentUsed> { item });

            _vm.InitCollection();
            _vm.InitCollection();

            Assert.HasCount(1, _vm.RecentUseds);
        }

        [TestMethod]
        public void InitCollection_WhenSourceIsEmpty_RecentUsedsIsEmpty() {
            _userSettingsClient.Setup(u => u.RecentUseds)
                .Returns(new List<IRecentUsed>());

            _vm.InitCollection();

            Assert.HasCount(0, _vm.RecentUseds);
        }

        // ── FilterByTitle ─────────────────────────────────────────────

        [TestMethod]
        public void FilterByTitle_WhenKeywordMatches_ShowsMatchingItems() {
            var match = MakeRecentUsed("Sunset.jpg", @"C:\wp\Sunset.jpg");
            var noMatch = MakeRecentUsed("CityNight.jpg", @"C:\wp\CityNight.jpg");
            PopulateCollection(match, noMatch);

            _vm.FilterByTitle("Sunset");

            Assert.HasCount(1, _vm.RecentUseds);
            Assert.AreSame(match, _vm.RecentUseds[0]);
        }

        [TestMethod]
        public void FilterByTitle_WhenKeywordEmpty_ShowsAllItems() {
            var a = MakeRecentUsed("Alpha.jpg", @"C:\wp\Alpha.jpg");
            var b = MakeRecentUsed("Beta.jpg", @"C:\wp\Beta.jpg");
            PopulateCollection(a, b);

            _vm.FilterByTitle(string.Empty);

            Assert.HasCount(2, _vm.RecentUseds);
        }

        [TestMethod]
        public void FilterByTitle_IsCaseInsensitive() {
            var item = MakeRecentUsed("Forest.jpg", @"C:\wp\Forest.jpg");
            PopulateCollection(item);

            _vm.FilterByTitle("forest");

            Assert.HasCount(1, _vm.RecentUseds);
        }

        [TestMethod]
        public void FilterByTitle_WhenNoMatch_RecentUsedsIsEmpty() {
            var item = MakeRecentUsed("Mountain.jpg", @"C:\wp\Mountain.jpg");
            PopulateCollection(item);

            _vm.FilterByTitle("Ocean");

            Assert.HasCount(0, _vm.RecentUseds);
        }

        [TestMethod]
        public void FilterByTitle_WhenCalledTwice_SecondFilterOverridesFirst() {
            var a = MakeRecentUsed("Alpha.jpg", @"C:\wp\Alpha.jpg");
            var b = MakeRecentUsed("Beta.jpg", @"C:\wp\Beta.jpg");
            PopulateCollection(a, b);

            _vm.FilterByTitle("Alpha");
            _vm.FilterByTitle("Beta");

            Assert.HasCount(1, _vm.RecentUseds);
            Assert.AreSame(b, _vm.RecentUseds[0]);
        }

        [TestMethod]
        public void FilterByTitle_WhenFileNameIsNull_ItemIsExcluded() {
            var nullName = MakeRecentUsed(null, @"C:\wp\unknown.jpg");
            PopulateCollection(nullName);

            _vm.FilterByTitle("any");

            Assert.HasCount(0, _vm.RecentUseds);
        }

        // ── ApplyFilter ───────────────────────────────────────────────

        [TestMethod]
        public void ApplyFilter_DelegatesToFilterByTitle() {
            var item = MakeRecentUsed("Cherry.jpg", @"C:\wp\Cherry.jpg");
            PopulateCollection(item);

            _vm.ApplyFilter("Cherry");

            Assert.HasCount(1, _vm.RecentUseds);
        }

        // ── RemoveFromListCommand ─────────────────────────────────────

        [TestMethod]
        public async Task RemoveFromListCommand_WhenItemNotNull_RemovesFromRecentUseds() {
            var item = MakeRecentUsed("Alpha.jpg", @"C:\wp\Alpha.jpg");
            PopulateCollection(item);
            _userSettingsClient.Setup(u => u.DeleteRecetUsedAsync(item))
                .Returns(Task.CompletedTask);

            await InvokeRemoveCommandAsync(item);

            Assert.HasCount(0, _vm.RecentUseds);
        }

        [TestMethod]
        public async Task RemoveFromListCommand_WhenItemNotNull_CallsDeleteRecetUsedAsync() {
            var item = MakeRecentUsed("Alpha.jpg", @"C:\wp\Alpha.jpg");
            PopulateCollection(item);
            _userSettingsClient.Setup(u => u.DeleteRecetUsedAsync(item))
                .Returns(Task.CompletedTask);

            await InvokeRemoveCommandAsync(item);

            _userSettingsClient.Verify(u => u.DeleteRecetUsedAsync(item), Times.Once);
        }

        [TestMethod]
        public async Task RemoveFromListCommand_WhenItemIsNull_DoesNotCallDelete() {
            await InvokeRemoveCommandAsync(null);

            _userSettingsClient.Verify(
                u => u.DeleteRecetUsedAsync(It.IsAny<IRecentUsed>()),
                Times.Never);
        }

        [TestMethod]
        public async Task RemoveFromListCommand_WhenItemNotNull_AlsoRemovesFromInternalList() {
            var item = MakeRecentUsed("Alpha.jpg", @"C:\wp\Alpha.jpg");
            PopulateCollection(item);
            _userSettingsClient.Setup(u => u.DeleteRecetUsedAsync(item))
                .Returns(Task.CompletedTask);

            await InvokeRemoveCommandAsync(item);

            // 过滤后内部列表已空，FilterByTitle 不会把 item 加回来
            _vm.FilterByTitle(string.Empty);
            Assert.HasCount(0, _vm.RecentUseds);
        }

        // ── Command 初始化 ─────────────────────────────────────────────

        [TestMethod]
        public void RemoveFromListCommand_IsNotNull_AfterConstruction() {
            Assert.IsNotNull(_vm.RemoveFromListCommand);
        }

        [TestMethod]
        public void CopyPathCommand_IsNotNull_AfterConstruction() {
            Assert.IsNotNull(_vm.CopyPathCommand);
        }

        [TestMethod]
        public void ShowOnDiskCommand_IsNotNull_AfterConstruction() {
            Assert.IsNotNull(_vm.ShowOnDiskCommand);
        }

        [TestMethod]
        public void RemoveFromListCommand_CanExecute_ReturnsTrue() {
            Assert.IsTrue(_vm.RemoveFromListCommand!.CanExecute(null));
        }

        // ── IsElevated ─────────────────────────────────────────────────

        [TestMethod]
        public void IsElevated_IsAssigned_AfterConstruction() {
            var _ = _vm.IsElevated;
            Assert.IsInstanceOfType(_vm.IsElevated, typeof(bool));
        }

        // ── 辅助方法 ──────────────────────────────────────────────────

        /// <summary>同时写入 RecentUseds 和内部 _recentUseds</summary>
        private void PopulateCollection(params IRecentUsed[] items) {
            _userSettingsClient.Setup(u => u.RecentUseds)
                .Returns(new List<IRecentUsed>(items));
            _vm.InitCollection();
        }

        private static IRecentUsed MakeRecentUsed(
            string? fileName,
            string filePath) {
            var mock = new Mock<IRecentUsed>();
            mock.Setup(r => r.FileName).Returns(fileName);
            mock.Setup(r => r.FilePath).Returns(filePath);
            return mock.Object;
        }

        /// <summary>RelayCommand&lt;T&gt; 内部为 async void，需等待一个 tick 让 Task 完成</summary>
        private async Task InvokeRemoveCommandAsync(IRecentUsed? item) {
            // RelayCommand<T>.Execute 触发 async void lambda
            // 用 Task.Yield() 让控制权交还事件循环，等待异步完成
            _vm.RemoveFromListCommand!.Execute(item);
            await Task.Yield();
        }
    }
}
