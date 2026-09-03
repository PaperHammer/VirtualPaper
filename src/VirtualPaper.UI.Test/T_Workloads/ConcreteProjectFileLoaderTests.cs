using System.Runtime.InteropServices;
using VirtualPaper.Common;
using Workloads.Creation.StaticImg;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Entry.FileLoaders.Specific;
using Workloads.Entry.FileLoaders;
using Workloads.Entry;
using Workloads.Utils.DraftUtils.Interfaces;
using Workloads.Utils.DraftUtils.Models;

namespace VirtualPaper.UI.Test.T_Workloads;

[TestClass]
public class ConcreteProjectFileLoaderTests {
    private string _tempDirectory = null!;

    [TestInitialize]
    public void Initialize() {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ProjectLoaderTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [TestCleanup]
    public void Cleanup() {
        if (Directory.Exists(_tempDirectory)) {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ImageLoader_ValidPngReturnsOriginalPathAndType() {
        var path = Path.Combine(_tempDirectory, "sample.png");
        await File.WriteAllBytesAsync(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var loader = new ImageProjectFileLoader();

        var result = await loader.LoadAsync(path, FileType.FImage);

        Assert.IsNotNull(result);
        Assert.AreEqual(path, result.FilePath);
        Assert.AreEqual(FileType.FImage, result.FileType);
    }

    [TestMethod]
    public async Task ImageLoader_HeaderAndExtensionMismatchReturnsNull() {
        var path = Path.Combine(_tempDirectory, "renamed.png");
        await File.WriteAllBytesAsync(path, [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]);
        var loader = new ImageProjectFileLoader();

        var result = await loader.LoadAsync(path, FileType.FImage);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DesignLoader_StaticImageHeaderReturnsProject() {
        var path = WriteDesignFile(ProjectType.P_StaticImage);
        var loader = new DesignProjectFileLoader();

        var result = await loader.LoadAsync(path, FileType.FDesign);

        Assert.IsNotNull(result);
        Assert.AreEqual(path, result.FilePath);
        Assert.AreEqual(FileType.FDesign, result.FileType);
    }

    [TestMethod]
    public async Task DesignLoader_NonStaticProjectHeaderReturnsNull() {
        var path = WriteDesignFile(ProjectType.P_WebBackdrop);
        var loader = new DesignProjectFileLoader();

        var result = await loader.LoadAsync(path, FileType.FDesign);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DesignLoader_TruncatedFileReturnsNull() {
        var path = Path.Combine(_tempDirectory, "truncated.vpd");
        await File.WriteAllBytesAsync(path, [0x5F, 0x56, 0x50, 0x44]);

        var result = await new DesignProjectFileLoader().LoadAsync(path, FileType.FDesign);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task WebLoader_PreservesIdentifierAndType() {
        var loader = new WebProjectFileLoader();

        var result = await loader.LoadAsync("web-project", FileType.FWebDesign);

        Assert.IsNotNull(result);
        Assert.AreEqual("web-project", result.FilePath);
        Assert.AreEqual(FileType.FWebDesign, result.FileType);
    }

    [TestMethod]
    [DataRow(FileType.FImage, true, false, false)]
    [DataRow(FileType.FDesign, false, true, false)]
    [DataRow(FileType.FWebDesign, false, false, true)]
    [DataRow(FileType.FUnknown, false, false, false)]
    public void CanLoad_OnlyAcceptsOwnedFileType(
        FileType type,
        bool imageExpected,
        bool designExpected,
        bool webExpected) {

        Assert.AreEqual(imageExpected, new ImageProjectFileLoader().CanLoad(type));
        Assert.AreEqual(designExpected, new DesignProjectFileLoader().CanLoad(type));
        Assert.AreEqual(webExpected, new WebProjectFileLoader().CanLoad(type));
    }

    [TestMethod]
    public async Task FileToRuntimePipeline_LoadsThenInitializesMatchingRuntime() {
        var path = Path.Combine(_tempDirectory, "pipeline.png");
        await File.WriteAllBytesAsync(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var registry = new ProjectFileLoaderRegistry([
            new ImageProjectFileLoader(),
            new DesignProjectFileLoader(),
            new WebProjectFileLoader(),
        ]);
        var staticRuntime = new PipelineRuntime();
        var webRuntime = new PipelineRuntime();
        var factory = new RuntimeFactory(() => staticRuntime, () => webRuntime);

        var loadResult = await registry.LoadAsync(path);
        Assert.IsNotNull(loadResult);
        var runtime = factory.Create(loadResult.FilePath, loadResult.FileType);

        Assert.AreSame(staticRuntime, runtime);
        Assert.AreEqual(path, staticRuntime.InitializedPath);
        Assert.AreEqual(FileType.FImage, staticRuntime.InitializedType);
        Assert.IsNull(webRuntime.InitializedPath);
    }

    private string WriteDesignFile(ProjectType projectType) {
        var header = FileHeader.Create(new ArcSize(800, 600, 96, RebuildMode.None), 1, 0, 0);
        header.ProjType = projectType;
        var bytes = new byte[Marshal.SizeOf<FileHeader>()];
        var pointer = Marshal.AllocHGlobal(bytes.Length);
        try {
            Marshal.StructureToPtr(header, pointer, false);
            Marshal.Copy(pointer, bytes, 0, bytes.Length);
        }
        finally {
            Marshal.FreeHGlobal(pointer);
        }

        var path = Path.Combine(_tempDirectory, $"{projectType}.vpd");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private sealed class PipelineRuntime : IRuntime {
        public event EventHandler<VirtualPaper.Common.Utils.UndoRedo.Events.IsSavedChangedEventArgs>? IsSavedChanged {
            add { }
            remove { }
        }
        public string FileName => string.Empty;
        public string FileNameWithoutEx => string.Empty;
        public string ProjectFilePath => InitializedPath ?? string.Empty;
        public string Id => string.Empty;
        public bool IsSavedFromInit => false;
        public FileType RuntimeFileType => InitializedType ?? FileType.FUnknown;
        public string? InitializedPath { get; private set; }
        public FileType? InitializedType { get; private set; }

        public void Initialize(string filePath, FileType fileType) {
            InitializedPath = filePath;
            InitializedType = fileType;
        }
        public Task<bool> SaveAsync() => Task.FromResult(true);
        public Task<string?> SaveAsAsync() => Task.FromResult<string?>(null);
        public Task UndoAsync() => Task.CompletedTask;
        public Task RedoAsync() => Task.CompletedTask;
        public Task<string?> ExportAsync(ExportImageFormat format) => Task.FromResult<string?>(null);
        public Task<bool> AddToLibraryAsync() => Task.FromResult(false);
    }
}
