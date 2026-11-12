using System.Runtime.InteropServices;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using Workloads.Creation.StaticImg;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Creation.StaticImg.Models.Specific;
using Windows.UI;

namespace StaticImg.Test.T_StaticImg;

[TestClass]
[DoNotParallelize]
public class ProjectPersistenceTests {
    private string _root = null!, _path = null!, _oldRoot = null!;
    private bool _oldTestMode;
    private readonly List<InkProjectSession> _sessions = [];
    private readonly List<InkRenderData> _renders = [];
    private readonly List<LayerInfo> _infos = [];
    private static readonly ArcSize Size = new(3, 2, 96, RebuildMode.None);

    [TestInitialize]
    public void Initialize() {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.VpdTests-" + Guid.NewGuid().ToString("N"))).FullName;
        _path = Path.Combine(_root, "project.vpd");
        _oldRoot = Constants.CommonPaths.TestRootDir; _oldTestMode = Constants.IsTestMode;
        Constants.IsTestMode = true; Constants.CommonPaths.TestRootDir = Path.Combine(_root, "profile");
        Assert.IsTrue(FileUtil.IsPathWithinDirectory(_root, Constants.CommonPaths.AppDataDir));
    }

    [TestCleanup]
    public void Cleanup() {
        foreach (var info in _infos) { info.IsDeleted = true; info.RenderData = null!; }
        foreach (var render in _renders) render.Dispose();
        foreach (var session in _sessions) session.Dispose();
        Constants.CommonPaths.TestRootDir = _oldRoot; Constants.IsTestMode = _oldTestMode;
        Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task MultipleLayers_SaveAndReopen_PreservePixelsVisibilityOrderAndBusinessData() {
        var session = Session();
        var layers = Layers(session);
        var business = Business(1);
        Assert.IsTrue((await session.DesignFileUtil.SaveAsync(Size, business, layers)).Success);
        var loaded = await LoadAsync();
        Assert.IsNotNull(loaded.Header);
        Assert.AreEqual(2, loaded.Header.Value.LayerCount);
        Assert.AreEqual(FileHeader.CurrentVersion, loaded.Header.Value.Version);
        Assert.AreEqual(File.ReadAllBytes(_path).LongLength, loaded.Header.Value.GetTotalFileSize());
        AssertLayers(layers, loaded.Layers!);
        AssertBusiness(business, loaded.Business!);
    }

    [TestMethod]
    public async Task BusinessOnlySave_ChangesItsOffsetAndPreservesExactLayerBytes() {
        var session = Session();
        Assert.IsTrue((await session.DesignFileUtil.SaveAsync(Size, Business(), Layers(session))).Success);
        var old = session.DesignFileUtil.FileHeaderCache;
        byte[] before = File.ReadAllBytes(_path)[(int)old.LayersOffset..];
        var changed = Business(1, 8);
        Assert.IsTrue(await session.DesignFileUtil.SaveBusinessDataAsync(changed));
        var next = session.DesignFileUtil.FileHeaderCache;
        Assert.AreNotEqual(old.LayersOffset, next.LayersOffset);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(_path)[(int)next.LayersOffset..]);
        AssertBusiness(changed, (await LoadAsync()).Business!);
    }

    [TestMethod]
    public async Task LayersOnlySave_PreservesBusinessBytesAndReopensNewLayerPixels() {
        var session = Session();
        var business = Business(1, 5);
        Assert.IsTrue((await session.DesignFileUtil.SaveAsync(Size, business, Layers(session))).Success);
        byte[] before = File.ReadAllBytes(_path);
        var header = session.DesignFileUtil.FileHeaderCache;
        var changed = Layers(session);
        changed[0].RenderData.RenderTarget.SetPixelBytes(Pixels(71));
        Assert.IsTrue(await session.DesignFileUtil.SaveLayersAsync(changed));
        byte[] after = File.ReadAllBytes(_path);
        CollectionAssert.AreEqual(before[(int)header.BusinessDataOffset..(int)header.LayersOffset],
            after[(int)header.BusinessDataOffset..(int)header.LayersOffset]);
        var loaded = await LoadAsync();
        AssertBusiness(business, loaded.Business!);
        AssertLayers(changed, loaded.Layers!);
    }

