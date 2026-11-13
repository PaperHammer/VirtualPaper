using VirtualPaper.ML.Inference;

namespace VirtualPaper.ML.Test.T_Inference;

[TestClass]
public class InferenceModelPoolTests {
    [TestMethod]
    public async Task SameDevice_ReusesModel_AndSwitchWaitsForAllTasksBeforeDisposing() {
        var models = new List<Model>();
        using var pool = new InferenceModelPool<Model>(new(true), policy => {
            Assert.IsTrue(models.All(model => model.Disposed), "Only one backend may remain resident.");
            var model = new Model(policy); models.Add(model); return model;
        });
        var first = await pool.AcquireAsync(false);
        var second = await pool.AcquireAsync(false);
        Assert.AreSame(first.Model, second.Model);
        var gpu = pool.AcquireAsync(true);
        Assert.IsFalse(gpu.IsCompleted);
        first.Dispose(); first.Dispose();
        Assert.IsFalse(first.Model.Disposed);
        Assert.IsFalse(gpu.IsCompleted, "The second CPU task still owns the model.");
        second.Dispose();
        using var accelerated = await gpu.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(first.Model.Disposed);
        Assert.AreEqual(1, first.Model.DisposeCount);
        Assert.AreEqual(InferenceDevicePreference.GpuPreferred, accelerated.Model.Policy.Preference);
        Assert.HasCount(2, models);
    }

    [TestMethod]
    public async Task CanceledWaiter_DoesNotSwitchOrDisposeTheRunningModel() {
        int creates = 0;
        using var pool = new InferenceModelPool<Model>(new(true), policy => { creates++; return new(policy); });
        using var cpu = await pool.AcquireAsync(false);
        using var cancellation = new CancellationTokenSource();
        var gpu = pool.AcquireAsync(true, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await gpu);
        using var anotherCpu = await pool.AcquireAsync(false);
        Assert.AreSame(cpu.Model, anotherCpu.Model);
        Assert.IsFalse(cpu.Model.Disposed);
        Assert.AreEqual(1, creates);
    }

    [TestMethod]
    public async Task Dispose_WakesWaiters_AndReleasesModelOnlyAfterLastLease() {
        var pool = new InferenceModelPool<Model>(new(true), policy => new(policy));
        var active = await pool.AcquireAsync(false);
        var waiting = pool.AcquireAsync(true);
        pool.Dispose(); pool.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await waiting);
        Assert.IsFalse(active.Model.Disposed);
        active.Dispose(); active.Dispose();
        Assert.AreEqual(1, active.Model.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await pool.AcquireAsync(false));
    }

    [TestMethod]
    public async Task FailedSwitch_DoesNotReuseDisposedModel_AndNextAcquisitionRecovers() {
        bool fail = true;
        using var pool = new InferenceModelPool<Model>(new(true), policy => {
            if (policy.Preference == InferenceDevicePreference.GpuPreferred && fail) {
                fail = false; throw new IOException("Controlled factory failure.");
            }
            return new(policy);
        });
        var cpu = await pool.AcquireAsync(false);
        cpu.Dispose();
        await Assert.ThrowsAsync<IOException>(async () => await pool.AcquireAsync(true));
        Assert.IsTrue(cpu.Model.Disposed);
        using var recovered = await pool.AcquireAsync(false);
        Assert.AreNotSame(cpu.Model, recovered.Model);
        Assert.IsFalse(recovered.Model.Disposed);
    }

    [TestMethod]
    public async Task DisabledPermission_UsesOneCpuModelRegardlessOfRequestedDevice() {
        using var pool = new InferenceModelPool<Model>(new(false), policy => new(policy));
        using var requested = await pool.AcquireAsync(true);
        using var cpu = await pool.AcquireAsync(false);
        Assert.AreSame(requested.Model, cpu.Model);
        Assert.AreEqual(new(false, InferenceDevice.Cpu, InferenceSelectionReason.HardwareAccelerationDisabled),
            requested.Model.Policy.SelectDevice());
    }

    [TestMethod]
    public async Task GpuFailure_SurvivesDeviceSwitch_WithoutMislabelingExplicitCpuWork() {
        var policy = new InferenceDevicePolicy(true);
        using var pool = new InferenceModelPool<Model>(policy, selected => new(selected));
        using (var gpu = await pool.AcquireAsync(true)) gpu.Model.Policy.DisableGpu();
        using (var cpu = await pool.AcquireAsync(false)) {
            Assert.AreEqual(InferenceSelectionReason.CpuPreferred, cpu.Model.Policy.SelectDevice().Reason);
            Assert.IsFalse(cpu.Model.Policy.SelectDevice().IsCpuFallback);
        }
        using var retry = await pool.AcquireAsync(true);
        Assert.AreEqual(InferenceSelectionReason.GpuFailed, retry.Model.Policy.SelectDevice().Reason);
        Assert.IsTrue(retry.Model.Policy.SelectDevice().IsCpuFallback);
    }

    private sealed class Model(InferenceDevicePolicy policy) : IDisposable {
        public InferenceDevicePolicy Policy { get; } = policy;
        public bool Disposed => DisposeCount != 0;
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
}
