using System.Collections.ObjectModel;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.ML.SuperResolution.Interfaces;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_WpSettings {
    [TestClass]
    public class ScreenFitTests {
        [TestMethod]
        public void FitPlan_PreservesAspectRatio_AndComputesTheVisibleSource() {
            var contain = ScreenFitPlan.Calculate(1000, 1000, 1920, 1080, false);
            Assert.AreEqual(1000d, contain.VisibleWidth, 0.001);
            Assert.AreEqual(1000d, contain.VisibleHeight, 0.001);
            Assert.AreEqual(1080, contain.OutputWidth);
            Assert.AreEqual(1080, contain.OutputHeight);
            var cover = ScreenFitPlan.Calculate(1000, 1000, 1920, 1080, true);
            Assert.AreEqual(1000d, cover.VisibleWidth, 0.001);
            Assert.AreEqual(562.5, cover.VisibleHeight, 0.001);
            Assert.AreEqual(1920, cover.OutputWidth);
            Assert.AreEqual(1920, cover.OutputHeight);
        }
        [TestMethod]
        public void FitPlan_HandlesPortraitScreens_AndLimitsEnhancementToFourTimes() {
            var portrait = ScreenFitPlan.Calculate(2000, 1000, 1080, 1920, true);
            Assert.AreEqual(562.5, portrait.VisibleWidth, 0.001);
            Assert.AreEqual(1000d, portrait.VisibleHeight, 0.001);
            Assert.AreEqual(3840, portrait.OutputWidth);
            Assert.AreEqual(1920, portrait.OutputHeight);
            var small = ScreenFitPlan.Calculate(100, 200, 3840, 2160, true);
            Assert.AreEqual(400, small.OutputWidth);
            Assert.AreEqual(800, small.OutputHeight);
            Assert.IsFalse(ScreenFitPlan.Calculate(3840, 2160, 1920, 1080, true).NeedsEnhancement);
            Assert.Throws<ArgumentOutOfRangeException>(() => ScreenFitPlan.Calculate(0, 1, 1920, 1080, true));
        }

        private string _root = null!, _source = null!;
        private Mock<IMonitorManagerClient> _monitors = null!;
        private Mock<IWallpaperControlClient> _wallpapers = null!;
        private Mock<ISettings> _settings = null!;
        private Mock<ISuperResolution> _model = null!;
        private Mock<IStoragePicker> _picker = null!;
        private ScreenFitViewModel _vm = null!;
        private IMonitor _monitor = null!;
        private StartArgsWeb _args = null!;
        [TestInitialize]
        public void Setup() {
            _root = Path.Combine(Path.GetTempPath(), "VirtualPaper-ScreenFit-Test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _source = Path.Combine(_root, "source.png"); File.WriteAllText(_source, "original");
            _monitor = new VirtualPaper.Models.Cores.Monitor {
                DeviceId = "screen-2", Content = "2", Bounds = new Rectangle(-1920, 0, 1920, 1080)
            };
            _monitors = new();
            _monitors.SetupGet(m => m.Monitors).Returns(new ReadOnlyCollection<IMonitor>([_monitor]));
            _monitors.SetupGet(m => m.PrimaryMonitor).Returns(_monitor);
            _wallpapers = new(); _model = new(); _settings = new();
            _settings.SetupProperty(s => s.WallpaperArrangement, WallpaperArrangement.Per);
            var user = new Mock<IUserSettingsClient>(); user.SetupGet(s => s.Settings).Returns(_settings.Object);
            _picker = new();
            _vm = new ScreenFitViewModel(_monitors.Object, _wallpapers.Object, user.Object, _picker.Object, () => _model.Object);
            _vm.SetSource(_source, 1000, 1000);
            _args = new StartArgsWeb {
                WpEffectFilePathUsing = Path.Combine(_root, "using.json"),
                WpEffectFilePathTemplate = Path.Combine(_root, "template.json"),
                WpEffectFilePathTemporary = Path.Combine(_root, "temporary.json")
            };
            File.WriteAllText(_args.WpEffectFilePathUsing, """{"Scaling":{"Value":0},"Atmosphere":{"Value":2}}""");
            _wallpapers.Setup(w => w.CreateBasicDataAsync(It.IsAny<string>(), FileType.FImage, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(new Grpc_WpBasicData { WallpaperUid = "fit-wallpaper", FType = (Grpc_FileType)FileType.FImage,
                    FilePath = _source, FolderPath = _root, AppInfo = new() { AppVersion = "1.0" } });
            _wallpapers.Setup(w => w.GetPlayerStartArgsAsync(It.IsAny<IWpBasicData>(), RuntimeType.RImage, null, It.IsAny<CancellationToken>(), null))
                .ReturnsAsync(JsonSerializer.Serialize(_args, StartArgsWebContext.Default.StartArgsWeb));
            _wallpapers.Setup(w => w.SetWallpaperAsync(_monitor, It.IsAny<IWpBasicData>(), RuntimeType.RImage, true, null,
                _args.WpEffectFilePathUsing, _args.WpEffectFilePathTemplate, _args.WpEffectFilePathTemporary, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Grpc_SetWallpaperResponse { IsFinished = true });
        }
        [TestCleanup]
        public void Cleanup() => Directory.Delete(_root, true);

        private async Task<WallpaperIndexEntry> CreateLibraryImageAsync() {
            string folder = Directory.CreateDirectory(Path.Combine(_root, "library-item")).FullName;
            string path = Path.Combine(folder, "portrait.png");
            using (var bitmap = new Bitmap(256, 384)) bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            var data = new WpBasicData {
                WallpaperUid = "existing-image", Title = "Portrait", Authors = "Artist", FType = FileType.FImage,
                FolderPath = folder, FolderName = "library-item", FilePath = path,
                AppInfo = new() { AppVersion = "0.6.1.0" }
            };
            await data.SaveMetadataAsync();
            return WallpaperIndexEntry.FromData(data);
        }

        [TestMethod]
        public async Task LibraryImage_ResolvesRelativePaths_AndReusesIdentityWithoutImporting() {
            var entry = await CreateLibraryImageAsync();
            byte[] metadata = await File.ReadAllBytesAsync(entry.JsonPath);
            await _vm.SelectLibraryImageAsync(entry);
            Assert.AreEqual(Path.Combine(entry.FolderPath, "portrait.png"), _vm.SourcePath);
            Assert.AreEqual(entry.Uid, _vm.LibrarySourceUid);
            Assert.AreEqual(_vm.SourcePath, _vm.PreviewPath);
            Assert.AreEqual(ScreenFitPlan.Calculate(256, 384, 1920, 1080, true), _vm.Plan);
            Assert.IsFalse(_vm.UseEnhancement);
            byte[] source = await File.ReadAllBytesAsync(_vm.SourcePath);
            IWpBasicData? applied = null;
            _vm.Imported += data => applied = data;

            Assert.IsTrue(await _vm.ApplyAsync(CancellationToken.None));
            Assert.IsNotNull(applied);
            Assert.AreEqual(entry.Uid, applied.WallpaperUid);
            _wallpapers.Verify(w => w.CreateBasicDataAsync(It.IsAny<string>(), It.IsAny<FileType>(), It.IsAny<CancellationToken>(), false), Times.Never);
            _wallpapers.Verify(w => w.SetWallpaperAsync(_monitor, It.Is<IWpBasicData>(data => data.WallpaperUid == entry.Uid),
                RuntimeType.RImage, true, null, _args.WpEffectFilePathUsing, _args.WpEffectFilePathTemplate,
                _args.WpEffectFilePathTemporary, It.IsAny<CancellationToken>()), Times.Once);
            CollectionAssert.AreEqual(metadata, await File.ReadAllBytesAsync(entry.JsonPath));
            CollectionAssert.AreEqual(source, await File.ReadAllBytesAsync(_vm.SourcePath));
        }

        [TestMethod]
        public async Task LibraryImage_EnhancementImportsACopy_AndPreservesOriginal() {
            var entry = await CreateLibraryImageAsync();
            await _vm.SelectLibraryImageAsync(entry);
            byte[] metadata = await File.ReadAllBytesAsync(entry.JsonPath);
            byte[] source = await File.ReadAllBytesAsync(_vm.SourcePath);
            string? generated = null;
            _model.Setup(m => m.RunAndSave(_vm.SourcePath, It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<CancellationToken>()))
                .Returns((string input, string output, uint width, uint height, CancellationToken ct) => {
                    generated = output; File.WriteAllText(output, "enhanced"); return output;
                });
            _vm.UseEnhancement = true;

            Assert.IsTrue(await _vm.ApplyAsync(CancellationToken.None));
            Assert.IsNotNull(generated);
            Assert.IsFalse(File.Exists(generated));
            _wallpapers.Verify(w => w.CreateBasicDataAsync(generated, FileType.FImage, It.IsAny<CancellationToken>(), false), Times.Once);
            CollectionAssert.AreEqual(metadata, await File.ReadAllBytesAsync(entry.JsonPath));
            CollectionAssert.AreEqual(source, await File.ReadAllBytesAsync(_vm.SourcePath));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task UnreadableLibrarySelection_KeepsPreviousSelection_AndAllowsRetry(bool invalidImage) {
            var entry = await CreateLibraryImageAsync();
            await _vm.SelectLibraryImageAsync(entry);
            string selected = _vm.SourcePath;
            var failed = entry with { Uid = "broken", JsonPath = Path.Combine(_root, "missing.json") };
            if (invalidImage) {
                var data = new WpBasicData {
                    WallpaperUid = "broken", FType = FileType.FImage, FolderPath = _root, FilePath = _source,
                    AppInfo = new() { AppVersion = "0.6.1.0" }
                };
                await data.SaveMetadataAsync();
                failed.JsonPath = Path.Combine(_root, Constants.Field.WpBasicDataFileName);
            }
            await _vm.SelectLibraryImageAsync(failed);
            Assert.IsFalse(_vm.IsBusy);
            Assert.AreEqual(selected, _vm.SourcePath);
            Assert.AreEqual(entry.Uid, _vm.LibrarySourceUid);
            Assert.IsFalse(string.IsNullOrEmpty(_vm.ErrorMessage));
            Assert.IsTrue(_vm.CanApply);
            await _vm.SelectLibraryImageAsync(entry);
            Assert.AreEqual(string.Empty, _vm.ErrorMessage);
        }

        [TestMethod]
        public async Task CancelledFilePickerAndUnsupportedLibraryType_KeepExistingChoice() {
            var entry = await CreateLibraryImageAsync();
            await _vm.SelectLibraryImageAsync(entry);
            string selected = _vm.SourcePath;
            await _vm.SelectImageAsync(); // A cancelled picker returns no storage items.
            await _vm.SelectLibraryImageAsync(entry with { FType = FileType.FVideo, JsonPath = "not-read" });
            Assert.AreEqual(selected, _vm.SourcePath);
            Assert.AreEqual(entry.Uid, _vm.LibrarySourceUid);
            Assert.IsTrue(_vm.CanApply);
            Assert.AreEqual(string.Empty, _vm.ErrorMessage);
        }

        [TestMethod]
        public async Task SelectingAFileAfterLibraryImage_DoesNotReuseThePreviousLibraryIdentity() {
            var entry = await CreateLibraryImageAsync();
            await _vm.SelectLibraryImageAsync(entry);
            string source = Path.Combine(_root, "selected.png");
            File.Copy(_vm.SourcePath, source);
            var item = new Mock<Windows.Storage.IStorageItem>();
            item.SetupGet(file => file.Path).Returns(source);
            _picker.Setup(picker => picker.PickFilesAsync(It.IsAny<nint>(), It.IsAny<string[]>(), false))
                .ReturnsAsync([item.Object]);

            await _vm.SelectImageAsync();
            Assert.AreEqual(source, _vm.SourcePath);
            Assert.IsNull(_vm.LibrarySourceUid);
            Assert.IsTrue(await _vm.ApplyAsync(CancellationToken.None));
            _wallpapers.Verify(w => w.CreateBasicDataAsync(source, FileType.FImage, It.IsAny<CancellationToken>(), false), Times.Once);
        }

        [TestMethod]
        public void SelectedDisplay_TwoWayBindingFeedback_DoesNotReenterNotifications() {
            int notifications = 0;
            _vm.PropertyChanged += (_, e) => {
                if (e.PropertyName != nameof(ScreenFitViewModel.SelectedMonitor)) return;
                notifications++;
                if (notifications < 3) _vm.SelectedMonitor = _vm.SelectedMonitor! with { };
            };
            _vm.SelectedMonitor = _vm.SelectedMonitor! with { Label = "Updated display" };
            Assert.AreEqual(1, notifications);
        }

        [TestMethod]
        public void Preview_UsesCurrentWallpaperUntilAnImageIsSelected_WithoutEnablingApply() {
            _monitor.ThumbnailPath = Path.Combine(_root, "current-wallpaper.jpg");
            _vm.SetSource(string.Empty, 0, 0);
            Assert.AreEqual(_monitor.ThumbnailPath, _vm.PreviewPath);
            Assert.AreEqual(string.Empty, _vm.SourcePath);
            Assert.IsTrue(_vm.CanEdit);
            Assert.IsFalse(_vm.CanConfigure);
            Assert.IsFalse(_vm.CanApply);

            _vm.SetSource(_source, 1000, 1000);
            Assert.AreEqual(_source, _vm.PreviewPath);
            Assert.IsTrue(_vm.CanConfigure);
            Assert.IsTrue(_vm.CanApply);
            _model.Verify(m => m.LoadModel(It.IsAny<string?>()), Times.Never);
        }

        [TestMethod]
        public void Preview_FollowsSelectedDisplay_AndKeepsUserSelectedImage() {
            var second = new VirtualPaper.Models.Cores.Monitor {
                DeviceId = "screen-3", Bounds = new Rectangle(0, 0, 2560, 1440), ThumbnailPath = "second-wallpaper.jpg"
            };
            _monitors.SetupGet(m => m.Monitors).Returns(new ReadOnlyCollection<IMonitor>([_monitor, second]));
            _vm.SetSource(string.Empty, 0, 0);
            bool previewChanged = false;
            _vm.PropertyChanged += (_, e) => previewChanged |= e.PropertyName == nameof(ScreenFitViewModel.PreviewPath);
            _vm.SelectedMonitor = new ScreenFitMonitor(second.DeviceId, "3", 2560, 1440);
            Assert.AreEqual(second.ThumbnailPath, _vm.PreviewPath);
            Assert.IsTrue(previewChanged);
            Assert.IsFalse(_vm.CanApply);

            second.IsStale = true;
            Assert.AreEqual(string.Empty, _vm.PreviewPath);
            _vm.SetSource(_source, 1000, 1000);
            _vm.SelectedMonitor = _vm.Monitors[0];
            Assert.AreEqual(_source, _vm.PreviewPath);
            _wallpapers.Verify(w => w.SetWallpaperAsync(It.IsAny<IMonitor>(), It.IsAny<IWpBasicData>(), It.IsAny<RuntimeType>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        [DataRow(0, 1)]
        [DataRow(1, 2)]
        public async Task Apply_UsesSelectedDisplayAndFitMode_WithoutChangingSourceOrOtherEffects(int fit, int scaling) {
            _vm.FitIndex = fit;
            bool imported = false; _vm.Imported += _ => imported = true;
            Assert.IsTrue(await _vm.ApplyAsync(CancellationToken.None));
            Assert.IsTrue(imported);
            Assert.AreEqual("original", File.ReadAllText(_source));
            _model.Verify(m => m.LoadModel(It.IsAny<string?>()), Times.Never);
            foreach (var path in new[] { _args.WpEffectFilePathUsing, _args.WpEffectFilePathTemplate, _args.WpEffectFilePathTemporary }) {
                var json = JsonNode.Parse(File.ReadAllText(path!))!;
                Assert.AreEqual(scaling, json["Scaling"]!["Value"]!.GetValue<int>());
                Assert.AreEqual(2, json["Atmosphere"]!["Value"]!.GetValue<int>());
            }
            _wallpapers.Verify(w => w.SetWallpaperAsync(_monitor, It.IsAny<IWpBasicData>(), RuntimeType.RImage, true, null,
                _args.WpEffectFilePathUsing, _args.WpEffectFilePathTemplate, _args.WpEffectFilePathTemporary, It.IsAny<CancellationToken>()), Times.Once);
        }
        [TestMethod]
        public async Task Enhancement_UsesAspectPreservingSize_AndCleansOnlyItsTemporaryCopy() {
            string? generated = null;
            _model.Setup(m => m.RunAndSave(_source, It.IsAny<string>(), 1920, 1920, It.IsAny<CancellationToken>()))
                .Returns((string input, string output, uint width, uint height, CancellationToken ct) => {
                    Assert.IsFalse(_vm.CanEdit);
                    Assert.IsFalse(_vm.CanConfigure);
                    generated = output; File.WriteAllText(output, "enhanced"); return output;
                });
            _vm.UseEnhancement = true;
            Assert.IsTrue(await _vm.ApplyAsync(CancellationToken.None));
            Assert.IsNotNull(generated);
            Assert.IsFalse(File.Exists(generated));
            Assert.IsTrue(_vm.CanConfigure);
            Assert.AreEqual("original", File.ReadAllText(_source));
            _wallpapers.Verify(w => w.CreateBasicDataAsync(generated, FileType.FImage, It.IsAny<CancellationToken>(), false), Times.Once);
            _model.Verify(m => m.Dispose(), Times.Once);
        }
        [TestMethod]
        public async Task EnhancementFailure_KeepsSourceAndAllowsApplyingWithoutEnhancement() {
            _vm.UseEnhancement = true;
            _model.Setup(m => m.LoadModel(null)).Throws(new IOException("model unavailable"));
            Assert.IsFalse(await _vm.ApplyAsync(CancellationToken.None));
            Assert.IsFalse(_vm.IsBusy);
            _wallpapers.Verify(w => w.CreateBasicDataAsync(It.IsAny<string>(), It.IsAny<FileType>(), It.IsAny<CancellationToken>(), false), Times.Never);
            Assert.AreEqual("original", File.ReadAllText(_source));
            _vm.UseEnhancement = false;
            Assert.IsTrue(await _vm.ApplyAsync(CancellationToken.None));
        }
        [TestMethod]
        public async Task CancellationAndTopologyChanges_DoNotApplyWallpaper() {
            using var cts = new CancellationTokenSource(); cts.Cancel();
            Assert.IsFalse(await _vm.ApplyAsync(cts.Token));
            _monitor.Bounds = new Rectangle(0, 0, 2560, 1440);
            Assert.IsFalse(await _vm.ApplyAsync(CancellationToken.None));
            _wallpapers.Verify(w => w.CreateBasicDataAsync(It.IsAny<string>(), It.IsAny<FileType>(), It.IsAny<CancellationToken>(), false), Times.Never);
        }
        [TestMethod]
        public async Task CancellationDuringEnhancement_DoesNotImportOrApply() {
            using var cts = new CancellationTokenSource();
            _vm.UseEnhancement = true;
            _model.Setup(m => m.RunAndSave(_source, It.IsAny<string>(), 1920, 1920, It.IsAny<CancellationToken>()))
                .Returns((string input, string output, uint width, uint height, CancellationToken ct) => {
                    File.WriteAllText(output, "partial"); cts.Cancel(); ct.ThrowIfCancellationRequested(); return output;
                });
            Assert.IsFalse(await _vm.ApplyAsync(cts.Token));
            Assert.IsFalse(_vm.IsBusy);
            _wallpapers.Verify(w => w.CreateBasicDataAsync(It.IsAny<string>(), It.IsAny<FileType>(), It.IsAny<CancellationToken>(), false), Times.Never);
            Assert.AreEqual("original", File.ReadAllText(_source));
        }
        [TestMethod]
        public async Task DisplayChangesDuringImport_DoNotApplyToAnotherDisplay() {
            _wallpapers.Setup(w => w.GetPlayerStartArgsAsync(It.IsAny<IWpBasicData>(), RuntimeType.RImage, null, It.IsAny<CancellationToken>(), null))
                .ReturnsAsync(() => { _monitor.IsStale = true; return JsonSerializer.Serialize(_args, StartArgsWebContext.Default.StartArgsWeb); });
            Assert.IsFalse(await _vm.ApplyAsync(CancellationToken.None));
            _wallpapers.Verify(w => w.SetWallpaperAsync(It.IsAny<IMonitor>(), It.IsAny<IWpBasicData>(), It.IsAny<RuntimeType>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        [TestMethod]
        public void ExistingLayoutsArePreserved_AndLargeImagesDoNotEnableUnnecessaryEnhancement() {
            _vm.SetSource(_source, 3840, 2160);
            _vm.UseEnhancement = true;
            Assert.IsFalse(_vm.UseEnhancement);
            _settings.Object.WallpaperArrangement = WallpaperArrangement.Expand;
            Assert.IsFalse(_vm.CanApply);
            Assert.AreEqual(WallpaperArrangement.Expand, _settings.Object.WallpaperArrangement);
        }
    }
}
