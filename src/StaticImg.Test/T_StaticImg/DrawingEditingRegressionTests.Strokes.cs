using System.Numerics;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Shader;
using Windows.UI;
using Workloads.Creation.StaticImg;
using Workloads.Creation.StaticImg.Core.Brushes;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.Extensions;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.ViewModels;

namespace StaticImg.Test.T_StaticImg;

public partial class DrawingEditingRegressionTests {
    [TestMethod]
    [DataRow(96)]
    [DataRow(144)]
    [DataRow(192)]
    [DataRow(300)]
    public async Task Stroke_PaintsTheSamePhysicalPixelsAsNativeDrawing_AndRoundTripsUndo(int dpi) {
        var vm = StrokeCanvas(dpi);
        var target = vm.Data.SelectedLayer.RenderData.RenderTarget;
        byte[] original = target.GetPixelBytes();
        var point = new Vector2(35, 28);
        var color = Color.FromArgb(255, 21, 137, 213);
        var stroke = CreateBrushStroke(vm, color, 10);
        using var reference = ReferenceTarget(target, original);
        using (var drawing = reference.CreateDrawingSession()) {
            drawing.Units = CanvasUnits.Pixels;
            drawing.FillCircle(point, 5, color);
        }
        using var drawer = new StrokeDrawer(vm);
        drawer.Start(stroke, point);
        drawer.Finish();
        CollectionAssert.AreEqual(reference.GetPixelBytes(), target.GetPixelBytes());
        Assert.IsTrue(vm.Session.UnReUtil.CanUndo);
        byte[] painted = target.GetPixelBytes();
        await vm.Session.UnReUtil.UndoAsync();
        CollectionAssert.AreEqual(original, target.GetPixelBytes());
        await vm.Session.UnReUtil.RedoAsync();
        CollectionAssert.AreEqual(painted, target.GetPixelBytes());
    }

    [TestMethod]
    [DataRow(96, 1f)]
    [DataRow(144, 1f)]
    [DataRow(192, 1f)]
    [DataRow(96, 0.5f)]
    [DataRow(144, 0.5f)]
    [DataRow(192, 0.5f)]
    public async Task Eraser_ChangesOnlyTheMask_AndPreservesPixelsThroughUndoRedo(int dpi, float opacity) {
        await ShaderLoader.LoadAllShadersAsync();
        var vm = StrokeCanvas(dpi);
        var target = vm.Data.SelectedLayer.RenderData.RenderTarget;
        byte[] original = target.GetPixelBytes();
        var stroke = new EffectWithCopyStroke(new BrushGenerateArgs(Color.FromArgb(255, 0, 0, 0), BrushType.Eraser, 12, Opacity: opacity));
        stroke.InitPixelsEffect(ShaderType.GeometryAlphaEraseEffect);
        using var drawer = new StrokeDrawer(vm);
        drawer.Start(stroke, new Vector2(35, 28));
        drawer.Finish();
        byte[] erased = target.GetPixelBytes();
        int width = (int)target.SizeInPixels.Width;
        int centerAlpha = erased[(28 * width + 35) * 4 + 3];
        Assert.AreEqual((1 - opacity) * 255, centerAlpha, 1);
        CollectionAssert.AreEqual(original[..4], erased[..4]);
        CollectionAssert.AreEqual(original[^4..], erased[^4..]);
        Assert.IsTrue(erased.Where((value, index) => value != original[index]).Any());
        await vm.Session.UnReUtil.UndoAsync();
        CollectionAssert.AreEqual(original, target.GetPixelBytes());
        await vm.Session.UnReUtil.RedoAsync();
        CollectionAssert.AreEqual(erased, target.GetPixelBytes());
    }

    [TestMethod]
    [DataRow(96)]
    [DataRow(144)]
    [DataRow(192)]
    public void LongOpaqueStroke_UsesBoundedActivePoints_AndMatchesOneContinuousLine(int dpi) {
        var vm = StrokeCanvas(dpi, 1200, 80);
        var target = vm.Data.SelectedLayer.RenderData.RenderTarget;
        var color = Color.FromArgb(255, 31, 173, 219);
        var stroke = CreateBrushStroke(vm, color, 8);
        using var reference = ReferenceTarget(target, target.GetPixelBytes());
        using (var drawing = reference.CreateDrawingSession()) {
            drawing.Units = CanvasUnits.Pixels;
            drawing.DrawLine(new Vector2(20, 30), new Vector2(1100, 30), stroke.InkBrush!, 8, stroke.Style);
        }
        using var drawer = new StrokeDrawer(vm);
        drawer.Start(stroke, new Vector2(20, 30));
        for (int x = 32; x <= 1100; x += 12) drawer.Append(new Vector2(x, 30));
        Assert.IsLessThan(34, drawer.ActivePointCount);
        drawer.Finish();
        CollectionAssert.AreEqual(reference.GetPixelBytes(), target.GetPixelBytes());
    }

