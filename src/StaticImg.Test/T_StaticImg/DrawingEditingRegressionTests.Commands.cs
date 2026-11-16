using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.UndoRedo;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Workloads.Creation.StaticImg;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;
using Workloads.Creation.StaticImg.Models;

namespace StaticImg.Test.T_StaticImg;

public partial class DrawingEditingRegressionTests {
    [TestMethod]
    [DataRow(96, false)]
    [DataRow(96, true)]
    [DataRow(144, true)]
    [DataRow(192, true)]
    public async Task RegionSnapshot_ChangesOnlyItsRectangle_AndRoundTripsAfterDiskSpill(int dpi, bool compressed) {
        var vm = Editable((uint)dpi);
        var layer = vm.Data.SelectedLayer;
        var target = layer.RenderData.RenderTarget;
        byte[] original = Pattern(target);
        target.SetPixelBytes(original);
        var region = new Rect(1, 0, 1, 1);
        byte[] before = ExtractPixels(original, (int)target.SizeInPixels.Width, region);
        byte[] after = [201, 99, 51, 255];
        var notifications = new List<Rect>();
        using var command = new RegionPixelSnapshotCommand(layer.Tag, vm.Data, region,
            compressed ? before.CompressPixels() : before,
            compressed ? after.CompressPixels() : after, compressed, "region regression", notifications.Add);
        using var store = new UndoDiskStore("region-roundtrip", _root);
        await AssertCommandSpills(command, store);
        byte[] expected = (byte[])original.Clone();
        PastePixels(expected, (int)target.SizeInPixels.Width, region, after);
        for (int repeat = 0; repeat < 2; repeat++) {
            await command.ExecuteAsync();
            CollectionAssert.AreEqual(expected, target.GetPixelBytes());
            await command.UndoAsync();
            CollectionAssert.AreEqual(original, target.GetPixelBytes());
        }
        CollectionAssert.AreEqual(Enumerable.Repeat(region, 4).ToArray(), notifications);
        command.Dispose();
        Assert.IsEmpty(Directory.GetFiles(store.SessionDirectory, "*.undo"));
    }

    [TestMethod]
    public async Task RegionSnapshot_ResolvesTheCurrentRenderTarget_AndIgnoresARemovedLayer() {
        var vm = Editable(144);
        var layer = vm.Data.SelectedLayer;
        using var previous = layer.RenderData;
        byte[] original = Pattern(previous.RenderTarget);
        previous.RenderTarget.SetPixelBytes(original);
        var region = new Rect(0, 0, 1, 1);
        byte[] before = ExtractPixels(original, (int)previous.RenderTarget.SizeInPixels.Width, region);
        byte[] after = [90, 30, 10, 255];
        using var command = new RegionPixelSnapshotCommand(layer.Tag, vm.Data, region, before, after, false, "rebuild", _ => { });
        await command.ExecuteAsync();
        byte[] previousPixels = previous.RenderTarget.GetPixelBytes();
        layer.RenderData = new InkRenderData(vm.Session, vm.Data.CanvasSize);
        layer.RenderData.RenderTarget.SetPixelBytes(original);
        await command.ExecuteAsync();
        var expected = (byte[])original.Clone();
        PastePixels(expected, (int)layer.RenderData.RenderTarget.SizeInPixels.Width, region, after);
        CollectionAssert.AreEqual(expected, layer.RenderData.RenderTarget.GetPixelBytes());
        await command.UndoAsync();
        CollectionAssert.AreEqual(original, layer.RenderData.RenderTarget.GetPixelBytes());
        CollectionAssert.AreEqual(previousPixels, previous.RenderTarget.GetPixelBytes());
        using var removed = layer.RenderData;
        vm.Data.Layers.Remove(layer);
        var unrelated = vm.Data.AddLayer("Unrelated", needRecord: false);
        byte[] unrelatedPixels = unrelated.RenderData.RenderTarget.GetPixelBytes();
        await command.ExecuteAsync();
        await command.UndoAsync();
        CollectionAssert.AreEqual(unrelatedPixels, unrelated.RenderData.RenderTarget.GetPixelBytes());
    }

