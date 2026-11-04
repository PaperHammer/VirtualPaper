using System.Text;
using Workloads.Creation.WebBackdrop.Models;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
public class WebEditorFileTests {
    [TestInitialize]
    public void Initialize() {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "VirtualPaper.Tests",
            $"WebEditorFile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    [TestCleanup]
    public void Cleanup() {
        if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, recursive: true);
    }

    [TestMethod]
    public async Task LoadAsync_DetectsUtf8BomAndCrLf() {
        string path = Path.Combine(_testRoot, "index.html");
        await File.WriteAllTextAsync(path, "first\r\nsecond\r\n", new UTF8Encoding(true));

        WebEditorFile file = await WebEditorFile.LoadAsync(path);

        Assert.AreEqual("UTF-8 BOM", file.EncodingText);
        Assert.AreEqual("CRLF", file.LineEndingText);
        Assert.AreEqual("first\r\nsecond\r\n", file.Content);
        Assert.AreEqual(WebEditorFileKind.Text, file.Kind);
    }

    [TestMethod]
    public async Task LoadAsync_DetectsUtf16BigEndianAndLf() {
        string path = Path.Combine(_testRoot, "script.js");
        await File.WriteAllTextAsync(path, "第一行\n第二行\n", Encoding.BigEndianUnicode);

        WebEditorFile file = await WebEditorFile.LoadAsync(path);

        Assert.AreEqual("UTF-16 BE", file.EncodingText);
        Assert.AreEqual("LF", file.LineEndingText);
        Assert.AreEqual("第一行\n第二行\n", file.Content);
    }

    [TestMethod]
    public async Task LoadAsync_ImageDoesNotReadBinaryContent() {
        string path = Path.Combine(_testRoot, "preview.png");
        await File.WriteAllBytesAsync(path, [0, 1, 2, 3, 255]);

        WebEditorFile file = await WebEditorFile.LoadAsync(path);

        Assert.AreEqual(WebEditorFileKind.Image, file.Kind);
        Assert.IsFalse(file.CanOpenAsText);
        Assert.AreEqual(string.Empty, file.Content);
    }

    [TestMethod]
    public async Task LoadAsync_MissingFileReturnsSafeUnsupportedModel() {
        string path = Path.Combine(_testRoot, "missing.bin");

        WebEditorFile file = await WebEditorFile.LoadAsync(path);

        Assert.AreEqual(WebEditorFileKind.Unsupported, file.Kind);
        Assert.AreEqual(string.Empty, file.Content);
        Assert.IsTrue(file.IsSaved);
    }

    [TestMethod]
    public async Task ReopenWithEncoding_RefreshesContentAndSavedState() {
        string path = Path.Combine(_testRoot, "text.txt");
        await File.WriteAllTextAsync(path, "初始", Encoding.Unicode);
        WebEditorFile file = await WebEditorFile.LoadAsync(path);
        file.Content = "modified";
        file.SetSavedState(false);
        var eventCount = 0;
        file.IsSavedChanged += (_, _) => eventCount++;

        await file.ReopenWithEncodingAsync("UTF-16 LE");

        Assert.AreEqual("初始", file.Content);
        Assert.IsTrue(file.IsSaved);
        Assert.AreEqual(1, eventCount);
    }

    [TestMethod]
    public async Task ContentNullIsNormalizedAndSavedEventsOnlyFireOnTransitions() {
        string path = Path.Combine(_testRoot, "text.txt");
        File.WriteAllText(path, "text");
        WebEditorFile file = await WebEditorFile.LoadAsync(path);
        var eventCount = 0;
        file.IsSavedChanged += (_, _) => eventCount++;

        file.Content = null!;
        file.SetSavedState(false);
        file.SetSavedState(false);
        file.MarkAsSaved();

        Assert.AreEqual(string.Empty, file.Content);
        Assert.AreEqual(2, eventCount);
        Assert.IsTrue(file.IsSaved);
    }

    [TestMethod]
    public async Task RebindPathUpdatesKindAndRaisesDerivedPropertyNotifications() {
        string path = Path.Combine(_testRoot, "readme.md");
        File.WriteAllText(path, "markdown");
        WebEditorFile file = await WebEditorFile.LoadAsync(path);
        var properties = new List<string?>();
        file.PropertyChanged += (_, args) => properties.Add(args.PropertyName);
        string imagePath = Path.Combine(_testRoot, "preview.png");

        file.RebindPath(imagePath);

        Assert.AreEqual(WebEditorFileKind.Image, file.Kind);
        CollectionAssert.Contains(properties, nameof(WebEditorFile.FilePath));
        CollectionAssert.Contains(properties, nameof(WebEditorFile.Kind));
        CollectionAssert.Contains(properties, nameof(WebEditorFile.CanOpenAsText));
    }

    private string _testRoot = null!;
}
