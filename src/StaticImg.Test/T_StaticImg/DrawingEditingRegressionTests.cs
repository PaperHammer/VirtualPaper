using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graphics.Canvas;
using System.Reflection;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Common.Utils.UndoRedo;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Shader;
using VirtualPaper.Shader.Models;
using Windows.UI;
using Workloads.Creation.StaticImg;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;
using Workloads.Creation.StaticImg.Extensions;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Creation.StaticImg.Models.ToolItems;
using Workloads.Creation.StaticImg.ViewModels;

namespace StaticImg.Test.T_StaticImg;

[TestClass]
[DoNotParallelize]
public partial class DrawingEditingRegressionTests {
    [TestInitialize]
    public void Initialize() {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.DrawingEditing-" + Guid.NewGuid().ToString("N"))).FullName;
        _oldRoot = Constants.CommonPaths.TestRootDir;
        Constants.CommonPaths.TestRootDir = Path.Combine(_root, "profile");
        try { _previous = AppServiceLocator.Services; }
        catch (InvalidOperationException) { _previous = null; }
        _services = new ServiceCollection().AddSingleton<IUserSettingsClient>(new SettingsClient()).BuildServiceProvider();
        AppServiceLocator.Services = _services;
        _previousUi = (IUiSynchronizationContext?)typeof(CrossThreadInvoker)
            .GetField("_uiSyncContext", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
        CrossThreadInvoker.Initialize(new InlineUiContext());
        _device = new CanvasDevice(true);
    }

    [TestCleanup]
    public void Cleanup() {
        foreach (var vm in _viewModels) {
            foreach (var layer in vm.Data.AllLayers) {
                layer.IsDeleted = true;
                layer.RenderData?.Dispose();
                layer.RenderData = null!;
            }
        }
        foreach (var session in _sessions) session.Dispose();
        _device.Dispose();
        _services.Dispose();
        AppServiceLocator.Services = _previous!;
        CrossThreadInvoker.Initialize(_previousUi!);
        Constants.CommonPaths.TestRootDir = _oldRoot;
        Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    [DataRow(72)]
    [DataRow(96)]
    [DataRow(120)]
    [DataRow(144)]
    [DataRow(192)]
    [DataRow(300)]
    public void BitmapClone_PreservesDpiDimensionsFormatAndIndependentPixels(int dpi) {
        using var source = new CanvasRenderTarget(_device, 13, 7, dpi);
        using (var ds = source.CreateDrawingSession()) { ds.Clear(Color.FromArgb(128, 40, 80, 120)); }
        var original = source.GetPixelBytes();
        using var clone = source.Clone();
        Assert.AreEqual(source.SizeInPixels, clone.SizeInPixels);
        Assert.AreEqual(source.Size, clone.Size);
        Assert.AreEqual(source.Dpi, clone.Dpi);
        Assert.AreEqual(source.Format, clone.Format);
        Assert.AreEqual(source.AlphaMode, clone.AlphaMode);
        Assert.AreSame(source.Device, clone.Device);
        CollectionAssert.AreEqual(original, clone.GetPixelBytes());
        using (var ds = clone.CreateDrawingSession()) { ds.Clear(Color.FromArgb(255, 200, 100, 50)); }
        CollectionAssert.AreEqual(original, source.GetPixelBytes());
        CollectionAssert.AreNotEqual(original, clone.GetPixelBytes());
        clone.Dispose();
        CollectionAssert.AreEqual(original, source.GetPixelBytes());
    }

    [TestMethod]
    [DataRow(96)]
    [DataRow(144)]
    [DataRow(192)]
    [DataRow(300)]
    public void LayerClone_PreservesBackgroundAndInitialState_WithoutSharingTarget(int dpi) {
        using var source = new InkRenderData(Session(), new ArcSize(13, 7, (uint)dpi, RebuildMode.None), true);
        using (var ds = source.RenderTarget.CreateDrawingSession()) { ds.Clear(Color.FromArgb(255, 11, 22, 33)); }
        var original = source.RenderTarget.GetPixelBytes();
        using var clone = source.Clone();
        Assert.IsTrue(clone.IsNeedBackground);
        Assert.IsTrue(clone.IsInited.Task.IsCompletedSuccessfully);
        Assert.IsTrue(clone.IsInited.Task.Result);
        Assert.AreNotSame(source.RenderTarget, clone.RenderTarget);
        Assert.AreEqual(source.RenderTarget.SizeInPixels, clone.RenderTarget.SizeInPixels);
        Assert.AreEqual(source.RenderTarget.Dpi, clone.RenderTarget.Dpi);
        CollectionAssert.AreEqual(original, clone.RenderTarget.GetPixelBytes());
        source.Dispose();
        CollectionAssert.AreEqual(original, clone.RenderTarget.GetPixelBytes());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MissingOrCorruptVpd_ReturnsFailureWithoutCreatingEditableBlankLayers(bool createCorruptFile) {
        string path = Path.Combine(_root, "invalid.vpd");
        byte[] invalid = [1, 2, 3, 4];
        if (createCorruptFile) await File.WriteAllBytesAsync(path, invalid);
        var vm = ViewModel(path);
        Assert.IsFalse(await vm.LoadAsync());
        Assert.HasCount(0, vm.Data.Layers);
        Assert.IsFalse(vm.Session.UnReUtil.CanUndo);
        if (createCorruptFile) CollectionAssert.AreEqual(invalid, await File.ReadAllBytesAsync(path));
        else Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public async Task ValidVpd_PropagatesSuccessAndRestoresExactPixels() {
        string path = Path.Combine(_root, "valid.vpd");
        var size = new ArcSize(3, 2, 144, RebuildMode.None);
        var session = Session(path);
        using var render = new InkRenderData(session, size);
        byte[] expected = Pattern(render.RenderTarget);
        render.RenderTarget.SetPixelBytes(expected);
        Assert.IsTrue((await session.DesignFileUtil.SaveAsync(size, new BusinessData(),
            [new Layer("Restored", new LayerState { IsVisible = true }, render)])).Success);
        var vm = ViewModel(path);
        Assert.IsTrue(await vm.LoadAsync());
        Assert.HasCount(1, vm.Data.Layers);
        Assert.AreEqual("Restored", vm.Data.SelectedLayer.Name);
        Assert.AreEqual(render.RenderTarget.SizeInPixels, vm.Data.SelectedLayer.RenderData.RenderTarget.SizeInPixels);
        CollectionAssert.AreEqual(expected, vm.Data.SelectedLayer.RenderData.RenderTarget.GetPixelBytes());
    }

    [TestMethod]
    [DataRow(96)]
    [DataRow(144)]
    [DataRow(192)]
    [DataRow(300)]
    public async Task PngLayerRoundTrip_PreservesNonUniformPixelsAtOriginalDpi(int dpi) {
        var size = new ArcSize(7, 5, (uint)dpi, RebuildMode.None);
        using var original = new InkRenderData(Session(), size);
        byte[] expected = Pattern(original.RenderTarget);
        original.RenderTarget.SetPixelBytes(expected);
        using var stream = new MemoryStream();
        await original.SavePngAsync(stream);
        stream.Position = 0;
        using var loaded = new InkRenderData(Session(), size);
        await loaded.LoadPngAsync(stream);
        Assert.AreEqual(original.RenderTarget.SizeInPixels, loaded.RenderTarget.SizeInPixels);
        CollectionAssert.AreEqual(expected, loaded.RenderTarget.GetPixelBytes());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LegacyPngDimensions_CopyAvailablePixelRegionWithoutScaling(bool oversized) {
        using var loaded = new InkRenderData(Session(), new ArcSize(3, 2, 192, RebuildMode.None));
        int width = oversized ? 12 : 2, height = oversized ? 8 : 1;
        using var bitmap = new CanvasRenderTarget(_device, width, height, 96);
        byte[] pixels = Pattern(bitmap);
        bitmap.SetPixelBytes(pixels);
        using var stream = new MemoryStream();
        using (var png = stream.AsRandomAccessStream()) { await bitmap.SaveAsync(png, CanvasBitmapFileFormat.Png); }
        // AsRandomAccessStream owns its stream, so load from the encoded bytes.
        using var input = new MemoryStream(stream.ToArray());
        await loaded.LoadPngAsync(input);
        var targetSize = loaded.RenderTarget.SizeInPixels;
        byte[] actual = loaded.RenderTarget.GetPixelBytes();
        for (int y = 0; y < targetSize.Height; y++) {
            for (int x = 0; x < targetSize.Width; x++) {
                int destination = (y * (int)targetSize.Width + x) * 4;
                byte[] expected = x < width && y < height ? pixels.AsSpan((y * width + x) * 4, 4).ToArray() : new byte[4];
                CollectionAssert.AreEqual(expected, actual.AsSpan(destination, 4).ToArray());
            }
        }
    }

    [TestMethod]
    [DataRow(96)]
    [DataRow(144)]
    [DataRow(192)]
    [DataRow(300)]
    public async Task EffectCommitUndoRedo_UsesExactSnapshotAndPreservesDimensions(int dpi) {
        var vm = Editable((uint)dpi);
        var target = vm.Data.SelectedLayer.RenderData.RenderTarget;
        byte[] original = target.GetPixelBytes();
        var dimensions = target.SizeInPixels;
        using var effect = new EffectTool { ViewModel = vm };
        Assert.IsTrue(effect.StartPreview(ShaderType.Invert));
        effect.UpdateParams(EffectParams.Default);
        byte[] changed = target.GetPixelBytes();
        Assert.AreEqual((byte)(255 - original[0]), changed[0]);
        Assert.AreEqual((byte)(255 - original[1]), changed[1]);
        Assert.AreEqual((byte)(255 - original[2]), changed[2]);
        Assert.IsTrue(effect.Commit());
        Assert.IsFalse(effect.IsPreviewing);
        Assert.IsTrue(vm.Session.UnReUtil.CanUndo);
        await vm.Session.UnReUtil.UndoAsync();
        CollectionAssert.AreEqual(original, target.GetPixelBytes());
        await vm.Session.UnReUtil.RedoAsync();
        Assert.AreEqual(dimensions, target.SizeInPixels);
        CollectionAssert.AreEqual(changed, target.GetPixelBytes());
    }

    [TestMethod]
    public void EffectCancel_RestoresOriginalTargetAfterSelectionChanges() {
        var vm = Editable(192);
        var originalLayer = vm.Data.SelectedLayer;
        byte[] original = originalLayer.RenderData.RenderTarget.GetPixelBytes();
        using var effect = new EffectTool { ViewModel = vm };
        Assert.IsTrue(effect.StartPreview(ShaderType.Invert));
        effect.UpdateParams(EffectParams.Default);
        var other = vm.Data.AddLayer("Other", needRecord: false);
        byte[] otherPixels = other.RenderData.RenderTarget.GetPixelBytes();
        vm.Data.SelectedLayer = other;
        effect.Cancel();
        Assert.IsFalse(effect.IsPreviewing);
        CollectionAssert.AreEqual(original, originalLayer.RenderData.RenderTarget.GetPixelBytes());
        CollectionAssert.AreEqual(otherPixels, other.RenderData.RenderTarget.GetPixelBytes());
        Assert.IsFalse(vm.Session.UnReUtil.CanUndo);
    }

    [TestMethod]
    public void FailedEffectCommit_DoesNotRecordUndo_AndRemainsCancellable() {
        var vm = Editable(144);
        byte[] original = vm.Data.SelectedLayer.RenderData.RenderTarget.GetPixelBytes();
        using var effect = new EffectTool { ViewModel = vm };
        Assert.IsTrue(effect.StartPreview(ShaderType.Invert));
        // Simulate a transient application failure while processing the render event.
        effect.RenderRequest += (_, _) => throw new IOException("Injected rendering callback failure");
        Assert.IsFalse(effect.Commit());
        Assert.IsTrue(effect.IsPreviewing);
        Assert.IsFalse(vm.Session.UnReUtil.CanUndo);
        effect.Cancel();
        Assert.IsFalse(effect.IsPreviewing);
        CollectionAssert.AreEqual(original, vm.Data.SelectedLayer.RenderData.RenderTarget.GetPixelBytes());
    }

    [TestMethod]
    [DataRow("update")]
    [DataRow("commit")]
    [DataRow("cancel")]
    public void DeviceLossNotification_StopsPreviewWithoutWritingOrRecordingUndo(string operation) {
        var vm = Editable(144);
        var target = vm.Data.SelectedLayer.RenderData.RenderTarget;
        using var effect = new EffectTool { ViewModel = vm };
        Assert.IsTrue(effect.StartPreview(ShaderType.Invert));
        effect.UpdateParams(EffectParams.Default);
        byte[] lastPixels = target.GetPixelBytes();
        vm.Session.OnDeviceLost(_device, EventArgs.Empty);
        if (operation == "update") effect.UpdateParams(EffectParams.Default);
        else if (operation == "commit") Assert.IsFalse(effect.Commit());
        else effect.Cancel();
        Assert.IsFalse(effect.IsPreviewing);
        Assert.IsFalse(effect.StartPreview(ShaderType.Invert));
        Assert.IsFalse(vm.Session.UnReUtil.CanUndo);
        CollectionAssert.AreEqual(lastPixels, target.GetPixelBytes());
    }

    [TestMethod]
    public void DeviceLossDuringRenderNotification_AbortsCommitAndDoesNotRecordUndo() {
        var vm = Editable(192);
        using var effect = new EffectTool { ViewModel = vm };
        Assert.IsTrue(effect.StartPreview(ShaderType.Invert));
        effect.RenderRequest += (_, _) => vm.Session.OnDeviceLost(_device, EventArgs.Empty);
        Assert.IsFalse(effect.Commit());
        Assert.IsTrue(vm.Session.IsDrawingUnavailable);
        Assert.IsFalse(effect.IsPreviewing);
        Assert.IsFalse(vm.Session.UnReUtil.CanUndo);
    }

    [TestMethod]
    public void PreviewWithDisposedTarget_FailsWithoutRecordingUndo() {
        var vm = Editable(144);
        using var effect = new EffectTool { ViewModel = vm };
        vm.Data.SelectedLayer.RenderData.RenderTarget.Dispose();
        Assert.IsFalse(effect.StartPreview(ShaderType.Invert));
        Assert.IsFalse(effect.IsPreviewing);
        Assert.IsFalse(effect.Commit());
        Assert.IsFalse(vm.Session.UnReUtil.CanUndo);
    }

    private InkProjectSession Session(string? path = null) {
        var session = new InkProjectSession(path ?? Path.Combine(_root, Guid.NewGuid().ToString("N") + ".vpd"), FileType.FImage, _device);
        _sessions.Add(session);
        return session;
    }

    private InkCanvasViewModel ViewModel(string path) {
        var vm = new InkCanvasViewModel(Session(path), null!);
        _viewModels.Add(vm);
        return vm;
    }

    [TestMethod]
    [DataRow(96)]
    [DataRow(144)]
    [DataRow(192)]
    public async Task ResizeAllLayers_UndoAndRedo_RestoreExactPixelsAfterSpillingToDisk(int dpi) {
        var vm = Editable((uint)dpi);
        var second = vm.Data.AddLayer("Second", needRecord: false);
        using (var drawing = second.RenderData.RenderTarget.CreateDrawingSession()) {
            drawing.Clear(Color.FromArgb(255, 60, 90, 120));
        }
        var originals = vm.Data.Layers.ToDictionary(layer => layer.Tag, layer => layer.RenderData.RenderTarget.GetPixelBytes());
        var originalSize = vm.Data.CanvasSize;
        var resized = new ArcSize(6, 4, (uint)dpi, RebuildMode.ResizeScale);
        var notifications = new List<ArcSize>();
        using var command = new LayerResizeScaleCommand(vm.Data, originalSize, resized, notifications.Add);
        await command.UndoAsync();
        Assert.IsEmpty(notifications, "Undo before execution must preserve the project.");
        await command.ExecuteAsync();
        Assert.AreEqual(resized, vm.Data.CanvasSize);
        var afterResize = vm.Data.Layers.ToDictionary(layer => layer.Tag, layer => layer.RenderData.RenderTarget.GetPixelBytes());
        foreach (var layer in vm.Data.Layers) {
            Assert.IsGreaterThan(originals[layer.Tag].Length, afterResize[layer.Tag].Length);
        }
        long memoryBeforeSpill = command.EstimatedMemoryBytes;
        using var store = new UndoDiskStore("resize-roundtrip", _root);
        var spillable = (IDiskSpillableUndoCommand)command;
        Assert.IsTrue(await spillable.TrySpillToDiskAsync(store, CancellationToken.None));
        Assert.IsGreaterThan(0L, spillable.DiskStorageBytes);
        Assert.IsLessThan(memoryBeforeSpill, command.EstimatedMemoryBytes);
        await command.UndoAsync();
        Assert.AreEqual(originalSize, vm.Data.CanvasSize);
        foreach (var layer in vm.Data.Layers) CollectionAssert.AreEqual(originals[layer.Tag], layer.RenderData.RenderTarget.GetPixelBytes());
        await command.ExecuteAsync();
        Assert.AreEqual(resized, vm.Data.CanvasSize);
        foreach (var layer in vm.Data.Layers) CollectionAssert.AreEqual(afterResize[layer.Tag], layer.RenderData.RenderTarget.GetPixelBytes());
        CollectionAssert.AreEqual(new[] { resized, originalSize, resized }, notifications);
        command.Dispose();
        Assert.AreEqual(0L, spillable.DiskStorageBytes);
        Assert.IsEmpty(Directory.GetFiles(store.SessionDirectory, "*.undo"));
    }

    private InkCanvasViewModel Editable(uint dpi) {
        var vm = ViewModel(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".vpd"));
        vm.Data.CanvasSize = new ArcSize(3, 2, dpi, RebuildMode.None);
        var layer = vm.Data.AddLayer("Original", needRecord: false);
        vm.Data.SelectedLayer = layer;
        using var ds = layer.RenderData.RenderTarget.CreateDrawingSession();
        ds.Clear(Color.FromArgb(255, 12, 34, 56));
        return vm;
    }

    private sealed class SettingsClient : IUserSettingsClient {
        public ISettings Settings => throw new NotSupportedException();
        public List<IApplicationRules> AppRules { get; } = [];
        public List<IWallpaperLayout> WallpaperLayouts { get; } = [];
        public List<IRecentUsed> RecentUseds { get; } = [];
        public Task SaveAsync<T>() => Task.CompletedTask;
        public void Save<T>() { }
        public Task LoadAsync<T>() => Task.CompletedTask;
        public void Load<T>() { }
        public Task UpdateRecentUsedAsync(string filePath) => Task.CompletedTask;
        public Task UpdateRecetUsedAsync(string[] filePath) => Task.CompletedTask;
        public Task DeleteRecetUsedAsync(IRecentUsed item) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class InlineUiContext : IUiSynchronizationContext {
        public override void Post(Action action) => action();
        public override void Send(Action action) => action();
        public override Task PostAsync(Func<Task> action) => action();
        public override Task<T> PostAsync<T>(Func<Task<T>> action) => action();
    }

    private static byte[] Pattern(CanvasRenderTarget target) {
        int count = checked((int)(target.SizeInPixels.Width * target.SizeInPixels.Height));
        var bytes = new byte[count * 4];
        for (int i = 0; i < count; i++) {
            bytes[i * 4] = (byte)(i * 7 % 256);
            bytes[i * 4 + 1] = (byte)(i * 11 % 256);
            bytes[i * 4 + 2] = (byte)(i * 17 % 256);
            bytes[i * 4 + 3] = 255;
        }
        return bytes;
    }

    private string _root = null!, _oldRoot = null!;
    private IServiceProvider? _previous;
    private IUiSynchronizationContext? _previousUi;
    private ServiceProvider _services = null!;
    private CanvasDevice _device = null!;
    private readonly List<InkProjectSession> _sessions = [];
    private readonly List<InkCanvasViewModel> _viewModels = [];
}