    [TestMethod]
    public async Task LegacyV1_BusinessSaveRetainsLayerEncoding_AndLayerSaveUpgradesToV2WithoutPixelLoss() {
        var session = Session();
        var layers = Layers(session);
        var business = Business(1);
        byte[] data = BusinessData.Serialize(business);
        await using (var file = File.Create(_path)) {
            await file.WriteAsync(new byte[Marshal.SizeOf<FileHeader>()]);
            await file.WriteAsync(data);
            long length = await Layer.SerializeToStreamAsync(layers, file, FileHeader.Version1);
            var header = FileHeader.Create(Size, layers.Count, (uint)data.Length, (uint)length);
            header.Version = FileHeader.Version1;
            byte[] bytes = HeaderBytes(header);
            file.Position = 0; await file.WriteAsync(bytes);
        }
        var read = Session();
        var legacy = await read.DesignFileUtil.LoadAsync(read);
        Track(legacy.layers);
        AssertLayers(layers, legacy.layers!);
        byte[] layerBytes = File.ReadAllBytes(_path)[(int)legacy.fileHeader!.Value.LayersOffset..];
        Assert.IsTrue(await read.DesignFileUtil.SaveBusinessDataAsync(Business(0, 5)));
        Assert.AreEqual(FileHeader.Version1, read.DesignFileUtil.FileHeaderCache.Version);
        CollectionAssert.AreEqual(layerBytes, File.ReadAllBytes(_path)[(int)read.DesignFileUtil.FileHeaderCache.LayersOffset..]);
        Assert.IsTrue(await read.DesignFileUtil.SaveLayersAsync(legacy.layers!));
        var upgraded = await LoadAsync();
        Assert.AreEqual(FileHeader.Version2, upgraded.Header!.Value.Version);
        AssertLayers(layers, upgraded.Layers!);
    }

