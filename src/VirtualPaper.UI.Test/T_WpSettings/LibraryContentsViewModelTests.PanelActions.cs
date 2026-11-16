using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UI.Test.T_WpSettings;

[DoNotParallelize]
public partial class LibraryContentsViewModelTests {
    [TestCleanup]
    public void UnregisterPanelActions() {
        PanelMessageCenter.UnregisterAllActions(PanelContracts.WpSettings.Id);
        if (_panelImportRoot != null) Directory.Delete(_panelImportRoot, recursive: true);
    }

    [TestMethod]
    [DataRow("unsupported.txt")]
    [DataRow("")]
    public async Task PanelImport_UnsupportedSourceDoesNotCallCoreOrAlterExistingLibrary(string path) {
        var existing = MakeWpData("existing", "Existing"); PopulateLibrary(existing);
        Assert.IsFalse(await ImportThroughPanel(path));
        CollectionAssert.AreEqual(new[] { existing }, _vm.LibraryWallpapers.ToArray());
        _wpControlClient.Verify(client => client.CreateBasicDataAsync(It.IsAny<string>(), It.IsAny<FileType>(), It.IsAny<CancellationToken>(), false), Times.Never);
        _wallpaperIndexService.Verify(index => index.Update(It.IsAny<IWpBasicData>()), Times.Never);
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("invalid")]
    [DataRow("failure")]
    public async Task PanelImport_FailedMetadataKeepsExistingLibrary_AndRetrySucceeds(string failure) {
        string source = CreatePanelImage();
        var existing = MakeWpData("existing", "Existing"); PopulateLibrary(existing);
        var setup = _wpControlClient.Setup(client => client.CreateBasicDataAsync(source, FileType.FImage, It.IsAny<CancellationToken>(), false));
        if (failure == "failure") setup.ThrowsAsync(new IOException("Source unavailable"));
        else setup.ReturnsAsync(failure == "empty" ? null! : new Grpc_WpBasicData { FType = (Grpc_FileType)FileType.FImage, AppInfo = new() });
        Assert.IsFalse(await ImportThroughPanel(source));
        _wpControlClient.Verify(client => client.CreateBasicDataAsync(source, FileType.FImage, It.IsAny<CancellationToken>(), false), Times.Once);
        CollectionAssert.AreEqual(new[] { existing }, _vm.LibraryWallpapers.ToArray());
        _wallpaperIndexService.Verify(index => index.Update(It.IsAny<IWpBasicData>()), Times.Never);

        var imported = MakeWpData("imported", "Imported");
        setup.ReturnsAsync(DataAssist.BasicDataToGrpcData(imported));
        Assert.IsTrue(await ImportThroughPanel(source));
        Assert.HasCount(2, _vm.LibraryWallpapers);
        CollectionAssert.AreEquivalent(new[] { "existing", "imported" }, _vm.LibraryWallpapers.Select(item => item.WallpaperUid).ToArray());
        _wallpaperIndexService.Verify(index => index.Update(It.Is<IWpBasicData>(item => item.WallpaperUid == "imported")), Times.Once);
    }

    [TestMethod]
    public async Task PanelImport_ReimportingSameIdentityUpdatesCardWithoutDuplicatingIt() {
        string source = CreatePanelImage();
        var existing = MakeWpData("same", "Original"); PopulateLibrary(existing);
        var replacement = MakeWpData("same", "Updated");
        _wpControlClient.Setup(client => client.CreateBasicDataAsync(source, FileType.FImage, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(DataAssist.BasicDataToGrpcData(replacement));
        Assert.IsTrue(await ImportThroughPanel(source));
        Assert.HasCount(1, _vm.LibraryWallpapers);
        Assert.AreEqual("same", _vm.LibraryWallpapers[0].WallpaperUid);
        Assert.AreEqual("Updated", _vm.LibraryWallpapers[0].Title);
        Assert.AreEqual(existing.FolderPath, _vm.LibraryWallpapers[0].FolderPath);
        _wallpaperIndexService.Verify(index => index.Update(It.Is<IWpBasicData>(item => item.Title == "Updated")), Times.Once);
    }

    private static Task<bool> ImportThroughPanel(string path) => PanelMessageCenter.InvokeAsync<string, bool>(
        PanelContracts.WpSettings.Id, PanelContracts.WpSettings.Action_ImportWallpaper, path);

    private string CreatePanelImage() {
        _panelImportRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.Tests", "PanelImport-" + Guid.NewGuid().ToString("N"))).FullName;
        string path = Path.Combine(_panelImportRoot, "source.png");
        File.WriteAllBytes(path, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jYyQAAAAASUVORK5CYII="));
        Assert.AreEqual(FileType.FImage, FileFilter.GetFileType(path));
        return path;
    }

    private string? _panelImportRoot;
}