    [TestMethod]
    public async Task RegionSnapshot_MissingDiskPayload_DoesNotChangePixelsOrPublishARender() {
        var vm = Editable(96);
        var layer = vm.Data.SelectedLayer;
        byte[] original = layer.RenderData.RenderTarget.GetPixelBytes();
        int renders = 0;
        using var command = new RegionPixelSnapshotCommand(layer.Tag, vm.Data, new Rect(0, 0, 1, 1),
            [1, 2, 3, 255], [7, 8, 9, 255], false, "missing payload", _ => renders++);
        using var store = new UndoDiskStore("missing-region", _root);
        await AssertCommandSpills(command, store);
        foreach (string path in Directory.GetFiles(store.SessionDirectory, "*.undo")) File.Delete(path);
        await Assert.ThrowsAsync<IOException>(() => command.ExecuteAsync());
        await Assert.ThrowsAsync<IOException>(() => command.UndoAsync());
        CollectionAssert.AreEqual(original, layer.RenderData.RenderTarget.GetPixelBytes());
        Assert.AreEqual(0, renders);
    }

    [TestMethod]
    [DataRow(96, true)]
    [DataRow(144, true)]
    [DataRow(192, true)]
    [DataRow(96, false)]
    [DataRow(192, false)]
    public async Task MoveSelection_RestoresSourceAndDestination_IncludingOverlapAndDiskReplay(int dpi, bool overlap) {
        var vm = Editable((uint)dpi);
        var layer = vm.Data.SelectedLayer;
        var target = layer.RenderData.RenderTarget;
        int width = (int)target.SizeInPixels.Width;
        byte[] original = Pattern(target);
        target.SetPixelBytes(original);
        var from = new Rect(0, 0, 2, 1);
        var to = new Rect(1, overlap ? 0 : 1, 2, 1);
        byte[] selection = ExtractPixels(original, width, from);
        byte[] destination = ExtractPixels(original, width, to);
        var notifications = new List<Rect>();
        using var command = new LayerSelectionCommand(layer.Tag, vm.Data, from, to,
            selection.CompressPixels(), destination.CompressPixels(), selection.CompressPixels(), notifications.Add);
        using var store = new UndoDiskStore("selection-roundtrip", _root);
        await AssertCommandSpills(command, store);
        byte[] expected = (byte[])original.Clone();
        PastePixels(expected, width, from, new byte[selection.Length]);
        PastePixels(expected, width, to, selection);
        for (int repeat = 0; repeat < 2; repeat++) {
            await command.ExecuteAsync();
            CollectionAssert.AreEqual(expected, target.GetPixelBytes());
            await command.UndoAsync();
            CollectionAssert.AreEqual(original, target.GetPixelBytes());
        }
        var dirty = new Rect(0, 0, 3, overlap ? 1 : 2);
        CollectionAssert.AreEqual(Enumerable.Repeat(dirty, 4).ToArray(), notifications);
        using var removed = layer.RenderData;
        vm.Data.Layers.Remove(layer);
        var other = vm.Data.AddLayer("Other", needRecord: false);
        byte[] otherPixels = other.RenderData.RenderTarget.GetPixelBytes();
        int rendersBefore = notifications.Count;
        await command.ExecuteAsync();
        await command.UndoAsync();
        Assert.HasCount(rendersBefore, notifications);
        CollectionAssert.AreEqual(otherPixels, other.RenderData.RenderTarget.GetPixelBytes());
        command.Dispose();
        Assert.IsEmpty(Directory.GetFiles(store.SessionDirectory, "*.undo"));
    }

