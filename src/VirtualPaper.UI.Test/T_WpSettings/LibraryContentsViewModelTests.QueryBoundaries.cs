using Moq;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_WpSettings;

public partial class LibraryContentsViewModelTests {
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedFilter_PagingRetriesItsFirstPageWithoutMixingOldCards(bool oldResultsExhausted) {
        int forestCount = oldResultsExhausted ? 2 : 31;
        var items = Enumerable.Range(0, forestCount + 31)
            .Select(i => MakeWpData("uid-" + i, i < forestCount ? "Forest" : "Ocean")).ToArray();
        ConfigureCachedPages(items);
        bool fail = true;
        _wallpaperIndexService.Setup(index => index.Query(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<FilterContext>()))
            .Returns((int offset, int limit, FilterContext filter) => {
                if (fail && filter.TitleKeyword == "Ocean") throw new IOException("Index temporarily unavailable");
                return Page(items, offset, limit, filter);
            });
        await _vm.ApplyFilterAsync(new FilterContext { TitleKeyword = "Forest" });
        var previous = _vm.LibraryWallpapers.ToArray();
        Assert.AreEqual(oldResultsExhausted, _vm.EndOfResults);
        int resets = 0;
        _vm.QueryReset += (_, _) => resets++;

        await _vm.ApplyFilterAsync(new FilterContext { TitleKeyword = "Ocean" });

        CollectionAssert.AreEqual(previous, _vm.LibraryWallpapers.ToArray());
        Assert.AreEqual(LoadingStatus.Ready, _vm.LibLoadingStatus);
        Assert.AreEqual(0, resets);
        fail = false;
        await _vm.LoadMorePageAsync();

        CollectionAssert.AreEqual(items.Skip(forestCount).Take(30).ToArray(), _vm.LibraryWallpapers.ToArray());
        Assert.AreEqual(1, resets);
        _wallpaperIndexService.Verify(index => index.Query(0, 30,
            It.Is<FilterContext>(filter => filter.TitleKeyword == "Ocean")), Times.Exactly(2));
        _wallpaperIndexService.Verify(index => index.Query(30, 30,
            It.Is<FilterContext>(filter => filter.TitleKeyword == "Ocean")), Times.Never);

        await _vm.LoadMorePageAsync();
        CollectionAssert.AreEqual(items.Skip(forestCount).ToArray(), _vm.LibraryWallpapers.ToArray());
        Assert.IsTrue(_vm.EndOfResults);
        Assert.AreEqual(1, resets);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedPage_KeepsVisibleCardsAndOffset_AndRetriesWithoutDuplicates(bool partialPage) {
        var items = Enumerable.Range(0, 61).Select(i => MakeWpData("uid-" + i, "Item " + i)).ToArray();
        ConfigureCachedPages(items);
        bool fail = false;
        string missingRoot = Path.Combine(Path.GetTempPath(), "VirtualPaper.MissingMetadata-" + Guid.NewGuid().ToString("N"));
        _wallpaperIndexService.Setup(index => index.Query(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<FilterContext>()))
            .Returns((int offset, int limit, FilterContext filter) => {
                if (fail && offset >= 30) {
                    if (partialPage && offset == 30) return new[] { WallpaperIndexEntry.FromData(items[30]) }
                        .Concat(Enumerable.Range(0, 29).Select(i => new WallpaperIndexEntry {
                            Uid = "missing-" + i, JsonPath = Path.Combine(missingRoot, i + ".json")
                        })).ToArray();
                    throw new IOException("Page query failed");
                }
                return Page(items, offset, limit, filter);
            });
        await _vm.ApplyFilterAsync(FilterContext.Empty);
        var previous = _vm.LibraryWallpapers.ToArray();
        int resets = 0;
        _vm.QueryReset += (_, _) => resets++;
        _wallpaperIndexService.Invocations.Clear();

        fail = true;
        await _vm.LoadMorePageAsync();

        CollectionAssert.AreEqual(previous, _vm.LibraryWallpapers.ToArray());
        Assert.AreEqual(LoadingStatus.Ready, _vm.LibLoadingStatus);
        Assert.IsFalse(_vm.EndOfResults);
        Assert.AreEqual(0, resets);
        fail = false;
        await _vm.LoadMorePageAsync();

        CollectionAssert.AreEqual(items.Take(60).ToArray(), _vm.LibraryWallpapers.ToArray());
        _wallpaperIndexService.Verify(index => index.Query(30, 30, FilterContext.Empty), Times.Exactly(2));
        _wallpaperIndexService.Verify(index => index.Query(0, It.IsAny<int>(), It.IsAny<FilterContext>()), Times.Never);
        await _vm.LoadMorePageAsync();
        CollectionAssert.AreEqual(items, _vm.LibraryWallpapers.ToArray());
        Assert.HasCount(61, _vm.LibraryWallpapers.Select(item => item.WallpaperUid).Distinct().ToArray());
        Assert.IsTrue(_vm.EndOfResults);
        Assert.AreEqual(0, resets);
    }

    [TestMethod]
    public async Task LoadingFilter_IgnoresRepeatedPagingRequests_AndCanPageAfterInitialization() {
        var items = Enumerable.Range(0, 61).Select(i => MakeWpData("uid-" + i, "Item " + i)).ToArray();
        ConfigureCachedPages(items);
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _wallpaperIndexService.SetupGet(index => index.Initialized).Returns(ready);
        Task loading = _vm.ApplyFilterAsync(FilterContext.Empty);
        try {
            Assert.IsFalse(loading.IsCompleted);
            Task first = _vm.LoadMorePageAsync();
            Task second = _vm.LoadMorePageAsync();
            Assert.IsTrue(first.IsCompletedSuccessfully);
            Assert.IsTrue(second.IsCompletedSuccessfully);
            _wallpaperIndexService.Verify(index => index.Query(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<FilterContext>()), Times.Never);
            ready.SetResult(true);
            await loading.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

            CollectionAssert.AreEqual(items.Take(30).ToArray(), _vm.LibraryWallpapers.ToArray());
            await _vm.LoadMorePageAsync();
            CollectionAssert.AreEqual(items.Take(60).ToArray(), _vm.LibraryWallpapers.ToArray());
            _wallpaperIndexService.Verify(index => index.Query(0, 30, FilterContext.Empty), Times.Once);
            _wallpaperIndexService.Verify(index => index.Query(30, 30, FilterContext.Empty), Times.Once);
        }
        finally { ready.TrySetResult(true); await loading; }
    }

    private void ConfigureCachedPages(WpBasicData[] items) {
        ReadyIndex();
        PopulateLibrary(items);
        _wallpaperIndexService.Setup(index => index.Query(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<FilterContext>()))
            .Returns((int offset, int limit, FilterContext filter) => Page(items, offset, limit, filter));
    }

    // Fixture ordering is fixed; filtering/sorting itself has its own query-service tests.
    private static WallpaperIndexEntry[] Page(IEnumerable<WpBasicData> items, int offset, int limit, FilterContext filter) => items
        .Where(item => filter.TitleKeyword.Length == 0 || item.Title == filter.TitleKeyword)
        .Skip(offset).Take(limit).Select(WallpaperIndexEntry.FromData).ToArray();
}
