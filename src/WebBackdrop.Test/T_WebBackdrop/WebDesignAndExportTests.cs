using System.IO.Compression;
using System.Text.Json.Nodes;
using VirtualPaper.Models.Cores;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
public class WebDesignAndExportTests {
    [TestInitialize]
    public void Initialize() {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "VirtualPaper.Tests",
            $"WebDesign-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    [TestCleanup]
    public void Cleanup() {
        if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, recursive: true);
    }

    [TestMethod]
    public void Create_WithDirectory_UsesConventionalProjectFileAndCreatesStructure() {
        string projectFolder = Path.Combine(_testRoot, "Sample");
        Directory.CreateDirectory(projectFolder);
        File.WriteAllText(Path.Combine(projectFolder, "index.html"), "<html></html>");

        WebDesignFileUtil util = WebDesignFileUtil.Create(projectFolder);
        util.EnsureProjectStructure();

        Assert.AreEqual(projectFolder, util.ProjectFolder);
        Assert.AreEqual(Path.Combine(projectFolder, "Sample.vpw"), util.ProjectFilePath);
        Assert.IsTrue(File.Exists(util.ProjectFilePath));
        Assert.IsTrue(File.Exists(Path.Combine(projectFolder, "project.json")));
        Assert.IsTrue(util.IsSaveFromInit);
        Assert.IsTrue(util.GetManifestItems().Any(item =>
            item.Path == "index.html" && item.Role == "entry"));
        Assert.IsTrue(util.GetManifestItems().Any(item => item.Role == "solution"));
        Assert.IsTrue(util.GetManifestItems().Any(item => item.Role == "metadata"));
        Assert.IsFalse(Directory.EnumerateFiles(projectFolder, ".*.tmp").Any());
    }