    [TestMethod]
    [DataRow("all")]
    [DataRow("business")]
    [DataRow("layers")]
    public async Task LockedDestination_FailedSavePreservesOriginalBytesCacheAndRemovesStaging(string mode) {
        var session = Session();
        var layers = Layers(session);
        Assert.IsTrue((await session.DesignFileUtil.SaveAsync(Size, Business(), layers)).Success);
        byte[] original = File.ReadAllBytes(_path);
        var header = session.DesignFileUtil.FileHeaderCache;
        using (var locked = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            bool saved = mode switch {
                "business" => await session.DesignFileUtil.SaveBusinessDataAsync(Business(1, 6)),
                "layers" => await session.DesignFileUtil.SaveLayersAsync(layers.Take(1).ToList()),
                _ => (await session.DesignFileUtil.SaveAsync(Size, Business(1, 6), layers.Take(1).ToList())).Success
            };
            Assert.IsFalse(saved);
        }
        CollectionAssert.AreEqual(original, File.ReadAllBytes(_path));
        Assert.AreEqual(header.LayerCount, session.DesignFileUtil.FileHeaderCache.LayerCount);
        Assert.AreEqual(header.LayersOffset, session.DesignFileUtil.FileHeaderCache.LayersOffset);
        Assert.IsEmpty(Directory.GetFiles(Constants.CommonPaths.TempDir));
        Assert.IsTrue((await session.DesignFileUtil.SaveAsync(Size, Business(), layers)).Success, "The I/O lock must be released for retry.");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task DamagedLayer_RecoveryNeverAllowsOverwritingTheOriginal(bool firstLayer) {
        var session = Session();
        var layers = Layers(session);
        Assert.IsTrue((await session.DesignFileUtil.SaveAsync(Size, Business(), layers)).Success);
        byte[] damaged = File.ReadAllBytes(_path);
        int offset = (int)session.DesignFileUtil.FileHeaderCache.LayersOffset;
        if (!firstLayer) offset += 8 + BitConverter.ToInt32(damaged, offset + 4);
        damaged[offset] ^= 0xff;
        File.WriteAllBytes(_path, damaged);
        var reopened = Session();
        var loaded = await reopened.DesignFileUtil.LoadAsync(reopened);
        Track(loaded.layers);
        Assert.IsTrue(reopened.DesignFileUtil.IsLoadFailed, "Partial layer recovery must retain the overwrite guard.");
        Assert.IsFalse((await reopened.DesignFileUtil.SaveAsync(Size, Business(), layers)).Success);
        Assert.IsFalse(await reopened.DesignFileUtil.SaveBusinessDataAsync(Business()));
        Assert.IsFalse(await reopened.DesignFileUtil.SaveLayersAsync(layers));
        CollectionAssert.AreEqual(damaged, File.ReadAllBytes(_path));
        if (firstLayer) {
            Assert.HasCount(1, loaded.layers!);
            string recoveryPath = Path.Combine(_root, "recovered.vpd");
            reopened.DesignFileUtil.SetFilePath(recoveryPath);
            Assert.IsTrue((await reopened.DesignFileUtil.SaveAsync(Size, loaded.businessData!, loaded.layers!)).Success);
            CollectionAssert.AreEqual(damaged, File.ReadAllBytes(_path));
            using var recovery = new InkProjectSession(recoveryPath, FileType.FImage);
            var read = await recovery.DesignFileUtil.LoadAsync(recovery);
            Track(read.layers);
            AssertLayers(loaded.layers!, read.layers!);
        }
    }

    [TestMethod]
    public async Task FailedCanvasSave_DoesNotMarkChangesSaved_AndRetryCanPersistThem() {
        var session = Session();
        var canvas = Canvas(session);
        bool? isSaved = null;
        session.IsSavedChanged += (_, e) => isSaved = e.IsSaved;
        Assert.IsTrue((await session.DesignFileUtil.SaveAsync(Size, Business(), Layers(session))).Success);
        Assert.IsTrue((await canvas.SaveAsync(session)).Success);
        Assert.AreEqual(true, isSaved);
        Track(canvas.AddLayer("Unsaved addition"));
        Assert.AreEqual(false, isSaved);
        byte[] original = File.ReadAllBytes(_path);
        using (var locked = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            Assert.IsFalse((await canvas.SaveAsync(session)).Success);
        }
        Assert.AreEqual(false, isSaved, "A failed write must keep the close/save warning active.");
        CollectionAssert.AreEqual(original, File.ReadAllBytes(_path));
        Assert.IsTrue((await canvas.SaveAsync(session)).Success);
        Assert.AreEqual(true, isSaved);
        Assert.AreEqual(3, (await LoadAsync()).Layers!.Count);
    }

    [TestMethod]
    public async Task DeleteUndoRedo_SaveAndReopen_PreserveLayerPixelsAndIdentityInMemory() {
        var session = Session();
        var canvas = Canvas(session);
        var selected = canvas.Layers[1];
        byte[] pixels = selected.RenderData.RenderTarget.GetPixelBytes();
        canvas.DeleteLayer(selected.Tag);
        Assert.AreEqual(1, canvas.Layers.Count);
        await session.UnReUtil.UndoAsync();
        Assert.AreSame(selected, canvas.Layers[1]);
        CollectionAssert.AreEqual(pixels, selected.RenderData.RenderTarget.GetPixelBytes());
        Assert.IsTrue((await session.DesignFileUtil.SaveAsync(Size, Business(), layers: canvas.Layers.Select(ToLayer).ToList())).Success);
        Assert.AreEqual(2, (await LoadAsync()).Layers!.Count);
        await session.UnReUtil.RedoAsync();
        Assert.AreEqual(1, canvas.Layers.Count);
        Assert.IsTrue((await canvas.SaveAsync(session)).Success);
        var loaded = await LoadAsync();
        Assert.AreEqual(1, loaded.Layers!.Count);
        CollectionAssert.AreEqual(canvas.Layers[0].RenderData.RenderTarget.GetPixelBytes(), loaded.Layers[0].RenderData.RenderTarget.GetPixelBytes());
    }

    [TestMethod]
    public async Task RotateUndoRedo_PreservesExactPixels_AndSavedProjectUsesRotatedDimensions() {
        var session = Session();
        var canvas = Canvas(session);
        byte[] original = canvas.Layers[0].RenderData.RenderTarget.GetPixelBytes();
        byte[] expected = new byte[original.Length];
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
                Array.Copy(original, (y * 3 + x) * 4, expected, (x * 2 + 1 - y) * 4, 4);
        var command = new LayerTransformCommand(canvas, Size, new ArcSize(2, 3, 96, RebuildMode.RotateRight), _ => { });
        await command.ExecuteAsync();
        CollectionAssert.AreEqual(expected, canvas.Layers[0].RenderData.RenderTarget.GetPixelBytes());
        await command.UndoAsync();
        CollectionAssert.AreEqual(original, canvas.Layers[0].RenderData.RenderTarget.GetPixelBytes());
        await command.ExecuteAsync();
        Assert.IsTrue((await session.DesignFileUtil.SaveAsync(canvas.CanvasSize, Business(), canvas.Layers.Select(ToLayer).ToList())).Success);
        var loaded = await LoadAsync();
        Assert.AreEqual(2f, loaded.Header!.Value.CanvasWidth);
        Assert.AreEqual(3f, loaded.Header.Value.CanvasHeight);
        CollectionAssert.AreEqual(expected, loaded.Layers![0].RenderData.RenderTarget.GetPixelBytes());
    }

    private InkProjectSession Session() { var session = new InkProjectSession(_path, FileType.FImage); _sessions.Add(session); return session; }
    private InkCanvasData Canvas(InkProjectSession session) {
        var canvas = new InkCanvasData(session, null!) { CanvasSize = Size };
        foreach (int i in Enumerable.Range(0, 2)) {
            var layer = canvas.AddLayer("Layer " + i, needRecord: false);
            Track(layer); layer.RenderData.RenderTarget.SetPixelBytes(Pixels((byte)(i * 31)));
        }
        return canvas;
    }
    private void Track(LayerInfo info) { _infos.Add(info); _renders.Add(info.RenderData); }
    private void Track(List<Layer>? layers) { if (layers != null) _renders.AddRange(layers.Select(l => l.RenderData)); }
    private List<Layer> Layers(InkProjectSession session) => [MakeLayer(session, "背景", true, 0), MakeLayer(session, "Hidden layer", false, 1)];
    private Layer MakeLayer(InkProjectSession session, string name, bool visible, int z) {
        var render = new InkRenderData(session, Size); _renders.Add(render);
        render.RenderTarget.SetPixelBytes(Pixels((byte)(z * 31)));
        return new Layer(name, new LayerState { IsVisible = visible, ZIndex = z }, render);
    }
    private static Layer ToLayer(LayerInfo info) => new(info.Name, new LayerState { IsVisible = info.IsVisible, ZIndex = info.ZIndex }, info.RenderData);
    private async Task<(FileHeader? Header, BusinessData? Business, List<Layer>? Layers)> LoadAsync() {
        var session = Session(); var result = await session.DesignFileUtil.LoadAsync(session); Track(result.layers); return result;
    }
    private static BusinessData Business(int selected = 0, int colors = 2) {
        var data = new BusinessData { SelectedLayerIndex = selected };
        data.SetColors(Enumerable.Range(0, colors).Select(i => Color.FromArgb(255, (byte)(i * 20), 60, 90))); return data;
    }
    private static byte[] Pixels(byte offset = 0) => Enumerable.Range(0, 6).SelectMany(i => new byte[] { (byte)(i * 17 + offset), (byte)(i * 13), (byte)(i * 7), 255 }).ToArray();
    private static void AssertBusiness(BusinessData expected, BusinessData actual) {
        Assert.AreEqual(expected.SelectedLayerIndex, actual.SelectedLayerIndex); CollectionAssert.AreEqual(expected.Colors.ToArray(), actual.Colors.ToArray());
    }
    private static void AssertLayers(List<Layer> expected, List<Layer> actual) {
        Assert.AreEqual(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++) {
            Assert.AreEqual(expected[i].Name, actual[i].Name); Assert.AreEqual(expected[i].State, actual[i].State);
            CollectionAssert.AreEqual(expected[i].RenderData.RenderTarget.GetPixelBytes(), actual[i].RenderData.RenderTarget.GetPixelBytes());
        }
    }
    private static byte[] HeaderBytes(FileHeader header) {
        int length = Marshal.SizeOf<FileHeader>(); var memory = Marshal.AllocHGlobal(length);
        try { Marshal.StructureToPtr(header, memory, false); var bytes = new byte[length]; Marshal.Copy(memory, bytes, 0, length); return bytes; }
        finally { Marshal.FreeHGlobal(memory); }
    }
}
