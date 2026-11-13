using VirtualPaper.Common.Utils;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UI.Test.T_Intelligent;

[TestClass]
public class GpuTaskOptionsTests {
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task SuperResolution_CapturesChoice_AndHidesItWhenPermissionIsDisabled(bool enabled, bool requested) {
        var completion = new ResettableCompletionSource<IIntelliData?>();
        var vm = new SuperResolutionAddTaskViewModel(new(enabled)) { IntelligentCTS = completion };
        Assert.AreEqual(enabled, vm.ShowGpuOptions);
        Assert.AreEqual(enabled, vm.UseGpu, "GPU is recommended by default only when permission is enabled.");
        vm.UseGpu = requested;
        vm.SetSourceSize(20, 10);
        vm.SourceFilePath = "source.png";
        await vm.OnNextStepClickedAsync();
        var data = (SuperResolutionData)(await completion.Task)!;
        vm.UseGpu = !requested;
        vm.Clean();
        Assert.AreEqual(enabled && requested, data.UseGpu, "Later changes must not alter submitted tasks.");
        Assert.AreEqual(enabled, vm.UseGpu, "A new task restores the recommended default.");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task StyleTransfer_CapturesGpuChoiceOnlyForEnhancement(bool enabled, bool requested) {
        var completion = new ResettableCompletionSource<IIntelliData?>();
        var vm = new StyleTransferAddTaskViewModel(new(enabled)) { IntelligentCTS = completion };
        Assert.AreEqual(enabled, vm.ShowGpuOptions);
        Assert.AreEqual(enabled, vm.UseGpu);
        vm.SelectedStyle.ImagePath = "custom-style.png";
        vm.SourceFilePath = "source.png";
        vm.UseGpu = requested;
        await vm.OnNextStepClickedAsync();
        var data = (StyleTransferData)(await completion.Task)!;
        vm.UseGpu = !requested;
        vm.Clean();
        Assert.AreEqual(enabled && requested, data.UseGpu);
        Assert.AreEqual(enabled, vm.UseGpu);
        Assert.AreEqual("custom-style.png", data.StyleFilePath);
    }
}