    [TestMethod]
    public void AddManifestPaths_DeduplicatesAndClassifiesFiles() {
        WebDesignFileUtil util = CreateProject();
        string style = Path.Combine(util.ProjectFolder, "style.css");
        string asset = Path.Combine(util.ProjectFolder, "asset.bin");
        File.WriteAllText(style, "body{}");
        File.WriteAllBytes(asset, [1, 2, 3]);

        util.AddManifestPaths([style, style.ToUpperInvariant(), asset]);

        IReadOnlyList<WebProjectManifestItem> items = util.GetManifestItems();
        Assert.HasCount(1, items.Where(item =>
            string.Equals(item.Path, "style.css", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(items.Any(item => item.Path == "style.css" && item.Role == "style"));
        Assert.IsTrue(items.Any(item => item.Path == "asset.bin" && item.Role == "asset"));
    }

    [TestMethod]
    public void RecursiveRenameAndRemove_UpdateFolderAndDescendants() {
        WebDesignFileUtil util = CreateProject();
        string oldFolder = Directory.CreateDirectory(Path.Combine(util.ProjectFolder, "scripts")).FullName;
        string nested = Directory.CreateDirectory(Path.Combine(oldFolder, "nested")).FullName;
        File.WriteAllText(Path.Combine(oldFolder, "app.js"), "app");
        File.WriteAllText(Path.Combine(nested, "child.js"), "child");
        util.AddManifestPathRecursive(oldFolder);

        string newFolder = Path.Combine(util.ProjectFolder, "src");
        Directory.Move(oldFolder, newFolder);
        util.RenameManifestPath(oldFolder, newFolder);

        string[] renamed = util.GetManifestItems().Select(item => item.Path).ToArray();
        Assert.IsFalse(renamed.Any(path => path.StartsWith("scripts", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(renamed.Contains("src/app.js"));
        Assert.IsTrue(renamed.Contains("src/nested/child.js"));

        util.RemoveManifestPath(newFolder);

        Assert.IsFalse(util.GetManifestItems().Any(item =>
            item.Path.StartsWith("src", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ReloadManifest_ObservesExternalChanges() {
        WebDesignFileUtil util = CreateProject();
        Assert.IsFalse(util.GetManifestItems().Any(item => item.Path == "external.js"));
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(util.ProjectFilePath))!.AsObject();
        manifest["files"]!.AsArray().Add(new JsonObject {
            ["path"] = "external.js",
            ["type"] = "javascript",
            ["role"] = "script",
        });
        File.WriteAllText(util.ProjectFilePath, manifest.ToJsonString());

        util.ReloadManifest();

        Assert.IsTrue(util.GetManifestItems().Any(item =>
            item.Path == "external.js" && item.Role == "script"));
    }

    [TestMethod]
    public void CorruptManifest_IsNotOverwrittenByMutation() {
        string projectFolder = Path.Combine(_testRoot, "Corrupt");
        Directory.CreateDirectory(projectFolder);
        string projectFile = Path.Combine(projectFolder, "Corrupt.vpw");
        const string corruptJson = "{ this is not json";
        File.WriteAllText(projectFile, corruptJson);
        WebDesignFileUtil util = WebDesignFileUtil.Create(projectFile);
        string asset = Path.Combine(projectFolder, "asset.txt");
        File.WriteAllText(asset, "asset");

        util.AddManifestPath(asset);

        Assert.AreEqual(corruptJson, File.ReadAllText(projectFile));
    }

    [TestMethod]
    public void Export_UsesManifestWhitelistAndEntryFallback() {
        WebDesignFileUtil util = CreateProject();
        string entry = Path.Combine(util.ProjectFolder, "main.html");
        string script = Path.Combine(util.ProjectFolder, "script.js");
        string untracked = Path.Combine(util.ProjectFolder, "secret.txt");
        File.WriteAllText(entry, "<html></html>");
        File.WriteAllText(script, "console.log(1)");
        File.WriteAllText(untracked, "not exported");
        File.WriteAllText(Path.Combine(util.ProjectFolder, "wp_metadata_basic.json"), "{}");
        util.AddManifestPath(entry, "entry");
        util.AddManifestPath(script);
        File.Delete(Path.Combine(util.ProjectFolder, "index.html"));
        WpWebProjectData data = util.GetOrCreateProjectData();
        data.File = "missing.html";
        data.Preview = "missing.jpg";
        string zipPath = Path.Combine(_testRoot, "export.zip");

        string result = WebProjectExporter.Export(util, zipPath);

        Assert.AreEqual(zipPath, result);
        Assert.AreEqual("main.html", data.File);
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        string[] entries = archive.Entries.Select(item => item.FullName.Replace('\\', '/')).ToArray();
        CollectionAssert.Contains(entries, "main.html");
        CollectionAssert.Contains(entries, "script.js");
        CollectionAssert.Contains(entries, "project.json");
        CollectionAssert.DoesNotContain(entries, "secret.txt");
        CollectionAssert.DoesNotContain(entries, Path.GetFileName(util.ProjectFilePath));
        CollectionAssert.DoesNotContain(entries, "wp_metadata_basic.json");
    }

    [TestMethod]
    public void Export_WhenEntryIsMissing_ThrowsWithoutCreatingArchive() {
        WebDesignFileUtil util = CreateProject(createIndex: false);
        util.GetOrCreateProjectData().File = "missing.html";
        string zipPath = Path.Combine(_testRoot, "missing.zip");

        Assert.ThrowsExactly<FileNotFoundException>(() => WebProjectExporter.Export(util, zipPath));
        Assert.IsFalse(File.Exists(zipPath));
    }

    [TestMethod]
    public void Export_WhenCancelled_DoesNotLeaveArchive() {
        WebDesignFileUtil util = CreateProject();
        string zipPath = Path.Combine(_testRoot, "cancelled.zip");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() =>
            WebProjectExporter.Export(util, zipPath, cancellation.Token));
        Assert.IsFalse(File.Exists(zipPath));
    }

    [TestMethod]
    public void ExportFull_IncludesProjectAndUntrackedFiles() {
        WebDesignFileUtil util = CreateProject();
        File.WriteAllText(Path.Combine(util.ProjectFolder, "untracked.txt"), "included");
        string zipPath = Path.Combine(_testRoot, "full.zip");

        WebProjectExporter.ExportFull(util, zipPath);

        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        string[] entries = archive.Entries.Select(item => item.FullName.Replace('\\', '/')).ToArray();
        CollectionAssert.Contains(entries, "untracked.txt");
        CollectionAssert.Contains(entries, Path.GetFileName(util.ProjectFilePath));
    }

    private WebDesignFileUtil CreateProject(bool createIndex = true) {
        string projectFolder = Path.Combine(_testRoot, $"Project-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectFolder);
        if (createIndex) {
            File.WriteAllText(Path.Combine(projectFolder, "index.html"), "<html></html>");
        }
        else {
            // Keep the folder non-empty so EnsureProjectStructure does not copy the bundled template.
            File.WriteAllText(Path.Combine(projectFolder, "placeholder.txt"), "placeholder");
        }
        WebDesignFileUtil util = WebDesignFileUtil.Create(projectFolder);
        util.EnsureProjectStructure();
        return util;
    }

    private string _testRoot = null!;
}
