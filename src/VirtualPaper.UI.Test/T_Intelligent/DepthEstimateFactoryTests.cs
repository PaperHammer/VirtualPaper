using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.ML.DepthEstimate;
using VirtualPaper.ML.DepthEstimate.Interfaces;
using VirtualPaper.ML.Inference;

namespace VirtualPaper.UI.Test.T_Intelligent;

[TestClass]
public class DepthEstimateFactoryTests {
    [TestMethod]
    public void FactoryModels_AreIndependent_AndDisposedModelsAreNotRetainedByRootContainer() {
        var policy = new InferenceDevicePolicy(true);
        using var services = new ServiceCollection()
            .AddSingleton<IDepthEstimateFactory>(new DepthEstimateFactory(policy)).BuildServiceProvider();
        var factory = services.GetRequiredService<IDepthEstimateFactory>();
        WeakReference[] references = Enumerable.Range(0, 5).Select(_ => CreateDisposedModel(factory)).ToArray();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.IsTrue(references.All(reference => !reference.IsAlive), "Short-lived models must not remain rooted in the DI disposal list.");
        using var next = factory.Create();
        Assert.IsTrue(((IInferenceStatusSource)next).InferenceState.AccelerationRequested);
        Assert.Throws<FileNotFoundException>(() => next.LoadModel(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".onnx")));
        GC.KeepAlive(services);
    }

    [TestMethod]
    public void FactoryModels_AreUsableAfterAnotherInstanceIsDisposed() {
        var policy = new InferenceDevicePolicy(true);
        var factory = new DepthEstimateFactory(policy);
        using var first = factory.Create();
        using var second = factory.Create();
        Assert.AreNotSame(first, second);
        first.Dispose();
        Assert.Throws<FileNotFoundException>(() => second.LoadModel(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".onnx")));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedModel(IDepthEstimateFactory factory) {
        using var model = factory.Create();
        return new WeakReference(model);
    }
}
