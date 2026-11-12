using VirtualPaper.Common;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.Services;
using VirtualPaper.ML.Inference;
using VirtualPaper.UI.Test.Utils;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UI.Test.T_Intelligent;

[TestClass]
[DoNotParallelize]
public class InferenceStatusTests {
    [TestInitialize]
    public void Setup() => CrossThreadInvoker.Initialize(new TUiSynchronizationContext());

    [TestMethod]
    public void Disabled_HidesBackend_AndUninitializedGpuDoesNotClaimAcceleration() {
        var task = new TaskItem();
        var source = new Source(new(false, InferenceDevice.Cpu, false));
        using (task.ObserveInference(source)) {
            Assert.IsFalse(task.HasBackendInfo);
            Assert.AreEqual(string.Empty, task.BackendText);
        }
        source.Set(new(true, InferenceDevice.Uninitialized, false));
        using (task.ObserveInference(source)) Assert.IsFalse(task.HasBackendInfo);
        Assert.AreEqual(0, source.SubscriberCount);
    }

    [TestMethod]
    public void CompletedStage_RetainsSnapshot_AndLaterModelFailureDoesNotRelabelIt() {
        var task = new TaskItem();
        var source = new Source(new(true, InferenceDevice.DirectML, false));
        using (task.ObserveInference(source)) {
            Assert.IsTrue(task.HasBackendInfo);
            Assert.AreEqual(LanguageUtil.GetI18n(Constants.I18n.Intelligent_Backend_Gpu), task.BackendText);
        }
        Assert.AreEqual(0, source.SubscriberCount);
        source.Set(new(true, InferenceDevice.Cpu, true));
        Assert.AreEqual(LanguageUtil.GetI18n(Constants.I18n.Intelligent_Backend_Gpu), task.BackendText);
    }

    [TestMethod]
    public void MultiStageTask_RetainsEarlierFallback_WhenLaterStageUsesGpu() {
        var task = new TaskItem();
        var style = new Source(new(true, InferenceDevice.DirectML, false));
        using (task.ObserveInference(style)) style.Set(new(true, InferenceDevice.Cpu, true));
        var upscale = new Source(new(true, InferenceDevice.DirectML, false));
        using (task.ObserveInference(upscale)) {
            Assert.AreEqual(LanguageUtil.GetI18n(Constants.I18n.Intelligent_Backend_CpuFallback), task.BackendText);
        }
        Assert.AreEqual(0, style.SubscriberCount);
        Assert.AreEqual(0, upscale.SubscriberCount);
    }

    [TestMethod]
    public void DelayedNotifications_ApplyFinalSnapshotOnDispose_AndIgnoreOlderQueuedActions() {
        var source = new Source(new(true, InferenceDevice.DirectML, false));
        var dispatch = new Queue<Action>();
        var applied = new List<InferenceState>();
        var subscription = new InferenceStatusSubscription(source, applied.Add, dispatch.Enqueue);
        source.Set(new(true, InferenceDevice.Cpu, true));
        Assert.HasCount(0, applied);
        subscription.Dispose();
        subscription.Dispose();
        source.Set(new(true, InferenceDevice.DirectML, false));
        while (dispatch.TryDequeue(out var action)) action();
        CollectionAssert.AreEqual(new[] { new InferenceState(true, InferenceDevice.Cpu, true) }, applied);
        Assert.AreEqual(0, source.SubscriberCount);
    }

    [TestMethod]
    public void InitialSnapshot_DoesNotOverwriteAFallbackPublishedDuringTheRead() {
        var source = new Source(new(true, InferenceDevice.DirectML, false));
        var fallback = new InferenceState(true, InferenceDevice.Cpu, true);
        source.OnSubscribedRead = () => source.Set(fallback);
        var applied = new List<InferenceState>();
        using (var subscription = new InferenceStatusSubscription(source, applied.Add, action => action()))
            Assert.AreEqual(fallback, applied[^1]);
        Assert.AreEqual(fallback, applied[^1]);
        Assert.AreEqual(0, source.SubscriberCount);
    }

    private sealed class Source(InferenceState state) : IInferenceStatusSource {
        public InferenceState InferenceState {
            get {
                var snapshot = _state;
                if (_changed != null && OnSubscribedRead is { } duringRead) {
                    OnSubscribedRead = null;
                    duringRead();
                }
                return snapshot;
            }
        }
        public Action? OnSubscribedRead { get; set; }
        private InferenceState _state = state;
        private EventHandler<InferenceState>? _changed;
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event EventHandler<InferenceState>? InferenceStateChanged { add => _changed += value; remove => _changed -= value; }
        public void Set(InferenceState value) { _state = value; _changed?.Invoke(this, value); }
    }

    private sealed class TaskItem : IntelligentTaskItem {
        public override bool IsPending => false;
        public override bool IsProcessing => false;
        public override bool IsCompleted => true;
        protected override void NotifyCommandsCanExecuteChanged() { }
    }
}