    [TestMethod]
    [DataRow(96)]
    [DataRow(144)]
    [DataRow(192)]
    public async Task RebuildLayers_CropsAndRestoresExactSnapshots_AndSkipsRemovedLayers(int dpi) {
        var vm = Editable((uint)dpi);
        var second = vm.Data.AddLayer("Second", needRecord: false);
        foreach (var layer in vm.Data.Layers) {
            byte[] pattern = Pattern(layer.RenderData.RenderTarget);
            if (layer == second) for (int i = 0; i < pattern.Length; i += 4) pattern[i] ^= 0x7f;
            layer.RenderData.RenderTarget.SetPixelBytes(pattern);
        }
        var originals = vm.Data.Layers.ToDictionary(layer => layer.Tag, layer => layer.RenderData.RenderTarget.GetPixelBytes());
        int width = (int)second.RenderData.RenderTarget.SizeInPixels.Width;
        var originalSize = vm.Data.CanvasSize;
        var croppedSize = new ArcSize(2, 1, (uint)dpi, RebuildMode.ResizeExpand);
        // Snapshots contain physical pixels; ArcSize dimensions are DIPs.
        using var reference = new CanvasRenderTarget(_device, croppedSize.Width, croppedSize.Height, croppedSize.Dpi);
        var region = new Rect(1, 0, reference.SizeInPixels.Width, reference.SizeInPixels.Height);
        var crops = originals.ToDictionary(pair => pair.Key, pair => ExtractPixels(pair.Value, width, region));
        int renders = 0;
        using var command = new LayerRebuildCommand(vm.Data, originalSize, croppedSize,
            originals.ToDictionary(pair => pair.Key, pair => pair.Value.CompressPixels()),
            crops.ToDictionary(pair => pair.Key, pair => pair.Value.CompressPixels()), () => renders++);
        using var store = new UndoDiskStore("rebuild-roundtrip", _root);
        await AssertCommandSpills(command, store);
        for (int repeat = 0; repeat < 2; repeat++) {
            await command.ExecuteAsync();
            Assert.AreEqual(croppedSize, vm.Data.CanvasSize);
            foreach (var layer in vm.Data.Layers) CollectionAssert.AreEqual(crops[layer.Tag], layer.RenderData.RenderTarget.GetPixelBytes());
            await command.UndoAsync();
            Assert.AreEqual(originalSize, vm.Data.CanvasSize);
            foreach (var layer in vm.Data.Layers) CollectionAssert.AreEqual(originals[layer.Tag], layer.RenderData.RenderTarget.GetPixelBytes());
        }
        Assert.AreEqual(4, renders);
        using var removed = second.RenderData;
        vm.Data.Layers.Remove(second);
        await command.ExecuteAsync();
        await command.UndoAsync();
        foreach (var layer in vm.Data.Layers) CollectionAssert.AreEqual(originals[layer.Tag], layer.RenderData.RenderTarget.GetPixelBytes());
        CollectionAssert.AreEqual(originals[second.Tag], removed.RenderTarget.GetPixelBytes());
        command.Dispose();
        command.Dispose();
        Assert.IsEmpty(Directory.GetFiles(store.SessionDirectory, "*.undo"));
    }

    private static async Task AssertCommandSpills(IMemoryAwareUndoableCommand command, UndoDiskStore store) {
        long before = command.EstimatedMemoryBytes;
        var disk = (IDiskSpillableUndoCommand)command;
        Assert.IsTrue(await disk.TrySpillToDiskAsync(store, CancellationToken.None));
        Assert.IsGreaterThan(0L, disk.DiskStorageBytes);
        Assert.IsLessThan(before, command.EstimatedMemoryBytes);
    }

    private static byte[] ExtractPixels(byte[] image, int width, Rect region) {
        int rowBytes = (int)region.Width * 4;
        var result = new byte[rowBytes * (int)region.Height];
        for (int y = 0; y < region.Height; y++) Buffer.BlockCopy(image, (((int)region.Y + y) * width + (int)region.X) * 4, result, y * rowBytes, rowBytes);
        return result;
    }

    private static void PastePixels(byte[] image, int width, Rect region, byte[] pixels) {
        int rowBytes = (int)region.Width * 4;
        for (int y = 0; y < region.Height; y++) Buffer.BlockCopy(pixels, y * rowBytes, image, (((int)region.Y + y) * width + (int)region.X) * 4, rowBytes);
    }
}
