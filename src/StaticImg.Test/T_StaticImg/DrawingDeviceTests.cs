using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common;
using Windows.UI;
using Workloads.Creation.StaticImg;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Creation.StaticImg.Extensions;
using Workloads.Utils.DraftUtils.Models;

namespace StaticImg.Test.T_StaticImg;

[TestClass]
[DoNotParallelize]
public class DrawingDeviceTests {
    [TestMethod]
    public void Disabled_DefaultFactoryUsesSoftwareDevice_AndRendersReadablePixels() {
        using var manager = new DrawingDeviceManager();
        manager.Initialize(false);
        var device = manager.GetDevice();
        Assert.IsTrue(device.ForceSoftwareRenderer);
        using var target = new CanvasRenderTarget(device, 1, 1, 96);
        using (var drawing = target.CreateDrawingSession()) { drawing.Clear(Color.FromArgb(255, 10, 20, 30)); }
        CollectionAssert.AreEqual(new byte[] { 30, 20, 10, 255 }, target.GetPixelBytes());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void StartupPreference_IsAppliedOnce_AndDeviceIsReused(bool enabled) {
        using var device = new CanvasDevice(true);
        var requests = new List<bool>();
        using var manager = new DrawingDeviceManager(software => { requests.Add(software); return device; });
        manager.Initialize(enabled);
        Assert.AreSame(device, manager.GetDevice());
        Assert.AreSame(device, manager.GetDevice());
        CollectionAssert.AreEqual(new[] { !enabled }, requests);
        Assert.ThrowsExactly<InvalidOperationException>(() => manager.Initialize(!enabled));
    }

    [TestMethod]
    [DataRow(unchecked((int)0x80004005))]
    [DataRow(unchecked((int)0x887A0004))]
    [DataRow(unchecked((int)0x887A0005))]
    [DataRow(unchecked((int)0x887A0006))]
    [DataRow(unchecked((int)0x887A0007))]
    [DataRow(unchecked((int)0x887A0020))]
    public void HardwareCreationFailure_RetriesSoftwareOnce_AndKeepsIt(int errorCode) {
        using var device = new CanvasDevice(true);
        var requests = new List<bool>();
        using var manager = new DrawingDeviceManager(software => {
            requests.Add(software);
            if (!software) throw new COMException("Injected device creation failure", errorCode);
            return device;
        });
        manager.Initialize(true);
        Assert.AreSame(device, manager.GetDevice());
        Assert.AreSame(device, manager.GetDevice());
        CollectionAssert.AreEqual(new[] { false, true }, requests);
    }

    [TestMethod]
    public void UnrelatedFailures_AreNotRetriedAsGpuFailures() {
        Exception[] errors = [new OperationCanceledException(), new OutOfMemoryException(),
            new ArgumentException(), new IOException(), new COMException("Access denied", unchecked((int)0x80070005))];
        foreach (var error in errors) {
            int calls = 0;
            using var manager = new DrawingDeviceManager(_ => { calls++; throw error; });
            manager.Initialize(true);
            Exception? caught = null;
            try { manager.GetDevice(); }
            catch (Exception ex) { caught = ex; }
            Assert.AreSame(error, caught);
            Assert.AreEqual(1, calls, error.GetType().Name);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SoftwareFailure_PropagatesWithoutRepeatedGpuAttempts(bool enabled) {
        var requests = new List<bool>();
        var error = new COMException("Injected device creation failure", unchecked((int)0x80004005));
        using var manager = new DrawingDeviceManager(software => { requests.Add(software); throw error; });
        manager.Initialize(enabled);
        Assert.AreSame(error, Assert.ThrowsExactly<COMException>(() => manager.GetDevice()));
        Assert.AreSame(error, Assert.ThrowsExactly<COMException>(() => manager.GetDevice()));
        CollectionAssert.AreEqual(enabled ? new[] { false, true, true } : new[] { true, true }, requests);
    }

    [TestMethod]
    public void LossNotification_SelectsSoftwareForNewProjects_AndIgnoresStaleNotifications() {
        using var original = new CanvasDevice(true);
        using var replacement = new CanvasDevice(true);
        var requests = new List<bool>();
        using var manager = new DrawingDeviceManager(software => {
            requests.Add(software);
            return requests.Count == 1 ? original : replacement;
        });
        Assert.AreSame(original, manager.GetDevice());
        // Inject the notification, without disabling the user's GPU or claiming
        // to reproduce a physical driver failure.
        manager.OnDeviceLost(original, EventArgs.Empty);
        Assert.AreSame(replacement, manager.GetDevice());
        manager.OnDeviceLost(original, EventArgs.Empty);
        Assert.AreSame(replacement, manager.GetDevice());
        CollectionAssert.AreEqual(new[] { false, true }, requests);
        Assert.ThrowsExactly<InvalidOperationException>(() => manager.Initialize(true));
    }

    [TestMethod]
    public void ManagerDisposal_DetachesWithoutDisposingSharedDevice() {
        using var device = new CanvasDevice(true);
        var manager = new DrawingDeviceManager(_ => device);
        Assert.AreSame(device, manager.GetDevice());
        manager.Dispose();
        manager.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => manager.GetDevice());
        Assert.ThrowsExactly<ObjectDisposedException>(() => manager.Initialize(true));
        using var target = new CanvasRenderTarget(device, 2, 2, 96);
        using var drawing = target.CreateDrawingSession();
        drawing.Clear(Color.FromArgb(255, 10, 20, 30));
    }

    [TestMethod]
    [DataRow(BrushType.General)]
    [DataRow(BrushType.Calligraphy)]
    [DataRow(BrushType.Airbrush)]
    [DataRow(BrushType.Oil)]
    [DataRow(BrushType.Watercolor)]
    [DataRow(BrushType.Marker)]
    [DataRow(BrushType.Pencil)]
    [DataRow(BrushType.Eraser)]
    public void BrushCache_UsesSuppliedSoftwareDevice_AndHasIndependentLifetime(BrushType type) {
        using var device = new CanvasDevice(true);
        var args = new BrushGenerateArgs(Color.FromArgb(255, 20, 40, 60), type, 12);
        var first = new BrushManager(device);
        using var second = new BrushManager(device);
        var firstBrush = first.GetBrush(args);
        var secondBrush = second.GetBrush(args);
        Assert.AreSame(device, firstBrush.Device);
        Assert.AreSame(device, secondBrush.Device);
        Assert.AreSame(firstBrush, first.GetBrush(args));
        Assert.AreNotSame(firstBrush, secondBrush);
        first.Dispose();
        first.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => first.GetBrush(args));
        Assert.AreSame(device, second.GetBrush(args).Device);
    }

    [TestMethod]
    public async Task LostSession_RejectsLayerWrites_AndPreservesSavedProjectAndOtherSessions() {
        string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.Drawing-" + Guid.NewGuid().ToString("N"))).FullName;
        string oldRoot = Constants.CommonPaths.TestRootDir;
        Constants.CommonPaths.TestRootDir = Path.Combine(root, "profile");
        try {
            string path = Path.Combine(root, "saved.vpd");
            using var device = new CanvasDevice(true);
            using var session = new InkProjectSession(path, FileType.FImage, device);
            using var other = new InkProjectSession(Path.Combine(root, "other.vpd"), FileType.FImage, device);
            using var render = new InkRenderData(session, new ArcSize(3, 2, 96, RebuildMode.None));
            using (var ds = render.RenderTarget.CreateDrawingSession()) { ds.Clear(Color.FromArgb(255, 12, 34, 56)); }
            var layers = new List<Layer> { new("Layer", new LayerState(), render) };
            Assert.IsTrue((await session.DesignFileUtil.SaveAsync(new ArcSize(3, 2, 96, RebuildMode.None), new BusinessData(), layers)).Success);
            byte[] originalBytes = await File.ReadAllBytesAsync(path);
            using (var reopened = new InkProjectSession(path, FileType.FImage, device)) {
                var loaded = await reopened.DesignFileUtil.LoadAsync(reopened);
                Assert.IsNotNull(loaded.layers);
                Assert.HasCount(1, loaded.layers);
                try {
                    Assert.AreSame(device, loaded.layers[0].RenderData.RenderTarget.Device);
                    CollectionAssert.AreEqual(render.RenderTarget.GetPixelBytes(), loaded.layers[0].RenderData.RenderTarget.GetPixelBytes());
                }
                finally { foreach (var layer in loaded.layers) layer.Dispose(); }
            }
            int notifications = 0;
            session.DrawingDeviceLost += (_, _) => notifications++;
            session.OnDeviceLost(device, EventArgs.Empty);
            session.OnDeviceLost(device, EventArgs.Empty);
            Assert.AreEqual(1, notifications);
            Assert.IsTrue(session.IsDrawingUnavailable);
            Assert.AreSame(device, session.Device); // Never replace an active project's layer device.
            Assert.ThrowsExactly<InvalidOperationException>(session.EnsureDrawingAvailable);
            using var stream = new MemoryStream();
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => render.SavePngAsync(stream));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => render.SaveAsync(stream));
            Assert.AreEqual(0L, stream.Length);
            Assert.IsFalse((await session.DesignFileUtil.SaveAsync(new ArcSize(3, 2, 96, RebuildMode.None), new BusinessData(), layers)).Success);
            CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(path));
            other.EnsureDrawingAvailable(); // Per-project notification does not dispose the shared device.
            session.Dispose();
            session.OnDeviceLost(device, EventArgs.Empty);
            Assert.AreEqual(1, notifications);
            Assert.ThrowsExactly<ObjectDisposedException>(session.EnsureDrawingAvailable);
        }
        finally {
            Constants.CommonPaths.TestRootDir = oldRoot;
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExportFailureOrCancellation_PreservesExistingFile_AndCleansTemporaryOutput(bool cancel) {
        string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.Export-" + Guid.NewGuid().ToString("N"))).FullName;
        try {
            string path = Path.Combine(root, "export.png");
            byte[] originalBytes = [1, 2, 3, 4];
            await File.WriteAllBytesAsync(path, originalBytes);
            using var device = new CanvasDevice(true);
            using var target = new CanvasRenderTarget(device, 1, 1, 96);
            using (var ds = target.CreateDrawingSession()) { ds.Clear(Color.FromArgb(255, 12, 34, 56)); }
            using var cancellation = new CancellationTokenSource();
            if (cancel) cancellation.Cancel();
            // An exclusive destination lock makes the final commit fail after encoding.
            using (var destinationLock = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None)) {
                var result = await target.ExportAsync(target.Size,
                    new ExportDataStaticImg("export.png", path, ExportImageFormat.Png), cancellation.Token);
                Assert.IsNull(result);
            }
            CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(path));
            CollectionAssert.AreEquivalent(new[] { path }, Directory.GetFiles(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
