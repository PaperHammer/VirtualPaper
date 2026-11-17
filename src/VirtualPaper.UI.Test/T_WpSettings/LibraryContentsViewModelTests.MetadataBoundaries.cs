using System.Text.Json.Nodes;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_WpSettings;

public partial class LibraryContentsViewModelTests {
    [TestMethod]
    [DataRow("malformed")]
    [DataRow("null")]
    [DataRow("unsupported")]
    [DataRow("null-info")]
    [DataRow("null-uid")]
    [DataRow("blank-uid")]
    [DataRow("null-version")]
    [DataRow("blank-version")]
    public async Task InvalidMetadata_IsSkippedWithoutHidingLaterValidWallpapers(string kind) {
        string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.MetadataBoundary-" + Guid.NewGuid().ToString("N"))).FullName;
        try {
            ReadyIndex();
            var invalid = MakeWpData("invalid", "Invalid", Path.Combine(root, "invalid"));
            var valid = MakeWpData("valid", "Available", Path.Combine(root, "valid"));
            await invalid.SaveMetadataAsync();
            await valid.SaveMetadataAsync();
            var entries = new[] { WallpaperIndexEntry.FromData(invalid), WallpaperIndexEntry.FromData(valid) };
            string badPath = entries[0].JsonPath;
            var json = JsonNode.Parse(await File.ReadAllTextAsync(badPath, TestContext.CancellationToken))!.AsObject();
            switch (kind) {
                case "unsupported": json["FType"] = nameof(FileType.FUnknown); break;
                case "null-info": json["AppInfo"] = null; break;
                case "null-uid": json["WallpaperUid"] = null; break;
                case "blank-uid": json["WallpaperUid"] = "  "; break;
                case "null-version": json["AppInfo"]!["AppVersion"] = null; break;
                case "blank-version": json["AppInfo"]!["AppVersion"] = "  "; break;
            }
            await File.WriteAllTextAsync(badPath, kind switch {
                "malformed" => "{",
                "null" => "null",
                _ => json.ToJsonString()
            }, TestContext.CancellationToken);
            _wallpaperIndexService.Setup(index => index.Query(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<FilterContext>()))
                .Returns((int offset, int limit, FilterContext _) => entries.Skip(offset).Take(limit).ToArray());

            await _vm.ApplyFilterAsync(FilterContext.Empty);

            Assert.HasCount(1, _vm.LibraryWallpapers);
            Assert.AreEqual("valid", _vm.LibraryWallpapers[0].WallpaperUid);
            Assert.AreEqual(valid.FilePath, _vm.LibraryWallpapers[0].FilePath);
            Assert.AreEqual(LoadingStatus.Ready, _vm.LibLoadingStatus);
            Assert.IsFalse(_vm.IsEmpty);
            Assert.IsTrue(_vm.EndOfResults);
            await _vm.LoadMorePageAsync();
            Assert.HasCount(1, _vm.LibraryWallpapers);
            _wallpaperIndexService.Verify(index => index.Query(0, 30, FilterContext.Empty), Times.Once);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
