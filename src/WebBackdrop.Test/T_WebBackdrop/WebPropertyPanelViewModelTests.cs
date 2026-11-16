using Microsoft.UI.Xaml;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;
using Workloads.Creation.WebBackdrop.ViewModels;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
public sealed class WebPropertyPanelViewModelTests {
    [TestInitialize]
    public void Initialize() {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.PropertyPanel-" + Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(_root, "index.html"), "<html></html>");
        _project = WebDesignFileUtil.Create(_root);
        _project.EnsureProjectStructure();
        _panel = new();
        _panel.LoadProject(_project);
    }

    [TestCleanup]
    public void Cleanup() { Directory.Delete(_root, recursive: true); }

    [TestMethod]
    [DataRow("script.js", true)]
    [DataRow("readme.md", true)]
    [DataRow("image.png", false)]
    [DataRow("payload.bin", false)]
    public async Task SelectingAFile_UsesItsDiskMetadata_AndOnlyCountsTextLines(string name, bool text) {
        string directory = Directory.CreateDirectory(Path.Combine(_root, "assets")).FullName;
        string path = Path.Combine(directory, name);
        await File.WriteAllTextAsync(path, "first\nsecond\nthird");
        var file = await WebEditorFile.LoadAsync(path);
        file.SetSavedState(false);
        _panel.Load(file, "JavaScript");
        Assert.AreEqual(name, _panel.FileNameText);
        Assert.AreEqual(FileUtil.GetRelativePath(_root, path), _panel.RelativePathText);
        Assert.AreEqual(FileUtil.SizeSuffix(new FileInfo(path).Length), _panel.SizeText);
        Assert.AreEqual(new FileInfo(path).LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), _panel.ModifiedText);
        Assert.AreEqual(LanguageUtil.GetI18n("WebBackdrop_Unsaved"), _panel.StatusText);
        Assert.AreEqual(text ? "3" : "-", _panel.LineCountText);
        Assert.AreEqual(text ? Visibility.Visible : Visibility.Collapsed, _panel.LineCountVisibility);
        Assert.AreEqual(Visibility.Visible, _panel.ItemInfoVisibility);
        Assert.AreEqual(Visibility.Collapsed, _panel.NoItemVisibility);
        if (text) Assert.AreEqual("JavaScript", _panel.TypeText);
        file.MarkAsSaved();
        _panel.Load(file, "JavaScript");
        Assert.AreEqual(LanguageUtil.GetI18n("WebBackdrop_Saved"), _panel.StatusText);
    }

    [TestMethod]
    public async Task SwitchingFromAFileToAFolderAndNoSelection_HidesIrrelevantMetadata() {
        var file = await WebEditorFile.LoadAsync(Path.Combine(_root, "index.html"));
        _panel.Load(file, "HTML");
        string folder = Directory.CreateDirectory(Path.Combine(_root, "nested")).FullName;
        _panel.LoadFolder(folder);
        Assert.AreEqual("nested", _panel.FileNameText);
        Assert.AreEqual(LanguageUtil.GetI18n("WebBackdrop_Folder"), _panel.TypeText);
        Assert.AreEqual(Visibility.Collapsed, _panel.StatusVisibility);
        Assert.AreEqual(Visibility.Collapsed, _panel.SizeVisibility);
        Assert.AreEqual(Visibility.Collapsed, _panel.LineCountVisibility);
        _panel.Load(null, "");
        Assert.AreEqual(Visibility.Collapsed, _panel.ItemInfoVisibility);
        Assert.AreEqual(Visibility.Visible, _panel.NoItemVisibility);
        _panel.Clear();
        Assert.AreEqual("-", _panel.ProjectTitleText);
        Assert.AreEqual("-", _panel.ProjectEntryText);
    }

    [TestMethod]
    public async Task MissingFilesAndFolders_KeepTheSelectionReadable_WithoutStaleDiskMetadata() {
        string path = Path.Combine(_root, "missing.html");
        _panel.Load(await WebEditorFile.LoadAsync(path), "HTML");
        Assert.AreEqual("missing.html", _panel.FileNameText);
        Assert.AreEqual("-", _panel.SizeText);
        Assert.AreEqual("-", _panel.ModifiedText);
        _panel.LoadFolder(Path.Combine(_root, "removed"));
        Assert.AreEqual("removed", _panel.FileNameText);
        Assert.AreEqual("-", _panel.ModifiedText);
    }

    private string _root = null!;
    private WebDesignFileUtil _project = null!;
    private WebPropertyPanelViewModel _panel = null!;
}
