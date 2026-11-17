using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.UndoRedo;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.ToolItems;

namespace StaticImg.Test.T_StaticImg;

public partial class DrawingEditingRegressionTests {
    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(7, 0)]
    [DataRow(0, 4)]
    [DataRow(4, 3)]
    [DataRow(2, 1)]
    [DataRow(-100, -10)]
    [DataRow(100, 100)]
    public void Fill_ChangesOnlyTheConnectedRegion_AndPreservesHolesInUndoData(int startX, int startY) {
        string[] mask = ["AAAABAAA", "ABABBABA", "AAAABAAA", "BBBBBBBB", "AAAABAAA"];
        using var target = new CanvasRenderTarget(_device, mask[0].Length, mask.Length, 96);
        byte[] original = mask.SelectMany(row => row.SelectMany(value => value == 'A'
            ? new byte[] { 10, 20, 30, 255 } : new byte[] { 90, 80, 70, 255 })).ToArray();
        target.SetPixelBytes(original);
        Color color = Color.FromArgb(255, 211, 123, 31);
        var start = new Point(startX, startY);
        var expected = FloodFillReference(original, mask[0].Length, mask.Length, start, NativePixel(color));
        var result = ComputeFill(start, color, target);
        AssertFillSnapshot(target, original, expected.Pixels, expected.Region, result);
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(1, 17)]
    [DataRow(17, 1)]
    public void Fill_HandlesSinglePixelAndSingleAxisImages(int width, int height) {
        using var target = new CanvasRenderTarget(_device, width, height, 96);
        using (var drawing = target.CreateDrawingSession()) drawing.Clear(Color.FromArgb(255, 3, 7, 11));
        byte[] original = target.GetPixelBytes();
        Color color = Color.FromArgb(255, 29, 59, 89);
        var expected = FloodFillReference(original, width, height, new Point(width - 0.25, height - 0.25), NativePixel(color));
        AssertFillSnapshot(target, original, expected.Pixels, expected.Region,
            ComputeFill(new Point(width - 0.25, height - 0.25), color, target));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(64)]
    [DataRow(128)]
    [DataRow(254)]
    [DataRow(255)]
    public void Fill_StoresTheSamePremultipliedPixelsAsNativeDrawing(int alpha) {
        using var target = new CanvasRenderTarget(_device, 3, 2, 96);
        using (var drawing = target.CreateDrawingSession()) drawing.Clear(Color.FromArgb(255, 1, 2, 3));
        byte[] original = target.GetPixelBytes();
        Color color = Color.FromArgb((byte)alpha, 199, 101, 51);
        byte[] pixel = NativePixel(color);
        var expected = Enumerable.Range(0, 6).SelectMany(_ => pixel).ToArray();
        AssertFillSnapshot(target, original, expected, new Rect(0, 0, 3, 2), ComputeFill(new Point(0, 0), color, target));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(64)]
    [DataRow(128)]
    [DataRow(254)]
    [DataRow(255)]
    public void Fill_IdenticalEffectiveColor_DoesNotCreateAnUndoPayload(int alpha) {
        using var target = new CanvasRenderTarget(_device, 3, 2, 96);
        Color color = Color.FromArgb((byte)alpha, 199, 101, 51);
        using (var drawing = target.CreateDrawingSession()) drawing.Clear(color);
        byte[] original = target.GetPixelBytes();
        Assert.IsNull(ComputeFill(new Point(1, 1), color, target));
        CollectionAssert.AreEqual(original, target.GetPixelBytes());
    }

    [TestMethod]
    public void Fill_CompressesAndRestoresPayloadsLargerThanOneCompressionChunk() {
        using var target = new CanvasRenderTarget(_device, 1024, 1024, 96);
        using (var drawing = target.CreateDrawingSession()) drawing.Clear(Color.FromArgb(255, 11, 22, 33));
        byte[] original = target.GetPixelBytes();
        Color color = Color.FromArgb(255, 77, 88, 99);
        byte[] expected = new byte[original.Length];
        byte[] pixel = NativePixel(color);
        for (int offset = 0; offset < expected.Length; offset += 4) pixel.CopyTo(expected, offset);
        AssertFillSnapshot(target, original, expected, new Rect(0, 0, 1024, 1024), ComputeFill(new Point(512, 512), color, target));
    }

    [TestMethod]
    public void Fill_PreCanceledRequest_DoesNotReadEvenADisposedTarget() {
        var target = new CanvasRenderTarget(_device, 1, 1, 96);
        target.Dispose();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => ComputeFill(new Point(0, 0), Color.FromArgb(255, 1, 2, 3), target, cancellation.Token));
        Assert.AreEqual(cancellation.Token, error.CancellationToken);
    }

    [TestMethod]
    [DataRow(96)]
    [DataRow(144)]
    [DataRow(192)]
    public async Task Fill_PayloadSupportsExactUndoRedoAndDiskSpill(int dpi) {
        var vm = Editable((uint)dpi);
        var layer = vm.Data.SelectedLayer;
        var target = layer.RenderData.RenderTarget;
        byte[] original = Pattern(target);
        target.SetPixelBytes(original);
        Color color = Color.FromArgb(255, 17, 71, 131);
        var expected = FloodFillReference(original, (int)target.SizeInPixels.Width, (int)target.SizeInPixels.Height, new Point(1, 1), NativePixel(color));
        var result = ComputeFill(new Point(1, 1), color, target);
        AssertFillSnapshot(target, original, expected.Pixels, expected.Region, result);
        Assert.IsNotNull(result);
        var renders = new List<Rect>();
        using var command = new RegionPixelSnapshotCommand(layer.Tag, vm.Data, result.Region,
            result.CompressedOriginal, result.CompressedCurrent, true, "Fill", renders.Add);
        using var store = new UndoDiskStore("fill-roundtrip", _root);
        await AssertCommandSpills(command, store);
        for (int repeat = 0; repeat < 2; repeat++) {
            await command.ExecuteAsync();
            CollectionAssert.AreEqual(expected.Pixels, target.GetPixelBytes());
            await command.UndoAsync();
            CollectionAssert.AreEqual(original, target.GetPixelBytes());
        }
        CollectionAssert.AreEqual(Enumerable.Repeat(result.Region, 4).ToArray(), renders);
    }

    [TestMethod]
    public void Fill_NewerRequestCancelsTheOldOne_AndOldCompletionCannotClearTheNewRequest() {
        var vm = Editable(96);
        var layer = vm.Data.SelectedLayer;
        using var tool = new FillTool(vm.Data) { ViewModel = vm };
        var first = BeginFill(tool);
        var second = BeginFill(tool);
        try {
            Assert.IsGreaterThan(first.Version, second.Version);
            Assert.IsTrue(first.Cts.IsCancellationRequested);
            Assert.IsFalse(IsCurrentFill(tool, first, layer));
            Assert.IsTrue(IsCurrentFill(tool, second, layer));
            InvokeFill("CompleteFillRequest", tool, first.Cts);
            Assert.IsTrue(IsCurrentFill(tool, second, layer));
            InvokeFill("CompleteFillRequest", tool, second.Cts);
            Assert.IsFalse(IsCurrentFill(tool, second, layer));
        }
        finally { first.Cts.Dispose(); second.Cts.Dispose(); }
    }

    [TestMethod]
    [DataRow("removed")]
    [DataRow("deleted")]
    [DataRow("replaced")]
    public void Fill_ResultCannotCommitAfterItsLayerOrTargetChanges(string change) {
        var vm = Editable(96);
        var layer = vm.Data.SelectedLayer;
        using var tool = new FillTool(vm.Data) { ViewModel = vm };
        var request = BeginFill(tool);
        using var cancellation = request.Cts;
        var originalTarget = layer.RenderData.RenderTarget;
        using var originalData = change == "replaced" ? layer.RenderData : null;
        Assert.IsTrue(IsCurrentFill(tool, request, layer));
        if (change == "removed") vm.Data.Layers.Remove(layer);
        else if (change == "deleted") layer.IsDeleted = true;
        else layer.RenderData = new InkRenderData(vm.Session, vm.Data.CanvasSize);
        Assert.IsFalse((bool)InvokeFill("IsFillRequestCurrent", tool, request.Version, request.Cts, layer, originalTarget)!);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Fill_CancelAndDisposeInvalidatePendingResults(bool dispose) {
        var vm = Editable(96);
        var layer = vm.Data.SelectedLayer;
        using var tool = new FillTool(vm.Data) { ViewModel = vm };
        var request = BeginFill(tool);
        using var cancellation = request.Cts;
        if (dispose) tool.Dispose(); else tool.CancelPendingOperation();
        Assert.IsTrue(cancellation.IsCancellationRequested);
        Assert.IsFalse(IsCurrentFill(tool, request, layer));
        if (!dispose) {
            var next = BeginFill(tool);
            using var nextCancellation = next.Cts;
            Assert.IsTrue(IsCurrentFill(tool, next, layer));
            tool.CancelPendingOperation();
            Assert.IsTrue(nextCancellation.IsCancellationRequested);
        }
    }

    [TestMethod]
    public void Fill_CancellationToleratesARequestWhoseSourceWasAlreadyDisposed() {
        var vm = Editable(96);
        using var tool = new FillTool(vm.Data) { ViewModel = vm };
        var previous = BeginFill(tool);
        previous.Cts.Dispose();
        var next = BeginFill(tool);
        Assert.IsTrue(IsCurrentFill(tool, next, vm.Data.SelectedLayer));
        next.Cts.Dispose();
        tool.CancelPendingOperation();
        Assert.IsFalse(IsCurrentFill(tool, next, vm.Data.SelectedLayer));
    }

    private byte[] NativePixel(Color color) {
        using var reference = new CanvasRenderTarget(_device, 1, 1, 96);
        using (var drawing = reference.CreateDrawingSession()) drawing.Clear(color);
        return reference.GetPixelBytes();
    }

    private static void AssertFillSnapshot(CanvasRenderTarget target, byte[] original, byte[] expected, Rect expectedRegion, FillSnapshot? result) {
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedRegion, result.Region);
        int width = (int)target.SizeInPixels.Width;
        CollectionAssert.AreEqual(original, target.GetPixelBytes(), "Background computation must not write into the live layer.");
        CollectionAssert.AreEqual(ExtractPixels(original, width, expectedRegion), result.CompressedOriginal.DecompressPixels());
        CollectionAssert.AreEqual(result.Current, result.CompressedCurrent.DecompressPixels());
        var reconstructed = (byte[])original.Clone();
        PastePixels(reconstructed, width, result.Region, result.Current);
        CollectionAssert.AreEqual(expected, reconstructed);
    }

    // Independent four-neighbor BFS oracle; production uses a scanline algorithm.
    private static (byte[] Pixels, Rect Region) FloodFillReference(byte[] original, int width, int height, Point start, byte[] fill) {
        byte[] result = (byte[])original.Clone();
        int first = Math.Clamp((int)start.Y, 0, height - 1) * width + Math.Clamp((int)start.X, 0, width - 1);
        byte[] source = original.AsSpan(first * 4, 4).ToArray();
        var visited = new HashSet<int> { first };
        var pending = new Queue<int>();
        pending.Enqueue(first);
        while (pending.TryDequeue(out int pixel)) {
            fill.CopyTo(result, pixel * 4);
            int x = pixel % width, y = pixel / width;
            foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) }) {
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                int neighbor = ny * width + nx;
                if (original.AsSpan(neighbor * 4, 4).SequenceEqual(source) && visited.Add(neighbor)) pending.Enqueue(neighbor);
            }
        }
        int minX = visited.Min(value => value % width), minY = visited.Min(value => value / width);
        int maxX = visited.Max(value => value % width), maxY = visited.Max(value => value / width);
        return (result, new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
    }

    private static FillSnapshot? ComputeFill(Point start, Color color, CanvasRenderTarget target, CancellationToken token = default) {
        object? value = InvokeFill("CreateFillResult", null, start, color, target, token);
        if (value == null) return null;
        var type = value.GetType();
        return new FillSnapshot((Rect)type.GetProperty("DirtyRegion")!.GetValue(value)!,
            (byte[])type.GetProperty("CurrentPixels")!.GetValue(value)!,
            (byte[])type.GetProperty("CompressedOriginalPixels")!.GetValue(value)!,
            (byte[])type.GetProperty("CompressedCurrentPixels")!.GetValue(value)!);
    }

    // Keep private computation/request access here instead of widening the product API for tests.
    private static object? InvokeFill(string method, FillTool? tool, params object[] arguments) {
        try {
            return typeof(FillTool).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!.Invoke(tool, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null) {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static (long Version, CancellationTokenSource Cts) BeginFill(FillTool tool) =>
        ((long, CancellationTokenSource))InvokeFill("BeginFillRequest", tool)!;

    private static bool IsCurrentFill(FillTool tool, (long Version, CancellationTokenSource Cts) request, LayerInfo layer) =>
        (bool)InvokeFill("IsFillRequestCurrent", tool, request.Version, request.Cts, layer, layer.RenderData.RenderTarget)!;

    private sealed record FillSnapshot(Rect Region, byte[] Current, byte[] CompressedOriginal, byte[] CompressedCurrent);
}
