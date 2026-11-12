using VirtualPaper.ML.Inference;

namespace VirtualPaper.ML.Test.T_Inference;

[TestClass]
public class InferenceDevicePolicyTests {
    [TestMethod]
    [DataRow(false, InferenceDevicePreference.CpuPreferred, true, InferenceDevice.Cpu, InferenceSelectionReason.HardwareAccelerationDisabled)]
    [DataRow(false, InferenceDevicePreference.GpuPreferred, true, InferenceDevice.Cpu, InferenceSelectionReason.HardwareAccelerationDisabled)]
    [DataRow(false, InferenceDevicePreference.GpuPreferred, false, InferenceDevice.Cpu, InferenceSelectionReason.HardwareAccelerationDisabled)]
    [DataRow(true, InferenceDevicePreference.CpuPreferred, true, InferenceDevice.Cpu, InferenceSelectionReason.CpuPreferred)]
    [DataRow(true, InferenceDevicePreference.CpuPreferred, false, InferenceDevice.Cpu, InferenceSelectionReason.ModelUnsupported)]
    [DataRow(true, InferenceDevicePreference.GpuPreferred, false, InferenceDevice.Cpu, InferenceSelectionReason.ModelUnsupported)]
    [DataRow(true, InferenceDevicePreference.GpuPreferred, true, InferenceDevice.DirectML, InferenceSelectionReason.GpuPreferred)]
    public void Selection_RespectsPermissionPreferenceAndCompatibility(bool enabled, InferenceDevicePreference preference,
        bool supported, InferenceDevice device, InferenceSelectionReason reason) {
        var policy = new InferenceDevicePolicy(enabled, preference);
        var actual = policy.SelectDevice(supported);
        Assert.AreEqual(new(enabled, device, reason), actual);
        Assert.IsFalse(actual.IsCpuFallback, "An initial CPU choice must not be presented as a GPU failure.");
    }

    [TestMethod]
    public void FailureMemory_IsPerModel_AndTheDisabledSwitchStillTakesPriority() {
        var policies = new InferenceDevicePolicies(true);
        policies.SuperResolution.DisableGpu();
        Assert.AreEqual(new(true, InferenceDevice.Cpu, InferenceSelectionReason.GpuFailed), policies.SuperResolution.SelectDevice());
        Assert.IsTrue(policies.SuperResolution.SelectDevice().IsCpuFallback);
        Assert.AreEqual(InferenceSelectionReason.CpuPreferred, policies.DepthEstimate.SelectDevice().Reason);
        Assert.AreEqual(InferenceSelectionReason.CpuPreferred, policies.StyleTransfer.SelectDevice().Reason);

        var disabled = new InferenceDevicePolicy(false);
        disabled.DisableGpu();
        Assert.AreEqual(new(false, InferenceDevice.Cpu, InferenceSelectionReason.HardwareAccelerationDisabled), disabled.SelectDevice(false));
        Assert.IsFalse(disabled.SelectDevice().IsCpuFallback);
    }

    [TestMethod]
    public void AutomaticProfiles_MatchCurrentWorkloads_WithoutChangingTheGlobalPermission() {
        foreach (bool enabled in new[] { false, true }) {
            var policies = new InferenceDevicePolicies(enabled);
            Assert.AreEqual(InferenceDevicePreference.GpuPreferred, policies.SuperResolution.Preference);
            Assert.AreEqual(InferenceDevicePreference.CpuPreferred, policies.StyleTransfer.Preference);
            Assert.AreEqual(InferenceDevicePreference.CpuPreferred, policies.DepthEstimate.Preference);
            foreach (var policy in new[] { policies.SuperResolution, policies.StyleTransfer, policies.DepthEstimate })
                Assert.AreEqual(enabled, policy.HardwareAccelerationEnabled);
            Assert.AreEqual(enabled ? InferenceDevice.DirectML : InferenceDevice.Cpu, policies.SuperResolution.SelectDevice().Device);
            Assert.AreEqual(InferenceDevice.Cpu, policies.StyleTransfer.SelectDevice().Device);
            Assert.AreEqual(InferenceDevice.Cpu, policies.DepthEstimate.SelectDevice().Device);
        }
    }
}