    [TestMethod]
    [DataRow(96, 1f)]
    [DataRow(144, 0.5f)]
    [DataRow(192, 0.5f)]
    public async Task LongEraser_MatchesAContinuousNativeMask_AndRestoresTheOriginalLayer(int dpi, float opacity) {
        await ShaderLoader.LoadAllShadersAsync();
        var vm = StrokeCanvas(dpi, 1200, 80);
        var target = vm.Data.SelectedLayer.RenderData.RenderTarget;
        byte[] original = target.GetPixelBytes();
        var stroke = new EffectWithCopyStroke(new BrushGenerateArgs(Color.FromArgb(255, 0, 0, 0), BrushType.Eraser, 8, Opacity: opacity));
        stroke.InitPixelsEffect(ShaderType.GeometryAlphaEraseEffect);
        using var mask = ReferenceTarget(target, new byte[original.Length]);
        using (var drawing = mask.CreateDrawingSession()) {
            drawing.Units = CanvasUnits.Pixels;
            drawing.DrawLine(new Vector2(20, 30), new Vector2(1100, 30), Color.FromArgb(255, 255, 255, 255), 8, stroke.Style);
        }
        using var reference = ReferenceTarget(target, original);
        using (var effect = stroke.MergeImages(mask, target, target.Device))
        using (var drawing = reference.CreateDrawingSession()) {
            drawing.Units = CanvasUnits.Pixels;
            drawing.Blend = CanvasBlend.Copy;
            drawing.DrawImage(effect);
        }
        using var drawer = new StrokeDrawer(vm);
        drawer.Start(stroke, new Vector2(20, 30));
        for (int x = 32; x <= 1100; x += 12) drawer.Append(new Vector2(x, 30));
        Assert.IsLessThan(34, drawer.ActivePointCount);
        drawer.Finish();
        byte[] erased = target.GetPixelBytes();
        byte[] expected = reference.GetPixelBytes();
        Assert.HasCount(expected.Length, erased);
        // Separate antialiased blocks can round an 8-bit mask one level differently from a single line.
        // Keep every channel within one level; undo/redo below must still restore exact bytes.
        int maximumDifference = expected.Zip(erased, (left, right) => Math.Abs(left - right)).Max();
        Console.WriteLine($"Maximum 8-bit channel difference: {maximumDifference}");
        Assert.IsLessThanOrEqualTo(1, maximumDifference);
        await vm.Session.UnReUtil.UndoAsync();
        CollectionAssert.AreEqual(original, target.GetPixelBytes());
        await vm.Session.UnReUtil.RedoAsync();
        CollectionAssert.AreEqual(erased, target.GetPixelBytes());
    }

    [TestMethod]
    [DataRow(128, 1f)]
    [DataRow(255, 0.5f)]
    public void TranslucentStroke_DoesNotAccumulateColorBetweenFramesOrUseTheOpaqueCache(int alpha, float opacity) {
        var vm = StrokeCanvas(96, 160, 80);
        var target = vm.Data.SelectedLayer.RenderData.RenderTarget;
        var stroke = CreateBrushStroke(vm, Color.FromArgb((byte)alpha, 31, 173, 219), 8, opacity);
        using var reference = ReferenceTarget(target, target.GetPixelBytes());
        using (var drawing = reference.CreateDrawingSession()) {
            drawing.Units = CanvasUnits.Pixels;
            drawing.DrawLine(new Vector2(20, 30), new Vector2(120, 30), stroke.InkBrush!, 8, stroke.Style);
        }
        using var drawer = new StrokeDrawer(vm);
        drawer.Start(stroke, new Vector2(20, 30));
        for (int x = 22; x <= 120; x += 2) drawer.Append(new Vector2(x, 30));
        Assert.AreEqual(51, drawer.ActivePointCount);
        drawer.Finish();
        CollectionAssert.AreEqual(reference.GetPixelBytes(), target.GetPixelBytes());
    }

