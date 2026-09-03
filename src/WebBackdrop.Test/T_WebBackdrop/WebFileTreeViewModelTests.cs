using VirtualPaper.Common.Utils.ProjectSystem.Events;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.ViewModels;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
public class WebFileTreeViewModelTests {
    [TestInitialize]
    public void Initialize() {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "VirtualPaper.Tests",
            $"WebFileTree-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    [TestCleanup]
    public void Cleanup() {
        _viewModel?.CancelPendingSearch();
        if (Directory.Exists(_testRoot)) {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void RefreshAndToggleFolder_LoadsSortedTreeLazily() {
        string scripts = Directory.CreateDirectory(Path.Combine(_testRoot, "scripts")).FullName;
        File.WriteAllText(Path.Combine(scripts, "z.js"), "z");
        File.WriteAllText(Path.Combine(scripts, "a.js"), "a");
        File.WriteAllText(Path.Combine(_testRoot, "index.html"), "html");

        var viewModel = CreateViewModel();
        viewModel.Refresh(_testRoot);

        WebFileItem root = AssertSingle(viewModel.FileItems);
        Assert.IsTrue(root.IsExpanded);
        Assert.IsTrue(root.IsChildrenLoaded);
        CollectionAssert.AreEqual(
            new[] { "scripts", "index.html" },
            root.Children.Select(item => item.FileName).ToArray());

        WebFileItem folder = root.Children[0];
        Assert.IsFalse(folder.IsChildrenLoaded);
        Assert.IsTrue(AssertSingle(folder.Children).IsPlaceholder);

        viewModel.ToggleFolder(folder);

        Assert.IsTrue(folder.IsChildrenLoaded);
        CollectionAssert.AreEqual(
            new[] { "a.js", "z.js" },
            folder.Children.Select(item => item.FileName).ToArray());
    }

    [TestMethod]
    public async Task CreateRenameAndDelete_KeepDiskAndTreeInSync() {
        var viewModel = CreateViewModel();
        viewModel.Refresh(_testRoot);
        WebFileItem root = AssertSingle(viewModel.FileItems);

        string createdPath = Path.Combine(_testRoot, "draft.txt");
        await viewModel.CreateFileAsync(_testRoot, _ => Task.FromResult<string?>(createdPath));
        WebFileItem created = viewModel.FindItem(createdPath)!;

        Assert.IsNotNull(created);
        Assert.IsTrue(File.Exists(createdPath));

        viewModel.BeginRename(created);
        await viewModel.RenameToAsync(created, "published.txt");

        string renamedPath = Path.Combine(_testRoot, "published.txt");
        Assert.IsFalse(File.Exists(createdPath));
        Assert.IsTrue(File.Exists(renamedPath));
        Assert.AreEqual(renamedPath, created.FilePath);
        Assert.AreSame(created, viewModel.FindItem(renamedPath));

        viewModel.Delete(created);

        Assert.IsFalse(File.Exists(renamedPath));
        Assert.IsNull(viewModel.FindItem(renamedPath));
        Assert.IsEmpty(root.Children);
    }

    [TestMethod]
    public void CopyAndCutPaste_UseDistinctDestinationsAndClearCutState() {
        string sourceFolder = Directory.CreateDirectory(Path.Combine(_testRoot, "source")).FullName;
        string targetFolder = Directory.CreateDirectory(Path.Combine(_testRoot, "target")).FullName;
        string sourcePath = Path.Combine(sourceFolder, "app.js");
        File.WriteAllText(sourcePath, "content");

        var viewModel = CreateViewModel();
        viewModel.Refresh(_testRoot);
        WebFileItem source = LoadChild(viewModel, sourceFolder, sourcePath);
        WebFileItem target = viewModel.FindItem(targetFolder)!;

        viewModel.Copy(source);
        Assert.IsTrue(viewModel.CanPasteTo(target));
        Assert.IsTrue(viewModel.PasteTo(target));
        Assert.IsTrue(File.Exists(Path.Combine(targetFolder, "app.js")));
        Assert.IsTrue(File.Exists(sourcePath));

        viewModel.Cut(source);
        Assert.IsTrue(source.IsCut);
        Assert.IsTrue(viewModel.PasteTo(target));

        string movedPath = Path.Combine(targetFolder, "app(1).js");
        Assert.IsFalse(File.Exists(sourcePath));
        Assert.IsTrue(File.Exists(movedPath));
        Assert.AreEqual(movedPath, source.FilePath);
        Assert.IsFalse(source.IsCut);
        Assert.IsFalse(viewModel.HasCutItem);
    }

    [TestMethod]
    public void FolderCannotBeCutIntoItsOwnDescendant() {
        string parentPath = Directory.CreateDirectory(Path.Combine(_testRoot, "parent")).FullName;
        string childPath = Directory.CreateDirectory(Path.Combine(parentPath, "child")).FullName;

        var viewModel = CreateViewModel();
        viewModel.Refresh(_testRoot);
        WebFileItem parent = viewModel.FindItem(parentPath)!;
        viewModel.ToggleFolder(parent);
        WebFileItem child = viewModel.FindItem(childPath)!;

        viewModel.Cut(parent);

        Assert.IsFalse(viewModel.CanPasteTo(parent));
        Assert.IsFalse(viewModel.CanPasteTo(child));
        Assert.IsFalse(viewModel.PasteTo(child));
        Assert.IsTrue(Directory.Exists(parentPath));
    }

    [TestMethod]
    public void ApplyChange_CreatesRenamesAndDeletesNodesIncrementally() {
        var viewModel = CreateViewModel();
        viewModel.Refresh(_testRoot);

        string createdPath = Path.Combine(_testRoot, "created.css");
        File.WriteAllText(createdPath, "body{}");
        viewModel.ApplyChange(new ProjectChangedEvent {
            Type = ProjectChangeType.Created,
            Path = createdPath,
        });
        Assert.IsNotNull(viewModel.FindItem(createdPath));

        string renamedPath = Path.Combine(_testRoot, "renamed.css");
        File.Move(createdPath, renamedPath);
        viewModel.ApplyChange(new ProjectChangedEvent {
            Type = ProjectChangeType.Renamed,
            OldPath = createdPath,
            Path = renamedPath,
        });
        Assert.IsNull(viewModel.FindItem(createdPath));
        Assert.IsNotNull(viewModel.FindItem(renamedPath));

        File.Delete(renamedPath);
        viewModel.ApplyChange(new ProjectChangedEvent {
            Type = ProjectChangeType.Deleted,
            Path = renamedPath,
        });
        Assert.IsNull(viewModel.FindItem(renamedPath));
    }

    [TestMethod]
    public async Task ContentSearch_RespectsCaseAndWholeWordAndSkipsBinaryFiles() {
        File.WriteAllText(Path.Combine(_testRoot, "first.txt"), "alpha Alpha alphabet");
        File.WriteAllText(Path.Combine(_testRoot, "second.js"), "const Alpha = 1;\nalpha();");
        File.WriteAllBytes(Path.Combine(_testRoot, "ignored.png"), "Alpha"u8.ToArray());

        var viewModel = CreateViewModel();
        viewModel.Refresh(_testRoot);
        viewModel.IsCaseSensitive = true;
        viewModel.IsWholeWord = true;
        viewModel.FilterText = "Alpha";

        await WaitForSearchAsync(viewModel);

        Assert.HasCount(2, viewModel.SearchResults);
        Assert.AreEqual(2, viewModel.SearchResults.Sum(result => result.Matches.Count));
        Assert.IsFalse(viewModel.SearchResults.Any(result => result.FilePath.EndsWith(".png")));
        Assert.IsTrue(viewModel.SearchResults.All(result => result.Matches[0].ColumnNumber > 0));

        viewModel.IsCaseSensitive = false;
        await WaitForSearchAsync(viewModel);

        Assert.AreEqual(3, viewModel.SearchResults.Sum(result => result.Matches.Count));
    }

    [TestMethod]
    public async Task ClearingFilter_CancelsSearchAndClearsResults() {
        File.WriteAllText(Path.Combine(_testRoot, "index.html"), "needle");
        var viewModel = CreateViewModel();
        viewModel.Refresh(_testRoot);
        viewModel.FilterText = "needle";
        viewModel.FilterText = string.Empty;

        await Task.Delay(250);

        Assert.IsFalse(viewModel.IsSearchMode);
        Assert.IsFalse(viewModel.IsSearching);
        Assert.IsEmpty(viewModel.SearchResults);
    }

    private WebFileTreeViewModel CreateViewModel() => _viewModel = new WebFileTreeViewModel();

    private static WebFileItem LoadChild(
        WebFileTreeViewModel viewModel,
        string folderPath,
        string childPath) {
        WebFileItem folder = viewModel.FindItem(folderPath)!;
        viewModel.ToggleFolder(folder);
        return viewModel.FindItem(childPath)!;
    }

    private static T AssertSingle<T>(IEnumerable<T> values) {
        T[] items = values.ToArray();
        Assert.HasCount(1, items);
        return items[0];
    }

    private static async Task WaitForSearchAsync(WebFileTreeViewModel viewModel) {
        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (viewModel.IsSearching && DateTime.UtcNow < deadline) {
            await Task.Delay(20);
        }
        Assert.IsFalse(viewModel.IsSearching, "Search did not complete in time.");
    }

    private string _testRoot = null!;
    private WebFileTreeViewModel? _viewModel;
}
