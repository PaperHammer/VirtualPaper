using VirtualPaper.Common;
using Workloads.Entry.FileLoaders;

namespace VirtualPaper.UI.Test.T_Workloads;

[TestClass]
public class ProjectFileLoaderRegistryTests {
    [TestMethod]
    public async Task LoadAsync_SelectsFirstCompatibleLoader() {
        var incompatible = new FakeLoader(FileType.FWebDesign, null);
        var expected = new ProjectFileLoadResult("sample.vpd", FileType.FDesign);
        var compatible = new FakeLoader(FileType.FDesign, expected);
        var registry = new ProjectFileLoaderRegistry([incompatible, compatible]);

        var actual = await registry.LoadAsync("sample.vpd");

        Assert.AreSame(expected, actual);
        Assert.AreEqual(0, incompatible.LoadCount);
        Assert.AreEqual(1, compatible.LoadCount);
        Assert.AreEqual(FileType.FDesign, compatible.LastFileType);
    }

    [TestMethod]
    public async Task LoadAsync_WhenNoLoaderSupportsType_ReturnsNull() {
        var registry = new ProjectFileLoaderRegistry([
            new FakeLoader(FileType.FWebDesign, new("ignored.vpw", FileType.FWebDesign)),
        ]);

        var result = await registry.LoadAsync("sample.vpd");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task LoadAsync_PropagatesNullResultFromSelectedLoader() {
        var loader = new FakeLoader(FileType.FDesign, null);
        var registry = new ProjectFileLoaderRegistry([loader]);

        var result = await registry.LoadAsync("broken.vpd");

        Assert.IsNull(result);
        Assert.AreEqual(1, loader.LoadCount);
    }

    [TestMethod]
    public async Task LoadAsync_PropagatesLoaderException() {
        var expected = new InvalidDataException("invalid project");
        var registry = new ProjectFileLoaderRegistry([
            new ThrowingLoader(FileType.FDesign, expected),
        ]);

        var actual = await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => registry.LoadAsync("broken.vpd"));

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public async Task LoadAsync_UnknownExtensionThrowsArgumentException() {
        var registry = new ProjectFileLoaderRegistry([]);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => registry.LoadAsync("unknown.extension"));
    }

    private sealed class FakeLoader(FileType supportedType, ProjectFileLoadResult? result)
        : IProjectFileLoader {
        public int LoadCount { get; private set; }
        public FileType? LastFileType { get; private set; }

        public bool CanLoad(FileType fileType) => fileType == supportedType;

        public Task<ProjectFileLoadResult?> LoadAsync(string filePath, FileType fileType) {
            LoadCount++;
            LastFileType = fileType;
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingLoader(FileType supportedType, Exception exception)
        : IProjectFileLoader {
        public bool CanLoad(FileType fileType) => fileType == supportedType;

        public Task<ProjectFileLoadResult?> LoadAsync(string filePath, FileType fileType)
            => Task.FromException<ProjectFileLoadResult?>(exception);
    }
}
