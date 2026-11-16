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
    [DataRow("super-resolution", false, false)]
    [DataRow("super-resolution", true, false)]
    [DataRow("style-transfer", false, false)]
    [DataRow("style-transfer", true, false)]
    [DataRow("depth", false, false)]
    [DataRow("depth", true, false)]
    [DataRow("super-resolution", true, true)]
    [DataRow("style-transfer", true, true)]
    [DataRow("depth", true, true)]
    public void SelectedPolicy_ProcessesRealModel_AndReportsActualBackend(string kind, bool enabled, bool automatic) {
        var profiles = new InferenceDevicePolicies(enabled);
        var policy = !automatic ? new InferenceDevicePolicy(enabled) : kind switch {
            "super-resolution" => profiles.SuperResolution,
            "style-transfer" => profiles.StyleTransfer,
            _ => profiles.DepthEstimate
        };
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
                    using (var model = new Realesrgan(policy)) {
                        model.LoadModel(modelPath);
                        model.RunAndSave(input, output, 128, 96);
                        state = model.InferenceState;
                    }
                    break;
                case "style-transfer":
                    using (var model = new AdaIn(policy)) {
                        string style = TestImageHelper.CreateSolidColorJpeg(64, 64, root);
                        model.LoadModel(modelPath);
                        using (var graph = File.OpenRead(modelPath)) {
                            Version.TryParse(Microsoft.ML.OnnxRuntime.OrtEnv.Instance().GetVersionString(), out var nativeVersion);
                            bool cpuOnly = AdaInModelCompatibility.RequiresCpu(Convert.ToHexString(SHA256.HashData(graph)),
                                nativeVersion);
                            if (enabled && policy.Preference == InferenceDevicePreference.GpuPreferred && cpuOnly) {
                                Assert.AreEqual(InferenceDevice.Cpu, model.InferenceState.Device,
                                    "Known incompatible graphs must select CPU before running, rather than waiting for DirectML to fail.");
                                Assert.AreEqual(InferenceSelectionReason.ModelUnsupported, model.InferenceState.Reason);
                                Assert.IsFalse(model.InferenceState.IsCpuFallback);
                            }
                        }
                        model.RunAndSave(input, style, output);
                        state = model.InferenceState;
                    }
                    break;
                default:
                    using (var model = new DepthAnythingV2(policy)) {
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
            Assert.AreEqual(enabled, state.HardwareAccelerationEnabled);
            Assert.AreNotEqual(InferenceDevice.Uninitialized, state.Device);
            if (!enabled || policy.Preference == InferenceDevicePreference.CpuPreferred) {
                Assert.AreEqual(InferenceDevice.Cpu, state.Device);
                Assert.AreEqual(enabled ? InferenceSelectionReason.CpuPreferred : InferenceSelectionReason.HardwareAccelerationDisabled, state.Reason);
                Assert.IsFalse(state.IsCpuFallback);
            }
            else if (state.Device == InferenceDevice.DirectML) Assert.AreEqual(InferenceSelectionReason.GpuPreferred, state.Reason);
            else Assert.IsTrue(state.Reason is InferenceSelectionReason.GpuFailed or InferenceSelectionReason.ModelUnsupported);
            TestContext.WriteLine($"{kind}: hardware acceleration: {enabled}; automatic profile: {automatic}; {state.Device}; reason: {state.Reason}; elapsed: {timer.ElapsedMilliseconds} ms");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
