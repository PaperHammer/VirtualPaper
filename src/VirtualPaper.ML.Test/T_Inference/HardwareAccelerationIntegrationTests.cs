using System.Diagnostics;
using System.Security.Cryptography;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.ML.DepthEstimate;
using VirtualPaper.ML.Inference;
using VirtualPaper.ML.StyleTransfer;
using VirtualPaper.ML.SuperResolution;
using VirtualPaper.ML.Test.Infrastructure;

namespace VirtualPaper.ML.Test.T_Inference;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public class HardwareAccelerationIntegrationTests {
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("super-resolution", false)]
    [DataRow("super-resolution", true)]
    [DataRow("style-transfer", false)]
    [DataRow("style-transfer", true)]
    [DataRow("depth", false)]
    [DataRow("depth", true)]
    public void SelectedPolicy_ProcessesRealModel_AndReportsActualBackend(string kind, bool allowGpu) {
        string modelPath = Path.Combine(AppContext.BaseDirectory, kind switch {
            "super-resolution" => Constants.WorkingDir.ML_SuperResolution_AI_Models,
            "style-transfer" => Constants.WorkingDir.ML_StyleTransfer_AI_Models,
            _ => Constants.WorkingDir.ML_DepthEstimate_AI_Models
        }, kind switch {
            "super-resolution" => SuperResolution.Utils.Fields.ModelName,
            "style-transfer" => StyleTransfer.Utils.Fields.ModelName,
            _ => DepthEstimate.Utils.Fields.DepthAnythingV2ModelName
        });
        if (!File.Exists(modelPath)) Assert.Inconclusive($"Model not found: {modelPath}");
        string root = Path.Combine(Path.GetTempPath(), "vp-accelerated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string input = TestImageHelper.CreateSolidColorJpeg(64, 64, root);
            byte[] original = File.ReadAllBytes(input);
            var timer = Stopwatch.StartNew();
            InferenceState state;
            string output = Path.Combine(root, "output.png");
            switch (kind) {
                case "super-resolution":
                    using (var model = new Realesrgan(new(allowGpu))) {
                        model.LoadModel(modelPath);
                        model.RunAndSave(input, output, 128, 96);
                        state = model.InferenceState;
                    }
                    break;
                case "style-transfer":
                    using (var model = new AdaIn(new(allowGpu))) {
                        string style = TestImageHelper.CreateSolidColorJpeg(64, 64, root);
                        model.LoadModel(modelPath);
                        using (var graph = File.OpenRead(modelPath)) {
                            Version.TryParse(Microsoft.ML.OnnxRuntime.OrtEnv.Instance().GetVersionString(), out var nativeVersion);
                            bool cpuOnly = AdaInModelCompatibility.RequiresCpu(Convert.ToHexString(SHA256.HashData(graph)),
                                nativeVersion);
                            if (allowGpu && cpuOnly) {
                                Assert.AreEqual(InferenceDevice.Cpu, model.InferenceState.Device,
                                    "Known incompatible graphs must select CPU before running, rather than waiting for DirectML to fail.");
                                Assert.IsTrue(model.InferenceState.IsCpuFallback);
                            }
                        }
                        model.RunAndSave(input, style, output);
                        state = model.InferenceState;
                    }
                    break;
                default:
                    using (var model = new DepthAnythingV2(new InferenceDevicePolicy(allowGpu))) {
                        model.LoadModel(modelPath);
                        var depth = model.Run(input);
                        Assert.IsTrue(depth.Depth.All(value => float.IsFinite(value) && value >= 0 && value <= 1));
                        output = model.SaveDepthMap(depth, root);
                        state = model.InferenceState;
                    }
                    break;
            }
            using var result = Cv2.ImRead(output);
            Assert.IsFalse(result.Empty());
            Assert.AreEqual(kind == "style-transfer" ? 512 : kind == "super-resolution" ? 128 : 64, result.Width);
            Assert.AreEqual(kind == "style-transfer" ? 512 : kind == "super-resolution" ? 96 : 64, result.Height);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
            Assert.AreEqual(allowGpu, state.AccelerationRequested);
            Assert.AreNotEqual(InferenceDevice.Uninitialized, state.Device);
            Assert.AreEqual(allowGpu && state.Device == InferenceDevice.Cpu, state.IsCpuFallback);
            if (!allowGpu) Assert.AreEqual(InferenceDevice.Cpu, state.Device);
            TestContext.WriteLine($"{kind}: acceleration requested: {allowGpu}; {state.Device}; CPU fallback: {state.IsCpuFallback}; elapsed: {timer.ElapsedMilliseconds} ms");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
