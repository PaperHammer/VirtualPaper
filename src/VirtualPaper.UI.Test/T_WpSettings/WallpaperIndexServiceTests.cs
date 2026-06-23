using Moq;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.WpSettingsPanel.Utils;

namespace VirtualPaper.UI.Test.T_WpSettings {
    [TestClass]
    public class WallpaperIndexServiceTests {
        private WallpaperIndexService _service = null!;

        [TestInitialize]
        public void Setup() {
            _service = new WallpaperIndexService();
        }

        private static Mock<IWpBasicData> MakeWpData(string uid, string title, string folderPath = @"C:\wp\test") {
            var mock = new Mock<IWpBasicData>();
            mock.Setup(d => d.WallpaperUid).Returns(uid);
            mock.Setup(d => d.Title).Returns(title);
            mock.Setup(d => d.FolderPath).Returns(folderPath);
            mock.Setup(d => d.Authors).Returns("author");
            mock.Setup(d => d.Tags).Returns("tag1;tag2");
            mock.Setup(d => d.CreatedTime).Returns(DateTime.Now);
            mock.Setup(d => d.IsAvailable()).Returns(true);
            return mock;
        }

        // ── Update ────────────────────────────────────────────────────

        [TestMethod]
        public void Update_NewEntry_AddsToIndex() {
            var data = MakeWpData("uid-1", "Wallpaper A");

            _service.Update(data.Object);

            Assert.IsTrue(_service.TryGetValue("uid-1", out _));
        }

        [TestMethod]
        public void Update_ExistingEntry_UpdatesInPlace() {
            var data = MakeWpData("uid-1", "Old Title");
            _service.Update(data.Object);

            var updated = MakeWpData("uid-1", "New Title");
            _service.Update(updated.Object);

            Assert.IsTrue(_service.TryGetValue("uid-1", out var idx));
            var entries = _service.Query(0, 100);
            Assert.AreEqual("New Title", entries[idx].Title);
        }

        [TestMethod]
        public void Update_NullData_NoOp() {
            _service.Update(null!);
            var entries = _service.Query(0, 100);
            Assert.IsEmpty(entries);
        }

        [TestMethod]
        public void Update_UnavailableData_NoOp() {
            var data = MakeWpData("uid-1", "Test");
            data.Setup(d => d.IsAvailable()).Returns(false);

            _service.Update(data.Object);

            Assert.IsFalse(_service.TryGetValue("uid-1", out _));
        }

        // ── Remove ────────────────────────────────────────────────────

        [TestMethod]
        public void Remove_ExistingEntry_RemovesFromIndex() {
            var data = MakeWpData("uid-1", "Test");
            _service.Update(data.Object);

            _service.Remove(data.Object);

            Assert.IsFalse(_service.TryGetValue("uid-1", out _));
        }

        [TestMethod]
        public void Remove_NonExistentEntry_NoOp() {
            var data = MakeWpData("uid-1", "Test");
            _service.Update(data.Object);

            var other = MakeWpData("uid-2", "Other");
            _service.Remove(other.Object);

            Assert.IsTrue(_service.TryGetValue("uid-1", out _));
        }

        [TestMethod]
        public void Remove_NullData_NoOp() {
            var data = MakeWpData("uid-1", "Test");
            _service.Update(data.Object);

            _service.Remove(null!);

            Assert.IsTrue(_service.TryGetValue("uid-1", out _));
        }

        // ── Query ─────────────────────────────────────────────────────

        [TestMethod]
        public void Query_EmptyIndex_ReturnsEmpty() {
            var result = _service.Query(0, 10);
            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void Query_WithOffsetAndLimit_ReturnsCorrectSlice() {
            for (int i = 0; i < 5; i++)
                _service.Update(MakeWpData($"uid-{i}", $"Title {i}").Object);

            var result = _service.Query(1, 2);

            Assert.HasCount(2, result);
        }

        [TestMethod]
        public void Query_LimitExceedsCount_ReturnsRemaining() {
            for (int i = 0; i < 3; i++)
                _service.Update(MakeWpData($"uid-{i}", $"Title {i}").Object);

            var result = _service.Query(0, 100);

            Assert.HasCount(3, result);
        }

        // ── TryGetValue ───────────────────────────────────────────────

        [TestMethod]
        public void TryGetValue_ExistingUid_ReturnsTrue() {
            _service.Update(MakeWpData("uid-1", "Test").Object);

            Assert.IsTrue(_service.TryGetValue("uid-1", out var idx));
            Assert.IsGreaterThanOrEqualTo(0, idx);
        }

        [TestMethod]
        public void TryGetValue_NonExistentUid_ReturnsFalse() {
            Assert.IsFalse(_service.TryGetValue("nonexistent", out _));
        }

        [TestMethod]
        public void TryGetValue_CaseInsensitive() {
            _service.Update(MakeWpData("UID-1", "Test").Object);

            Assert.IsTrue(_service.TryGetValue("uid-1", out _));
        }

        // ── Update → Remove → Query 综合 ──────────────────────────────

        [TestMethod]
        public void UpdateRemoveQuery_MultipleEntries_ConsistentState() {
            _service.Update(MakeWpData("a", "A").Object);
            _service.Update(MakeWpData("b", "B").Object);
            _service.Update(MakeWpData("c", "C").Object);

            _service.Remove(MakeWpData("b", "B").Object);

            var all = _service.Query(0, 100);
            Assert.HasCount(2, all);
            Assert.IsTrue(_service.TryGetValue("a", out _));
            Assert.IsFalse(_service.TryGetValue("b", out _));
            Assert.IsTrue(_service.TryGetValue("c", out _));
        }
    }
}
