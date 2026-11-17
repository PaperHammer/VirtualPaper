using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using VirtualPaper.ML.Inference;

namespace VirtualPaper.ML.Test.T_Inference;

[TestClass]
[DoNotParallelize] // 契约测试自行创建并发操作，避免与真实模型测试争用线程池。
public class OnnxInferenceSessionTests {
    // Self-authored Identity graph: float[2] -> float[2], IR 8 / opset 13. No downloaded weights.
    internal static readonly byte[] IdentityModel = Convert.FromBase64String(
        "CAgSElZpcnR1YWxQYXBlciB0ZXN0czpdChkKBWlucHV0EgZvdXRwdXQiCElkZW50aXR5EhVWaXJ0dWFsUGFwZXIuSWRlbnRpdHlaEwoFaW5wdXQSCgoICAESBAoCCAJiFAoGb3V0cHV0EgoKCAgBEgQKAggCQgIQDQ==");

    private static InferenceSession CpuSession() {
        using var options = new SessionOptions { IntraOpNumThreads = 1, InterOpNumThreads = 1 };
        return new InferenceSession(IdentityModel, options);
    }

    // ORT 1.20 only exposes this constructor internally. Keep controlled native failures in the fixture.
    private static OnnxRuntimeException NativeFailure(int code, string message) {
        var constructor = typeof(OnnxRuntimeException).GetConstructors(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Single();
        return (OnnxRuntimeException)constructor.Invoke([Enum.ToObject(constructor.GetParameters()[0].ParameterType, code), message]);
    }

    private static OnnxRuntimeException DeviceRemoved() => NativeFailure(1, "DmlExecutionProvider: DXGI_ERROR_DEVICE_REMOVED (0x887A0005)");

    [TestMethod]
    public void Disabled_UsesOnlyCpu_AndExecutesRealIdentityGraph() {
        var attempts = new List<bool>();
        InferenceState? state = null;
        using var session = new OnnxInferenceSession(new(), gpu => { attempts.Add(gpu); return CpuSession(); }, value => state = value);
        var actual = session.Execute(() => {
            using var options = new RunOptions();
            using var output = session.Run([NamedOnnxValue.CreateFromTensor("input", new DenseTensor<float>(new[] { 2f, -3f }, new[] { 2 }))], ["output"], options);
            return output.Single().AsTensor<float>().ToArray();
        }, default);
        CollectionAssert.AreEqual(new[] { 2f, -3f }, actual);
        CollectionAssert.AreEqual(new[] { false }, attempts);
        Assert.AreEqual(new(false, InferenceDevice.Cpu, InferenceSelectionReason.HardwareAccelerationDisabled), state);
    }

    [TestMethod]
    [DataRow(InferenceDevicePreference.CpuPreferred, false)]
    [DataRow(InferenceDevicePreference.GpuPreferred, true)]
    public void SelectedSession_IsReusedAcrossOperations_WithoutRecreatingTheBackend(InferenceDevicePreference preference, bool gpuExpected) {
        var creates = new List<bool>();
        var states = new List<InferenceState>();
        using var session = new OnnxInferenceSession(new(true, preference), gpu => { creates.Add(gpu); return CpuSession(); }, states.Add);
        for (int index = 0; index < 3; index++) Assert.AreEqual(index, session.Execute(() => index, default));
        CollectionAssert.AreEqual(new[] { gpuExpected }, creates);
        Assert.HasCount(1, states, "Reused sessions must not reselect or reinitialize a backend for every image.");
        Assert.AreEqual(gpuExpected ? InferenceSelectionReason.GpuPreferred : InferenceSelectionReason.CpuPreferred, states[0].Reason);
    }

    [TestMethod]
    public void InitializationFailure_IsCachedPerModelAcrossInstances() {
        var policies = new InferenceDevicePolicies(true);
        var attempts = new List<bool>();
        InferenceSession Create(bool gpu) { attempts.Add(gpu); if (gpu) throw DeviceRemoved(); return CpuSession(); }
        using (var first = new OnnxInferenceSession(policies.SuperResolution, Create, _ => { })) Assert.IsFalse(first.IsGpu);
        using (var next = new OnnxInferenceSession(policies.SuperResolution, Create, _ => { })) Assert.IsFalse(next.IsGpu);
        CollectionAssert.AreEqual(new[] { true, false, false }, attempts);
        Assert.IsFalse(policies.SuperResolution.CanUseGpu);
        Assert.AreEqual(InferenceSelectionReason.CpuPreferred, policies.StyleTransfer.SelectDevice().Reason);
        Assert.AreEqual(InferenceSelectionReason.CpuPreferred, policies.DepthEstimate.SelectDevice().Reason);
    }

    [TestMethod]
    public void ExecutionDeviceFailure_RetriesWholeOperationOnce_AndKeepsCpuForNextCall() {
        var states = new List<InferenceState>();
        var attempts = new List<bool>();
        // CPU native sessions stand in for both backends; device loss is injected deterministically.
        using var session = new OnnxInferenceSession(new(true), gpu => { attempts.Add(gpu); return CpuSession(); }, states.Add);
        int runs = 0;
        Assert.AreEqual(42, session.Execute(() => { if (++runs == 1) throw new AggregateException(DeviceRemoved()); return 42; }, default));
        Assert.AreEqual(2, runs);
        Assert.AreEqual(7, session.Execute(() => 7, default));
        CollectionAssert.AreEqual(new[] { true, false }, attempts);
        CollectionAssert.AreEqual(new[] { new InferenceState(true, InferenceDevice.DirectML, InferenceSelectionReason.GpuPreferred), new(true, InferenceDevice.Cpu, InferenceSelectionReason.GpuFailed) }, states);
    }

    [TestMethod]
    public void CpuRetryFailure_PropagatesWithoutAnotherRetry() {
        var attempts = new List<bool>();
        using var session = new OnnxInferenceSession(new(true), gpu => { attempts.Add(gpu); return CpuSession(); }, _ => { });
        int runs = 0;
        Assert.Throws<OnnxRuntimeException>(() => session.Execute<int>(() => { runs++; throw DeviceRemoved(); }, default));
        Assert.AreEqual(2, runs);
        CollectionAssert.AreEqual(new[] { true, false }, attempts);
    }

    [TestMethod]
    public void NativeOperatorRuntimeFailure_UsesCpuRetry_WithoutDeviceNameInMessage() {
        var failure = NativeFailure(6, "Non-zero status code returned while running Reshape node. Name:'node_view'");
        Assert.IsFalse(GpuInferenceFailure.IsDeviceFailure(failure), "Model initialization must retain stricter classification.");
        int runs = 0;
        InferenceState? state = null;
        using var session = new OnnxInferenceSession(new(true), _ => CpuSession(), value => state = value);
        Assert.AreEqual(42, session.Execute(() => { if (++runs == 1) throw failure; return 42; }, default));
        Assert.AreEqual(2, runs);
        Assert.AreEqual(new(true, InferenceDevice.Cpu, InferenceSelectionReason.GpuFailed), state);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NonDeviceFailure_NeverRetriesOrDisablesGpu(bool duringInitialization) {
        var policy = new InferenceDevicePolicy(true);
        var failure = NativeFailure(2, "DmlExecutionProvider input dimensions are invalid");
        int creates = 0, runs = 0;
        InferenceSession Create(bool gpu) { creates++; if (duringInitialization) throw failure; return CpuSession(); }
        if (duringInitialization) {
            Assert.AreSame(failure, Assert.Throws<OnnxRuntimeException>(() => new OnnxInferenceSession(policy, Create, _ => { })));
        }
        else {
            using var session = new OnnxInferenceSession(policy, Create, _ => { });
            Assert.AreSame(failure, Assert.Throws<OnnxRuntimeException>(() => session.Execute<int>(() => { runs++; throw failure; }, default)));
            Assert.AreEqual(1, runs);
        }
        Assert.AreEqual(1, creates);
        Assert.IsTrue(policy.CanUseGpu);
    }

    [TestMethod]
    public void Cancellation_DoesNotRetryOrPoisonTheNextTask() {
        var policy = new InferenceDevicePolicy(true);
        using var cancellation = new CancellationTokenSource();
        using var session = new OnnxInferenceSession(policy, _ => CpuSession(), _ => { });
        int runs = 0;
        Assert.Throws<OnnxRuntimeException>(() => session.Execute<int>(() => {
            runs++; cancellation.Cancel(); throw DeviceRemoved();
        }, cancellation.Token));
        Assert.AreEqual(1, runs);
        Assert.IsTrue(policy.CanUseGpu);
        Assert.AreEqual(42, session.Execute(() => 42, default));
        Assert.IsTrue(session.IsGpu);
    }

    [TestMethod]
    public async Task RequestedAcceleration_SerializesOperations_AndCancelsWaitingWork() {
        using var session = new OnnxInferenceSession(new(true), _ => CpuSession(), _ => { });
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var first = Task.Run(() => session.Execute(() => { entered.Set(); Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(8))); return 1; }, default));
        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
        int waitingRuns = 0;
        var waiting = Task.Run(() => session.Execute(() => Interlocked.Increment(ref waitingRuns), cancellation.Token));
        try {
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(0, waitingRuns);
        }
        finally { release.Set(); }
        Assert.AreEqual(1, await first.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(2, session.Execute(() => 2, default));
    }

    [TestMethod]
    public async Task RequestedAcceleration_DoesNotEnterSecondOperationUntilFirstCompletes() {
        using var session = new OnnxInferenceSession(new(true), _ => CpuSession(), _ => { });
        using var firstEntered = new ManualResetEventSlim();
        using var secondAttempted = new ManualResetEventSlim();
        using var secondEntered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var first = Task.Run(() => session.Execute(() => {
            firstEntered.Set(); Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(8))); return 1;
        }, default));
        Assert.IsTrue(firstEntered.Wait(TimeSpan.FromSeconds(5)));
        var second = Task.Run(() => {
            secondAttempted.Set();
            return session.Execute(() => { secondEntered.Set(); return 2; }, default);
        });
        try {
            Assert.IsTrue(secondAttempted.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsFalse(secondEntered.Wait(TimeSpan.FromMilliseconds(200)), "DirectML operations must not overlap on one session.");
        }
        finally { release.Set(); }
        CollectionAssert.AreEqual(new[] { 1, 2 }, await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    [DataRow(false, true, InferenceDevicePreference.GpuPreferred, InferenceSelectionReason.HardwareAccelerationDisabled)]
    [DataRow(true, false, InferenceDevicePreference.GpuPreferred, InferenceSelectionReason.ModelUnsupported)]
    [DataRow(true, true, InferenceDevicePreference.CpuPreferred, InferenceSelectionReason.CpuPreferred)]
    public async Task CpuOnly_RetainsConcurrentOperations_WithoutAttemptingGpu(bool enabled, bool supported,
        InferenceDevicePreference preference, InferenceSelectionReason reason) {
        var attempts = new List<bool>();
        var policy = new InferenceDevicePolicy(enabled, preference);
        InferenceState? state = null;
        using var session = new OnnxInferenceSession(policy, gpu => { attempts.Add(gpu); return CpuSession(); }, value => state = value,
            supportsGpu: supported);
        using var entered = new CountdownEvent(2);
        using var release = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, 2).Select(_ => Task.Run(() => session.Execute(() => {
            entered.Signal(); Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(8))); return 1;
        }, default))).ToArray();
        try { Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)), "CPU operations must retain the existing parallel path."); }
        finally { release.Set(); }
        CollectionAssert.AreEqual(new[] { 1, 1 }, await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5)));
        CollectionAssert.AreEqual(new[] { false }, attempts);
        Assert.AreEqual(new(enabled, InferenceDevice.Cpu, reason), state);
        Assert.IsFalse(state!.IsCpuFallback);
        Assert.AreEqual(enabled && preference == InferenceDevicePreference.GpuPreferred, policy.CanUseGpu,
            "A model capability limit must not disable other model/runtime combinations.");
    }

    [TestMethod]
    public async Task Dispose_WaitsForAcceleratedOperation_ThenRejectsUse() {
        var session = new OnnxInferenceSession(new(true), _ => CpuSession(), _ => { });
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var run = Task.Run(() => session.Execute(() => { entered.Set(); Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(8))); return session.InputMetadata.Count; }, default));
        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
        var dispose = Task.Run(session.Dispose);
        try { Assert.IsFalse(dispose.Wait(TimeSpan.FromMilliseconds(100))); }
        finally { release.Set(); }
        Assert.AreEqual(1, await run.WaitAsync(TimeSpan.FromSeconds(5)));
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.Execute(() => 1, default));
    }

    [TestMethod]
    public void FailureClassification_RejectsCancellationIoAndMixedAggregates() {
        Assert.IsFalse(GpuInferenceFailure.IsDeviceFailure(new OperationCanceledException("DirectML")));
        Assert.IsFalse(GpuInferenceFailure.IsDeviceFailure(new IOException("DirectML source image")));
        Assert.IsFalse(GpuInferenceFailure.IsDeviceFailure(new AggregateException(DeviceRemoved(), new InvalidDataException("Bad model"))));
        Assert.IsTrue(GpuInferenceFailure.IsDeviceFailure(new DllNotFoundException("DirectML.dll")));
        Assert.IsTrue(GpuInferenceFailure.IsDeviceFailure(new EntryPointNotFoundException("AppendExecutionProvider_DML")));
    }
}
