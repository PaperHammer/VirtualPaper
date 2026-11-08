using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UI.Test.Utils;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.Utils.Interfaces;
using VirtualPaper.WpSettingsPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_WpSettings {
    [TestClass]
    public class LibraryContentsViewModelTests {
        private Mock<IUserSettingsClient> _userSettingsClient = null!;
        private Mock<IWallpaperControlClient> _wpControlClient = null!;
        private Mock<IWallpaperIndexService> _wallpaperIndexService = null!;
        private Mock<IMonitorManagerClient> _monitorManagerClient = null!;
        private Mock<ISettings> _settings = null!;
        private LibraryContentsViewModel _vm = null!;
        private Mock<IStoragePicker> _storagePicker = null!;
        private Mock<IMonitor> _primaryMonitor = null!;

        [TestInitialize]
        public void Setup() {
            CrossThreadInvoker.Initialize(new TUiSynchronizationContext());
            _userSettingsClient = new Mock<IUserSettingsClient>();
            _wpControlClient = new Mock<IWallpaperControlClient>();
            _wallpaperIndexService = new Mock<IWallpaperIndexService>();
            _settings = new Mock<ISettings>();

            _settings.SetupProperty(s => s.WallpaperDir, @"C:\Wallpapers");
            _settings.SetupProperty(s => s.BuiltInWallpapersDir, @"C:\BuiltInWallpapers");
            _userSettingsClient.Setup(u => u.Settings).Returns(_settings.Object);
            
            SetupWpSettings();

            _vm = new LibraryContentsViewModel(
                _userSettingsClient.Object,
                _wpControlClient.Object,
                CreateVm(),
                _wallpaperIndexService.Object);
        }

        private void SetupWpSettings() {
            _monitorManagerClient = new Mock<IMonitorManagerClient>();
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

        // ── FilterByTitle ─────────────────────────────────────────────

        [TestMethod]
        public void FilterByTitle_WhenKeywordMatches_ShowsMatchingItems() {
            var match = MakeWpData("uid-1", "Nature Wallpaper");
            var noMatch = MakeWpData("uid-2", "City Night");
            PopulateLibrary(match, noMatch);

            _vm.FilterByTitle("Nature");

            Assert.HasCount(1, _vm.LibraryWallpapers);
            Assert.AreSame(match, _vm.LibraryWallpapers[0]);
        }

        [TestMethod]
        public void FilterByTitle_WhenKeywordEmpty_ShowsAllItems() {
            var a = MakeWpData("uid-1", "Alpha");
            var b = MakeWpData("uid-2", "Beta");
            PopulateLibrary(a, b);

            _vm.FilterByTitle(string.Empty);

            Assert.HasCount(2, _vm.LibraryWallpapers);
        }

        [TestMethod]
        public void FilterByTitle_IsCaseInsensitive() {
            var item = MakeWpData("uid-1", "Sunset");
            PopulateLibrary(item);

            _vm.FilterByTitle("sunset");

            Assert.HasCount(1, _vm.LibraryWallpapers);
        }

        [TestMethod]
        public void FilterByTitle_WhenNoMatch_LibraryWallpapersIsEmpty() {
            var item = MakeWpData("uid-1", "Forest");
            PopulateLibrary(item);

            _vm.FilterByTitle("Ocean");

            Assert.HasCount(0, _vm.LibraryWallpapers);
        }

        [TestMethod]
        public void FilterByTitle_WhenCalledTwice_SecondFilterOverridesFirst() {
            var a = MakeWpData("uid-1", "Mountain");
            var b = MakeWpData("uid-2", "Desert");
            PopulateLibrary(a, b);

            _vm.FilterByTitle("Mountain");
            _vm.FilterByTitle("Desert");

            Assert.HasCount(1, _vm.LibraryWallpapers);
            Assert.AreSame(b, _vm.LibraryWallpapers[0]);
        }

        // ── FilterKeyword ─────────────────────────────────────────────

        [TestMethod]
        public void FilterKeyword_DefaultValue_IsLibraryTitle() {
            Assert.AreEqual(FilterKey.LibraryTitle, _vm.FilterKeyword);
        }

        // ── ApplyFilter ───────────────────────────────────────────────

        [TestMethod]
        public void ApplyFilter_DelegatesToFilterByTitle() {
            var item = MakeWpData("uid-1", "Cherry Blossom");
            PopulateLibrary(item);

            _vm.ApplyFilter("Cherry");

            Assert.HasCount(1, _vm.LibraryWallpapers);
        }

        // ── HandleDelete ──────────────────────────────────────────────

        [TestMethod]
        public void HandleDelete_RemovesFromLibraryWallpapers() {
            var item = MakeWpData("uid-1", "Galaxy");
            PopulateLibrary(item);

            _vm.HandleDelete(item);

            Assert.HasCount(0, _vm.LibraryWallpapers);
        }

        [TestMethod]
        public void HandleDelete_CallsWallpaperIndexServiceRemove() {
            var item = MakeWpData("uid-1", "Galaxy");
            PopulateLibrary(item);

            _vm.HandleDelete(item);

            _wallpaperIndexService.Verify(s => s.Remove(item), Times.Once);
        }

        [TestMethod]
        public void HandleDelete_WhenItemNotExists_DoesNotThrow() {
            var item = MakeWpData("uid-99", "Ghost");

            // 不 populate，直接删除
            _vm.HandleDelete(item);

            _wallpaperIndexService.Verify(s => s.Remove(item), Times.Once);
        }

        [TestMethod]
        public void HandleDelete_MultipleItems_RemovesAllAndRaisesOneNotification() {
            var first = MakeWpData("uid-1", "First");
            var second = MakeWpData("uid-2", "Second");
            PopulateLibrary(first, second);
            int notifications = 0;
            _vm.ItemDeleted += (_, _) => notifications++;

            _vm.HandleDelete(new[] { first, second });

            Assert.HasCount(0, _vm.LibraryWallpapers);
            _wallpaperIndexService.Verify(s => s.Remove(first), Times.Once);
            _wallpaperIndexService.Verify(s => s.Remove(second), Times.Once);
            Assert.AreEqual(1, notifications);
        }

        [TestMethod]
        public void HandleDelete_MultipleItems_DeduplicatesByWallpaperUid() {
            var item = MakeWpData("uid-1", "First");
            var duplicate = MakeWpData("uid-1", "Duplicate instance");
            PopulateLibrary(item);

            _vm.HandleDelete(new[] { item, duplicate });

            _wallpaperIndexService.Verify(s => s.Remove(It.IsAny<IWpBasicData>()), Times.Once);
        }

        // ── UpdateLib (TryGetValue 命中) ──────────────────────────────

        [TestMethod]
        public void UpdateLib_WhenIndexExists_ReplacesItemAtIndex() {
            var original = MakeWpData("uid-1", "Old Title");
            PopulateLibrary(original);

            _wallpaperIndexService
                .Setup(s => s.TryGetValue("uid-1", out It.Ref<int>.IsAny))
                .Returns((string _, ref int idx) => { idx = 0; return true; });

            var updated = MakeWpData("uid-1", "New Title");
            _vm.UpdateLib(updated);

            Assert.AreEqual("New Title", _vm.LibraryWallpapers[0].Title);
            _wallpaperIndexService.Verify(s => s.Update(updated), Times.Once);
        }

        [TestMethod]
        public void UpdateLib_WhenIndexNotExists_InsertsAtFront() {
            _wallpaperIndexService
                .Setup(s => s.TryGetValue(It.IsAny<string>(), out It.Ref<int>.IsAny))
                .Returns((string _, ref int idx) => { idx = -1; return false; });

            var newItem = MakeWpData("uid-new", "Brand New");
            _vm.UpdateLib(newItem);

            Assert.HasCount(1, _vm.LibraryWallpapers);
            Assert.AreSame(newItem, _vm.LibraryWallpapers[0]);
            _wallpaperIndexService.Verify(s => s.Update(newItem), Times.Once);
        }

        // ── IsFileInPreview ───────────────────────────────────────────

        [TestMethod]
        public void IsFileInPreview_WhenNotInPreview_ReturnsFalse() {
            var data = MakeWpData("uid-1", "Test");

            var result = _vm.IsFileInPreview(data);

            Assert.IsFalse(result);
        }

        // ── IsFileInUseAsync ──────────────────────────────────────────

        [TestMethod]
        public async Task IsFileInUseAsync_WhenLayoutContainsFolderPath_ReturnsTrue() {
            var data = MakeWpData("uid-1", "Test", folderPath: @"C:\Wallpapers\uid-1");
            var layout = MakeWallpaperLayout(@"C:\Wallpapers\uid-1");

            _userSettingsClient.Setup(u => u.LoadAsync<List<IWallpaperLayout>>())
                .Returns(Task.CompletedTask);
            _userSettingsClient.Setup(u => u.WallpaperLayouts)
                .Returns(new List<IWallpaperLayout> { layout });

            var result = await _vm.IsFileInUseAsync(data);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task IsFileInUseAsync_WhenLayoutDoesNotContainFolderPath_ReturnsFalse() {
            var data = MakeWpData("uid-1", "Test", folderPath: @"C:\Wallpapers\uid-1");
            var layout = MakeWallpaperLayout(@"C:\Wallpapers\other");

            _userSettingsClient.Setup(u => u.LoadAsync<List<IWallpaperLayout>>())
                .Returns(Task.CompletedTask);
            _userSettingsClient.Setup(u => u.WallpaperLayouts)
                .Returns(new List<IWallpaperLayout> { layout });

            var result = await _vm.IsFileInUseAsync(data);

            Assert.IsFalse(result);
        }

        // ── LibLoadingStatus property ─────────────────────────────────

        [TestMethod]
        public void LibLoadingStatus_WhenSet_RaisesPropertyChanged() {
            bool raised = false;
            _vm.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(_vm.LibLoadingStatus)) raised = true;
            };

            _vm.LibLoadingStatus = LoadingStatus.Changing;

            Assert.IsTrue(raised);
        }

        // ── UpdateLib 在过滤状态下的行为 ──────────────────────────────

        [TestMethod]
        [Description("UpdateLib should still find and replace the item in the visible list even when a filter is active")]
        public void UpdateLib_WhenFilterActive_MatchingItem_ReplacesInFilteredView() {
            // Arrange: populate two items, then apply a filter that only shows uid-1
            var original = MakeWpData("uid-1", "Nature Wallpaper");
            var other    = MakeWpData("uid-2", "City Night");
            PopulateLibrary(original, other);
            _vm.FilterByTitle("Nature"); // only uid-1 visible

            _wallpaperIndexService
                .Setup(s => s.TryGetValue("uid-1", out It.Ref<int>.IsAny))
                .Returns((string _, ref int idx) => { idx = 0; return true; });

            var updated = MakeWpData("uid-1", "Nature Wallpaper v2");

            // Act
            _vm.UpdateLib(updated);

            // Assert: the visible item should be the updated one
            Assert.AreEqual("Nature Wallpaper v2", _vm.LibraryWallpapers[0].Title,
                "UpdateLib should update the item in the filtered visible list");
            _wallpaperIndexService.Verify(s => s.Update(updated), Times.Once);
        }

        [TestMethod]
        [Description("UpdateLib should insert a new item at front even when filter hides existing items")]
        public void UpdateLib_WhenFilterActive_NewItem_InsertsAtFront() {
            // Arrange: populate one item and filter it out
            var existing = MakeWpData("uid-1", "Forest");
            PopulateLibrary(existing);
            _vm.FilterByTitle("Ocean"); // no matches → empty view

            _wallpaperIndexService
                .Setup(s => s.TryGetValue(It.IsAny<string>(), out It.Ref<int>.IsAny))
                .Returns((string _, ref int idx) => { idx = -1; return false; });

            var newItem = MakeWpData("uid-new", "Ocean Breeze");

            // Act
            _vm.UpdateLib(newItem);

            // Assert: new item inserted at front regardless of filter state
            Assert.IsTrue(_vm.LibraryWallpapers.Any(w => w.WallpaperUid == "uid-new"),
                "New item should be inserted into the visible list even when filter is active");
            _wallpaperIndexService.Verify(s => s.Update(newItem), Times.Once);
        }

        [TestMethod]
        [Description("Calling UpdateLib twice with the same uid should not duplicate the item")]
        public void UpdateLib_CalledTwice_SameUid_ShouldNotDuplicate() {
            var item = MakeWpData("uid-1", "Sunset v1");
            PopulateLibrary(item);
            _wallpaperIndexService
                .Setup(s => s.TryGetValue("uid-1", out It.Ref<int>.IsAny))
                .Returns((string _, ref int idx) => { idx = 0; return true; });

            var v2 = MakeWpData("uid-1", "Sunset v2");
            var v3 = MakeWpData("uid-1", "Sunset v3");

            _vm.UpdateLib(v2);
            _vm.UpdateLib(v3);

            Assert.HasCount(1, _vm.LibraryWallpapers,
                "Updating the same uid twice must not duplicate the item in the list");
            Assert.AreEqual("Sunset v3", _vm.LibraryWallpapers[0].Title);
        }

        // ── ApplyFilter(FilterContext) 组合过滤 ───────────────────────

        [TestMethod]
        public void ApplyFilter_WithActiveTypes_OnlyShowsMatchingTypes() {
            var img = MakeWpData("uid-1", "Alpha", ftype: FileType.FImage);
            var vid = MakeWpData("uid-2", "Beta", ftype: FileType.FVideo);
            var gif = MakeWpData("uid-3", "Gamma", ftype: FileType.FGif);
            PopulateLibrary(img, vid, gif);

            IReadOnlySet<FileType> types = new HashSet<FileType> { FileType.FImage };
            _vm.ApplyFilter(new FilterContext { ActiveTypes = types });

            Assert.HasCount(1, _vm.LibraryWallpapers);
            Assert.AreSame(img, _vm.LibraryWallpapers[0]);
        }

        [TestMethod]
        public void ApplyFilter_WithNullActiveTypes_ShowsAllItems() {
            var img = MakeWpData("uid-1", "Alpha", ftype: FileType.FImage);
            var vid = MakeWpData("uid-2", "Beta", ftype: FileType.FVideo);
            PopulateLibrary(img, vid);

            _vm.ApplyFilter(new FilterContext { ActiveTypes = null });

            Assert.HasCount(2, _vm.LibraryWallpapers);
        }

        [TestMethod]
        public void ApplyFilter_WithTitleAndType_BothConditionsApplied() {
            var matchImg = MakeWpData("uid-1", "Nature Sky", ftype: FileType.FImage);
            var matchVid = MakeWpData("uid-2", "Nature River", ftype: FileType.FVideo);
            var noMatchImg = MakeWpData("uid-3", "City Night", ftype: FileType.FImage);
            PopulateLibrary(matchImg, matchVid, noMatchImg);

            IReadOnlySet<FileType> types = new HashSet<FileType> { FileType.FImage };
            _vm.ApplyFilter(new FilterContext { TitleKeyword = "Nature", ActiveTypes = types });

            Assert.HasCount(1, _vm.LibraryWallpapers);
            Assert.AreSame(matchImg, _vm.LibraryWallpapers[0]);
        }

        [TestMethod]
        public void ApplyFilter_WithMultipleActiveTypes_ShowsAllMatching() {
            var img = MakeWpData("uid-1", "A", ftype: FileType.FImage);
            var gif = MakeWpData("uid-2", "B", ftype: FileType.FGif);
            var vid = MakeWpData("uid-3", "C", ftype: FileType.FVideo);
            PopulateLibrary(img, gif, vid);

            IReadOnlySet<FileType> types = new HashSet<FileType> { FileType.FImage, FileType.FGif };
            _vm.ApplyFilter(new FilterContext { ActiveTypes = types });

            Assert.HasCount(2, _vm.LibraryWallpapers);
        }

        [TestMethod]
        public void ApplyFilter_EmptyContext_ShowsAllItems() {
            var a = MakeWpData("uid-1", "Alpha");
            var b = MakeWpData("uid-2", "Beta");
            PopulateLibrary(a, b);

            _vm.ApplyFilter(FilterContext.Empty);

            Assert.HasCount(2, _vm.LibraryWallpapers);
        }

        // ── 辅助方法 ──────────────────────────────────────────────────

        [TestMethod]
        public async Task FullLibrarySearch_PaginatesMatchesBeyondInitiallyLoadedItems() {
            string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.Tests", "LibraryPages-" + Guid.NewGuid().ToString("N"))).FullName;
            try {
                var index = new WallpaperIndexService(Path.Combine(root, "preferences.json"));
                ReadyIndex();
                _wallpaperIndexService.Setup(service => service.Query(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<FilterContext>()))
                    .Returns((int offset, int limit, FilterContext filter) => index.Query(offset, limit, filter));
                for (int i = 0; i < 66; i++) {
                    string folder = Directory.CreateDirectory(Path.Combine(root, "wallpaper-" + i)).FullName;
                    var data = MakeWpData("uid-" + i, i < 31 ? "Forest" : "Ocean", folder);
                    data.CreatedTime = new DateTime(2026, 1, 1).AddMinutes(-i);
                    await data.SaveMetadataAsync();
                    index.Update(data);
                }

                await _vm.ApplyFilterAsync(FilterContext.Empty);
                Assert.HasCount(30, _vm.LibraryWallpapers);
                Assert.IsTrue(_vm.LibraryWallpapers.All(item => item.Title == "Forest"));
                await _vm.ApplyFilterAsync(new FilterContext { TitleKeyword = "Ocean" });
                Assert.HasCount(30, _vm.LibraryWallpapers);
                await _vm.LoadMorePageAsync();
                Assert.HasCount(35, _vm.LibraryWallpapers);
                Assert.IsTrue(_vm.LibraryWallpapers.All(item => item.Title == "Ocean"));
                Assert.HasCount(35, _vm.LibraryWallpapers.Select(item => item.WallpaperUid).Distinct().ToArray());
                await _vm.LoadMorePageAsync();
                Assert.HasCount(35, _vm.LibraryWallpapers);
                await _vm.ApplyFilterAsync(FilterContext.Empty);
                Assert.HasCount(30, _vm.LibraryWallpapers);
                Assert.IsTrue(_vm.LibraryWallpapers.All(item => item.Title == "Forest"));
            }
            finally { Directory.Delete(root, true); }
        }

        [TestMethod]
        public async Task MissingMetadata_ConsumesOffsetsAndDoesNotHideLaterValidItems() {
            string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.Tests", "MissingPages-" + Guid.NewGuid().ToString("N"))).FullName;
            try {
                ReadyIndex();
                var entries = Enumerable.Range(0, 31).Select(i => new WallpaperIndexEntry {
                    Uid = "missing-" + i, JsonPath = Path.Combine(root, "missing-" + i + ".json")
                }).ToList();
                var valid = MakeWpData("valid", "Available", root);
                await valid.SaveMetadataAsync();
                entries.Add(WallpaperIndexEntry.FromData(valid));
                _wallpaperIndexService.Setup(service => service.Query(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<FilterContext>()))
                    .Returns((int offset, int limit, FilterContext _) => entries.Skip(offset).Take(limit).ToArray());

                await _vm.ApplyFilterAsync(FilterContext.Empty);

                Assert.HasCount(1, _vm.LibraryWallpapers);
                Assert.AreEqual("valid", _vm.LibraryWallpapers[0].WallpaperUid);
                _wallpaperIndexService.Verify(service => service.Query(30, 30, It.IsAny<FilterContext>()), Times.Once);
                await _vm.LoadMorePageAsync();
                Assert.HasCount(1, _vm.LibraryWallpapers);
            }
            finally { Directory.Delete(root, true); }
        }

        [TestMethod]
        public async Task NewFilter_SupersedesQueryWaitingForInitialization() {
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _wallpaperIndexService.SetupGet(service => service.Initialized).Returns(ready);
            var forest = MakeWpData("forest", "Forest");
            var ocean = MakeWpData("ocean", "Ocean");
            PopulateLibrary(forest, ocean);
            _wallpaperIndexService.Setup(service => service.Query(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<FilterContext>()))
                .Returns((int offset, int limit, FilterContext filter) => new[] { forest, ocean }
                    .Where(item => item.Title == filter.TitleKeyword).Select(WallpaperIndexEntry.FromData).Skip(offset).Take(limit).ToArray());

            Task oldQuery = _vm.ApplyFilterAsync(new FilterContext { TitleKeyword = "Forest" });
            Task newQuery = _vm.ApplyFilterAsync(new FilterContext { TitleKeyword = "Ocean" });
            ready.SetResult(true);
            await Task.WhenAll(oldQuery, newQuery);

            Assert.HasCount(1, _vm.LibraryWallpapers);
            Assert.AreSame(ocean, _vm.LibraryWallpapers[0]);
            _wallpaperIndexService.Verify(service => service.Query(It.IsAny<int>(), It.IsAny<int>(),
                It.Is<FilterContext>(filter => filter.TitleKeyword == "Forest")), Times.Never);
        }

        [TestMethod]
        public void UpdateLib_NonMatchingImportedItem_RemainsHiddenUntilFiltersClear() {
            _vm.ApplyFilter(new FilterContext { TitleKeyword = "Ocean" });
            var forest = MakeWpData("forest", "Forest");
            _vm.UpdateLib(forest);
            Assert.IsEmpty(_vm.LibraryWallpapers);
            _vm.ApplyFilter(FilterContext.Empty);
            Assert.AreSame(forest, _vm.LibraryWallpapers.Single());
        }

        private void ReadyIndex() {
            var ready = new TaskCompletionSource<bool>(); ready.SetResult(true);
            _wallpaperIndexService.SetupGet(service => service.Initialized).Returns(ready);
        }

        /// <summary>将数据同时写入 LibraryWallpapers 和内部 _libraryWallpapers</summary>
        [TestMethod]
        [DataRow(false, false, true, false)]
        [DataRow(true, true, false, true)]
        [DataRow(false, true, true, true)]
        [DataRow(true, false, true, true)]
        public void FavoriteActions_ReflectAllActualTargets(bool first, bool second, bool canAdd, bool canRemove) {
            var a = MakeWpData("a", "A");
            var b = MakeWpData("b", "B");
            _wallpaperIndexService.Setup(service => service.GetOrganization("a")).Returns(new WallpaperOrganization { Favorite = first });
            _wallpaperIndexService.Setup(service => service.GetOrganization("b")).Returns(new WallpaperOrganization { Favorite = second });

            Assert.AreEqual((canAdd, canRemove), _vm.FavoriteActions([a, b]));
            Assert.AreEqual(first, _vm.IsFavorite(a));
            Assert.AreEqual(second, _vm.IsFavorite(b));
            Assert.AreEqual((false, false), _vm.FavoriteActions([]));
        }

        [TestMethod]
        public void ContextItems_RightClickUsesSelectedGroupOnlyWhenTargetIsSelected() {
            var a = MakeWpData("a", "A");
            var b = MakeWpData("b", "B");
            var c = MakeWpData("c", "C");
            CollectionAssert.AreEqual(new IWpBasicData[] { a, b }, LibraryContentsViewModel.ContextItems(a, [a, b]).ToArray());
            CollectionAssert.AreEqual(new IWpBasicData[] { c }, LibraryContentsViewModel.ContextItems(c, [a, b]).ToArray());
            CollectionAssert.AreEqual(new IWpBasicData[] { c }, LibraryContentsViewModel.ContextItems(c, []).ToArray());
            // A refreshed card with the same UID still targets the original selection.
            CollectionAssert.AreEqual(new IWpBasicData[] { a, b }, LibraryContentsViewModel.ContextItems(MakeWpData("A", "Refreshed"), [a, b]).ToArray());
        }

        [TestMethod]
        public async Task SaveDetails_PersistsMetadataAndSeparatePreferencesAndRefreshesSearch() {
            string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.Tests", "LibraryEdit-" + Guid.NewGuid().ToString("N"))).FullName;
            try {
                var index = ConnectOrganizationIndex(Path.Combine(root, "preferences.json"));
                var data = MakeWpData("edit", "Old title", Directory.CreateDirectory(Path.Combine(root, "item")).FullName);
                data.Tags = "Original";
                await data.SaveMetadataAsync();
                index.Update(data);
                await index.UpdateOrganizationAsync([data.WallpaperUid], favorite: true);
                PopulateLibrary(data);
                _vm.ApplyFilter(new FilterContext { TitleKeyword = "New title" });
                Assert.IsEmpty(_vm.LibraryWallpapers);
                int changed = 0;
                _vm.OrganizationChanged += (_, _) => changed++;

                var authors = new WallpaperInfoTokens(["Author"], splitCommas: false);
                await _vm.SaveDetailsAsync(data, new("New title", "New description", authors.Serialize("Second author"), "Mine, mine;Calm"));

                var saved = WpBasicData.Load(Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName));
                Assert.AreEqual("New title", saved.Title);
                Assert.AreEqual("New description", saved.Desc);
                Assert.AreEqual("Author;Second author", saved.Authors);
                Assert.AreEqual("Original", saved.Tags);
                CollectionAssert.AreEqual(new IWpBasicData[] { data }, _vm.LibraryWallpapers.ToArray());
                Assert.HasCount(1, index.Query(0, 10, new FilterContext { TitleKeyword = "New title" }));
                Assert.HasCount(1, index.Query(0, 10, new FilterContext { TitleKeyword = "Second author" }));
                Assert.IsTrue(_vm.IsFavorite(data));
                Assert.AreEqual((false, true), _vm.FavoriteActions([data]));
                Assert.AreEqual(1, changed);
                var restarted = new WallpaperIndexService(Path.Combine(root, "preferences.json"));
                CollectionAssert.AreEquivalent(new[] { "Mine", "Calm" }, restarted.GetOrganization("edit").Tags);
                Assert.IsTrue(restarted.GetOrganization("edit").Favorite);

                byte[] before = File.ReadAllBytes(Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName));
                // A card/menu change made while the info form is open must not be overwritten.
                await index.UpdateOrganizationAsync([data.WallpaperUid], favorite: false);
                await _vm.SaveDetailsAsync(data, new(data.Title, data.Desc, data.Authors, "Personal only"));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName)));
                Assert.IsFalse(_vm.IsFavorite(data));
                Assert.AreEqual((true, false), _vm.FavoriteActions([data]));
            }
            finally { Directory.Delete(root, true); }
        }

        [TestMethod]
        public async Task SaveDetails_FailedMetadataSaveDoesNotPublishDraftOrChangeFavorites() {
            var data = new Mock<IWpBasicData>();
            data.SetupAllProperties();
            data.Object.WallpaperUid = "edit";
            data.Object.Title = "Original";
            data.Object.Desc = "Original description";
            data.Object.Tags = "Original tag";
            data.Object.AppInfo = new() { FileVersion = "18" };
            var draft = new Mock<IWpBasicData>();
            draft.SetupAllProperties();
            draft.Setup(item => item.SaveAsync()).ThrowsAsync(new IOException("Cannot save metadata"));
            data.Setup(item => item.Clone()).Returns(draft.Object);

            await Assert.ThrowsAsync<IOException>(() => _vm.SaveDetailsAsync(data.Object, new("Changed", "Changed", "Changed", "Personal")));

            Assert.AreEqual("Original", data.Object.Title);
            Assert.AreEqual("Original description", data.Object.Desc);
            Assert.AreEqual("Original tag", data.Object.Tags);
            Assert.AreEqual("18", data.Object.AppInfo.FileVersion);
            _wallpaperIndexService.Verify(service => service.Update(It.IsAny<IWpBasicData>()), Times.Never);
            _wallpaperIndexService.Verify(service => service.UpdateOrganizationAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<WallpaperTagUpdate>()), Times.Never);
        }

        [TestMethod]
        public async Task SaveDetails_FailedPreferencesSaveKeepsPersistedMetadataSearchableAndCanRetry() {
            string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.Tests", "LibraryEditRetry-" + Guid.NewGuid().ToString("N"))).FullName;
            try {
                var index = ConnectOrganizationIndex(Path.Combine(root, "preferences.json"));
                var data = MakeWpData("edit", "Original", Directory.CreateDirectory(Path.Combine(root, "item")).FullName);
                PopulateLibrary(data);
                _vm.ApplyFilter(new FilterContext { TitleKeyword = "Changed" });
                var edit = new WallpaperDetailsEdit("Changed", "", "", "Personal");
                _wallpaperIndexService.Setup(service => service.UpdateOrganizationAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<WallpaperTagUpdate>()))
                    .ThrowsAsync(new IOException("Cannot save preferences"));

                await Assert.ThrowsAsync<IOException>(() => _vm.SaveDetailsAsync(data, edit));

                Assert.AreEqual("Changed", WpBasicData.Load(Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName)).Title);
                Assert.HasCount(1, index.Query(0, 10, new FilterContext { TitleKeyword = "Changed" }));
                Assert.HasCount(1, _vm.LibraryWallpapers);
                Assert.IsFalse(_vm.IsFavorite(data));
                _wallpaperIndexService.Setup(service => service.UpdateOrganizationAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<WallpaperTagUpdate>()))
                    .Returns((IEnumerable<string> uids, bool? favorite, IEnumerable<string>? tags, WallpaperTagUpdate mode) => index.UpdateOrganizationAsync(uids, favorite, tags, mode));
                await _vm.SaveDetailsAsync(data, edit);
                Assert.IsFalse(_vm.IsFavorite(data));
                CollectionAssert.AreEqual(new[] { "Personal" }, index.GetOrganization(data.WallpaperUid).Tags);
                Assert.HasCount(1, index.Query(0, 10, new FilterContext { TitleKeyword = "Changed" }));
            }
            finally { Directory.Delete(root, true); }
        }

        private WallpaperIndexService ConnectOrganizationIndex(string statePath) {
            var index = new WallpaperIndexService(statePath);
            _wallpaperIndexService.Setup(service => service.GetOrganization(It.IsAny<string>())).Returns((string uid) => index.GetOrganization(uid));
            _wallpaperIndexService.Setup(service => service.GetTags()).Returns(() => index.GetTags());
            _wallpaperIndexService.Setup(service => service.Update(It.IsAny<IWpBasicData>())).Callback((IWpBasicData data) => index.Update(data));
            _wallpaperIndexService.Setup(service => service.UpdateOrganizationAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<WallpaperTagUpdate>()))
                .Returns((IEnumerable<string> uids, bool? favorite, IEnumerable<string>? tags, WallpaperTagUpdate mode) => index.UpdateOrganizationAsync(uids, favorite, tags, mode));
            return index;
        }

        private void PopulateLibrary(params IWpBasicData[] items) {
            _vm.TestPopulate(items);
        }

        private static WpBasicData MakeWpData(
            string uid,
            string title,
            string folderPath = @"C:\Wallpapers\default",
            FileType ftype = FileType.FImage) {
            return new WpBasicData {
                WallpaperUid = uid,
                Title = title,
                FolderPath = folderPath,
                FilePath = $"C:\\Wallpapers\\{uid}\\file.png",
                ThumbnailPath = $"C:\\Wallpapers\\{uid}\\thumb.png",
                AppInfo = new ApplicationInfo { AppVersion = "1.0" },
                FType = ftype,
            };
        }

        private static IWallpaperLayout MakeWallpaperLayout(string folderPath) {
            var mock = new Mock<IWallpaperLayout>();
            mock.Setup(l => l.FolderPath).Returns(folderPath);
            return mock.Object;
        }
    }
}
