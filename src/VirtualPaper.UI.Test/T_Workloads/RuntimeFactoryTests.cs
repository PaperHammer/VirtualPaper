using VirtualPaper.Common;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using Workloads.Entry;
using Workloads.Utils.DraftUtils.Interfaces;
using Workloads.Utils.DraftUtils.Models;

namespace VirtualPaper.UI.Test.T_Workloads;

[TestClass]
public class RuntimeFactoryTests {
    [TestMethod]
    [DataRow(FileType.FImage)]
    [DataRow(FileType.FDesign)]
    [DataRow(FileType.FWebDesign)]
    public void Create_ActivatesAndInitializesRequestedRuntime(FileType type) {
        var staticRuntime = new RecordingRuntime();
        var webRuntime = new RecordingRuntime();
        var factory = new RuntimeFactory(() => staticRuntime, () => webRuntime);
        var expectedRuntime = type == FileType.FWebDesign ? webRuntime : staticRuntime;

        var result = factory.Create("project.file", type);

        Assert.AreSame(expectedRuntime, result);
        Assert.AreEqual("project.file", expectedRuntime.InitializedFile);
        Assert.AreEqual(type, expectedRuntime.InitializedType);
        Assert.AreEqual(1, expectedRuntime.InitializeCount);
    }

    [TestMethod]
    public void Create_PropagatesActivationFailureWithoutInitialization() {
        var expected = new InvalidOperationException("activation failed");
        var factory = new RuntimeFactory(() => throw expected, () => new RecordingRuntime());

        var actual = Assert.ThrowsExactly<InvalidOperationException>(
            () => factory.Create("project.vpd", FileType.FDesign));

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public void Constructor_NullActivatorThrows() {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new RuntimeFactory(null!, () => new RecordingRuntime()));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new RuntimeFactory(() => new RecordingRuntime(), null!));
    }

    [TestMethod]
    public void Create_UnsupportedTypeThrowsBeforeActivation() {
        var activationCount = 0;
        var factory = new RuntimeFactory(
            () => { activationCount++; return new RecordingRuntime(); },
            () => { activationCount++; return new RecordingRuntime(); });

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => factory.Create("unknown", FileType.FUnknown));

        Assert.AreEqual("type", exception.ParamName);
        Assert.AreEqual(0, activationCount);
    }

    private sealed class RecordingRuntime : IRuntime {
        public event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged {
            add { }
            remove { }
        }
        public string FileName => string.Empty;
        public string FileNameWithoutEx => string.Empty;
        public string ProjectFilePath => string.Empty;
        public string Id => string.Empty;
        public bool IsSavedFromInit => false;
        public FileType RuntimeFileType => InitializedType ?? FileType.FUnknown;
        public string? InitializedFile { get; private set; }
        public FileType? InitializedType { get; private set; }
        public int InitializeCount { get; private set; }

        public void Initialize(string filePath, FileType fileType) {
            InitializeCount++;
            InitializedFile = filePath;
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
