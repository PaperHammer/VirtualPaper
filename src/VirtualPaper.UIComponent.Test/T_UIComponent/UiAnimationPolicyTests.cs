using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Test.T_UIComponent;

[TestClass]
public class UiAnimationPolicyTests {
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Subscribe_ImmediatelyAppliesCurrentState(bool enabled) {
        var policy = new UiAnimationPolicy(enabled);
        var controller = new RecordingController();
        using var subscription = policy.Subscribe(controller);
        CollectionAssert.AreEqual(new[] { enabled }, controller.States);
    }

    [TestMethod]
    public void Subscribe_NotifiesOnlyActiveComponents_AndReappliesAfterReload() {
        var policy = new UiAnimationPolicy(false);
        var controller = new RecordingController();
        var subscription = policy.Subscribe(controller);
        policy.SetRequested(true);
        policy.SetSystemEnabled(false);
        policy.SetSystemEnabled(false);
        subscription.Dispose();
        subscription.Dispose();
        policy.SetSystemEnabled(true);
        policy.SetRequested(false);
        CollectionAssert.AreEqual(new[] { false, true, false }, controller.States);
        using var reloaded = policy.Subscribe(controller);
        policy.SetRequested(true);
        CollectionAssert.AreEqual(new[] { false, true, false, false, true }, controller.States);
    }

    [TestMethod]
    public void Subscribe_InitialControllerFailure_DoesNotKeepSubscription() {
        var policy = new UiAnimationPolicy();
        var controller = new RecordingController { ThrowOnNext = true };
        Assert.ThrowsExactly<InvalidOperationException>(() => policy.Subscribe(controller));
        policy.SetRequested(false);
        CollectionAssert.AreEqual(new[] { true }, controller.States);
    }

    private sealed class RecordingController : IUiAnimationController {
        internal List<bool> States { get; } = [];
        internal bool ThrowOnNext { get; set; }
        public void SetAnimationsEnabled(bool enabled) {
            States.Add(enabled);
            if (!ThrowOnNext) return;
            ThrowOnNext = false;
            throw new InvalidOperationException("Controller initialization failed.");
        }
    }

    [TestMethod]
    [DataRow(true, true, true)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, false)]
    public void EffectiveAnimation_RequiresBothPreferences(bool requested, bool system, bool expected) =>
        Assert.AreEqual(expected, new UiAnimationPolicy(requested, system).IsEnabled);

    [TestMethod]
    public void Notifications_OnlyPublishEffectiveChanges_AndKeepPreferenceWhileSystemDisablesMotion() {
        var policy = new UiAnimationPolicy();
        int changes = 0;
        policy.Changed += (_, _) => changes++;
        policy.SetSystemEnabled(false);
        policy.SetRequested(false);
        policy.SetRequested(true);
        Assert.IsFalse(policy.IsEnabled);
        Assert.AreEqual(1, changes);
        policy.SetSystemEnabled(true);
        Assert.IsTrue(policy.IsEnabled);
        policy.SetSystemEnabled(true);
        Assert.AreEqual(2, changes);
        policy.SetRequested(false);
        policy.SetSystemEnabled(false);
        policy.SetSystemEnabled(true);
        Assert.IsFalse(policy.IsEnabled);
        Assert.AreEqual(3, changes);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void HardwarePreference_DoesNotPreventAnimation(bool gpu) {
        var hardware = new UiHardwareAccelerationPolicy(gpu);
        var animation = new UiAnimationPolicy();
        Assert.AreEqual(gpu, hardware.IsEnabled);
        Assert.IsTrue(animation.IsEnabled);
        animation.SetRequested(false);
        Assert.AreEqual(gpu, hardware.IsEnabled);
    }
}