    [TestMethod]
    public async Task ConsecutiveStrokes_ClearThePreviousMask_AndUndoIndependently() {
        var vm = StrokeCanvas(96);
        var target = vm.Data.SelectedLayer.RenderData.RenderTarget;
        byte[] original = target.GetPixelBytes();
        using var drawer = new StrokeDrawer(vm);
        drawer.Start(CreateBrushStroke(vm, Color.FromArgb(255, 0, 0, 255), 10), new Vector2(20, 20));
        drawer.Finish();
        byte[] first = target.GetPixelBytes();
        var mask = drawer.Mask;
        drawer.Start(CreateBrushStroke(vm, Color.FromArgb(255, 255, 0, 0), 10), new Vector2(70, 20));
        drawer.Finish();
        Assert.AreSame(mask, drawer.Mask);
        byte[] second = target.GetPixelBytes();
        int firstPixel = (20 * (int)target.SizeInPixels.Width + 20) * 4;
        CollectionAssert.AreEqual(first.AsSpan(firstPixel, 4).ToArray(), second.AsSpan(firstPixel, 4).ToArray());
        CollectionAssert.AreNotEqual(first, second);
        await vm.Session.UnReUtil.UndoAsync();
        CollectionAssert.AreEqual(first, target.GetPixelBytes());
        await vm.Session.UnReUtil.UndoAsync();
        CollectionAssert.AreEqual(original, target.GetPixelBytes());
        await vm.Session.UnReUtil.RedoAsync();
        await vm.Session.UnReUtil.RedoAsync();
        CollectionAssert.AreEqual(second, target.GetPixelBytes());
    }

    [TestMethod]
    public void ChangingCanvasSizeAndDpi_RebuildsBothBuffers_WithoutOwningTheLayerTarget() {
        var vm = StrokeCanvas(96);
        using var drawer = new StrokeDrawer(vm);
        drawer.Start(CreateBrushStroke(vm, Color.FromArgb(255, 0, 0, 255), 10), new Vector2(20, 20));
        drawer.Finish();
        var oldMask = drawer.Mask;
        var oldSnapshot = drawer.Snapshot;
        using var previous = vm.Data.SelectedLayer.RenderData;
        vm.Data.CanvasSize = new ArcSize(110, 90, 144, RebuildMode.None);
        vm.Data.SelectedLayer.RenderData = new InkRenderData(vm.Session, vm.Data.CanvasSize);
        var target = vm.Data.SelectedLayer.RenderData.RenderTarget;
        drawer.Start(CreateBrushStroke(vm, Color.FromArgb(255, 255, 0, 0), 10), new Vector2(40, 30));
        drawer.Finish();
        Assert.AreNotSame(oldMask, drawer.Mask);
        Assert.AreNotSame(oldSnapshot, drawer.Snapshot);
        Assert.AreEqual(target.SizeInPixels, drawer.Mask.SizeInPixels);
        Assert.AreEqual(target.SizeInPixels, drawer.Snapshot.SizeInPixels);
        Assert.AreEqual(target.Dpi, drawer.Mask.Dpi);
        Assert.AreEqual(target.Dpi, drawer.Snapshot.Dpi);
        byte[] pixels = target.GetPixelBytes();
        drawer.Dispose();
        CollectionAssert.AreEqual(pixels, target.GetPixelBytes());
    }

    private InkCanvasViewModel StrokeCanvas(int dpi, int width = 96, int height = 64) {
        var vm = ViewModel(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".vpd"));
        vm.Data.CanvasSize = new ArcSize(width, height, (uint)dpi, RebuildMode.None);
        vm.Data.SelectedLayer = vm.Data.AddLayer("Drawing", needRecord: false);
        using var drawing = vm.Data.SelectedLayer.RenderData.RenderTarget.CreateDrawingSession();
        drawing.Clear(Color.FromArgb(255, 80, 100, 120));
        return vm;
    }

    private static BrushStroke CreateBrushStroke(InkCanvasViewModel vm, Color color, float thickness, float opacity = 1) {
        var args = new BrushGenerateArgs(color, BrushType.General, thickness, Opacity: opacity);
        return new BrushStroke(args) { InkBrush = vm.Session.Brushes.GetBrush(args) };
    }

    private static CanvasRenderTarget ReferenceTarget(CanvasRenderTarget target, byte[] original) {
        var reference = new CanvasRenderTarget(target.Device, (float)target.Size.Width, (float)target.Size.Height,
            target.Dpi, target.Format, target.AlphaMode);
        reference.SetPixelBytes(original);
        return reference;
    }

    // Expose protected drawing stages without constructing WinUI input or controls.
    private sealed class StrokeDrawer : CanvasPathDrawer {
        public StrokeDrawer(InkCanvasViewModel vm) {
            ViewModel = vm;
            FatalErrorOccurred += (_, error) => Assert.Fail(error.ToString());
        }
        public int ActivePointCount => CurrentStroke.Points.Count;
        public CanvasRenderTarget Mask => TempRenderTarget;
        public CanvasRenderTarget Snapshot => SnapshotRenderTarget;
        public void Start(StrokeBase stroke, Vector2 point) {
            CurrentStroke = stroke;
            LayerId = ViewModel.Data.SelectedLayer.Tag;
            InitDrawState(point);
            RenderToTarget();
        }
        public void Append(Vector2 point) { CurrentStroke.Points.Add(point); RenderToTarget(); }
        public void Finish() => EndDrawing();
        protected override void InitCurrentStroke(CanvasPointerEventArgs e) => throw new NotSupportedException("The headless fixture supplies stroke data directly.");
    }
}
