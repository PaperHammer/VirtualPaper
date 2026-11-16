namespace StaticImg.Test.T_StaticImg;

public partial class DrawingEditingRegressionTests {
    [TestMethod]
    [DataRow(96)]
    [DataRow(192)]
    public async Task CopyLayer_PreservesPixelsAndIdentityAcrossUndoRedo_AndEditsAreIndependent(int dpi) {
        var vm = Editable((uint)dpi); var data = vm.Data;
        var original = data.SelectedLayer;
        var top = data.AddLayer("Top", needRecord: false);
        byte[] pixels = Pattern(original.RenderData.RenderTarget);
        original.RenderData.RenderTarget.SetPixelBytes(pixels);
        var copy = data.CopyLayer(original.Tag);
        Assert.IsNotNull(copy);
        CollectionAssert.AreEqual(new[] { original, copy, top }, data.Layers.ToArray());
        CollectionAssert.AreEqual(data.Layers.ToArray(), data.ActiveLayers.ToArray());
        CollectionAssert.AreEqual(pixels, copy.RenderData.RenderTarget.GetPixelBytes());
        Assert.AreEqual(original.RenderData.RenderTarget.Dpi, copy.RenderData.RenderTarget.Dpi);
        byte[] changed = (byte[])pixels.Clone(); changed[0] ^= 31;
        copy.RenderData.RenderTarget.SetPixelBytes(changed);
        CollectionAssert.AreEqual(pixels, original.RenderData.RenderTarget.GetPixelBytes());
        for (int i = 0; i < 2; i++) {
            await vm.Session.UnReUtil.UndoAsync();
            Assert.IsTrue(copy.IsDeleted);
            CollectionAssert.AreEqual(new[] { original, top }, data.ActiveLayers.ToArray());
            await vm.Session.UnReUtil.RedoAsync();
            Assert.IsFalse(copy.IsDeleted);
            CollectionAssert.AreEqual(new[] { original, copy, top }, data.Layers.ToArray());
            CollectionAssert.AreEqual(data.Layers.ToArray(), data.ActiveLayers.ToArray());
            CollectionAssert.AreEqual(changed, copy.RenderData.RenderTarget.GetPixelBytes());
        }
        Assert.HasCount(3, data.AllLayers);
    }

    [TestMethod]
    public async Task DeleteMiddleLayer_UndoRestoresItsOriginalPositionAndPixels_WithoutPhantomCommands() {
        var vm = Editable(144); var data = vm.Data;
        var bottom = data.SelectedLayer;
        var middle = data.AddLayer("Middle", needRecord: false);
        var top = data.AddLayer("Top", needRecord: false);
        byte[] pixels = Pattern(middle.RenderData.RenderTarget);
        middle.RenderData.RenderTarget.SetPixelBytes(pixels);
        data.DeleteLayer(middle.Tag);
        CollectionAssert.AreEqual(new[] { bottom, top }, data.ActiveLayers.ToArray());
        Assert.IsTrue(middle.IsDeleted);
        await vm.Session.UnReUtil.UndoAsync();
        CollectionAssert.AreEqual(new[] { bottom, middle, top }, data.Layers.ToArray());
        CollectionAssert.AreEqual(data.Layers.ToArray(), data.ActiveLayers.ToArray());
        CollectionAssert.AreEqual(pixels, middle.RenderData.RenderTarget.GetPixelBytes());
        Assert.IsFalse(vm.Session.UnReUtil.CanUndo);
        await vm.Session.UnReUtil.RedoAsync();
        CollectionAssert.AreEqual(new[] { bottom, top }, data.ActiveLayers.ToArray());
        Assert.HasCount(3, data.AllLayers);
        Assert.IsFalse(vm.Session.UnReUtil.CanRedo);
    }

    [TestMethod]
    public async Task MoveLayer_UndoRedoRestoresRenderOrderAndZIndices() {
        var vm = Editable(96); var data = vm.Data;
        var bottom = data.SelectedLayer;
        var middle = data.AddLayer("Middle", needRecord: false);
        var top = data.AddLayer("Top", needRecord: false);
        // Drag/drop reorders the bound collection before recording the move.
        data.Layers.Move(0, 2); data.MoveLayer(bottom, 0, 2);
        CollectionAssert.AreEqual(new[] { middle, top, bottom }, data.ActiveLayers.ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, data.Layers.Select(layer => layer.ZIndex).ToArray());
        await vm.Session.UnReUtil.UndoAsync();
        CollectionAssert.AreEqual(new[] { bottom, middle, top }, data.ActiveLayers.ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, data.Layers.Select(layer => layer.ZIndex).ToArray());
        await vm.Session.UnReUtil.RedoAsync();
        CollectionAssert.AreEqual(new[] { middle, top, bottom }, data.ActiveLayers.ToArray());
        Assert.IsFalse(vm.Session.UnReUtil.CanRedo);
    }

    [TestMethod]
    public async Task Visibility_UndoRedoInvalidatesRenderSnapshot_WithoutRecordingExtraChanges() {
        var vm = Editable(96); var data = vm.Data;
        var original = data.SelectedLayer; var other = data.AddLayer("Other", needRecord: false);
        CollectionAssert.AreEqual(new[] { original, other }, data.ActiveLayers.ToArray());
        original.IsVisible = false;
        CollectionAssert.AreEqual(new[] { other }, data.ActiveLayers.ToArray());
        await vm.Session.UnReUtil.UndoAsync();
        Assert.IsTrue(original.IsVisible);
        CollectionAssert.AreEqual(new[] { original, other }, data.ActiveLayers.ToArray());
        Assert.IsFalse(vm.Session.UnReUtil.CanUndo);
        await vm.Session.UnReUtil.RedoAsync();
        Assert.IsFalse(original.IsVisible);
        CollectionAssert.AreEqual(new[] { other }, data.ActiveLayers.ToArray());
        Assert.IsFalse(vm.Session.UnReUtil.CanRedo);
    }

    [TestMethod]
    public async Task AddLayerAboveSelected_UndoRedoRestoresSameLayerAndPixels() {
        var vm = Editable(192); var data = vm.Data;
        var original = data.SelectedLayer; var top = data.AddLayer("Top", needRecord: false);
        var added = data.AddLayer("Inserted", layerId: original.Tag);
        byte[] pixels = Pattern(added.RenderData.RenderTarget); added.RenderData.RenderTarget.SetPixelBytes(pixels);
        CollectionAssert.AreEqual(new[] { original, added, top }, data.ActiveLayers.ToArray());
        await vm.Session.UnReUtil.UndoAsync();
        CollectionAssert.AreEqual(new[] { original, top }, data.ActiveLayers.ToArray());
        await vm.Session.UnReUtil.RedoAsync();
        CollectionAssert.AreEqual(new[] { original, added, top }, data.ActiveLayers.ToArray());
        CollectionAssert.AreEqual(pixels, added.RenderData.RenderTarget.GetPixelBytes());
        Assert.HasCount(3, data.AllLayers);
    }
}
