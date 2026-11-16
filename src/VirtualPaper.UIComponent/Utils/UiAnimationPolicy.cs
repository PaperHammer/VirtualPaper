using System;

namespace VirtualPaper.UIComponent.Utils;

/// <summary>界面动画同时遵循应用偏好和系统的减少动态效果设置，与 GPU 偏好相互独立。</summary>
public sealed class UiAnimationPolicy(bool requested = true, bool systemEnabled = true) {
    public bool IsEnabled => _requested && _systemEnabled;
    public event EventHandler? Changed;

    /// <summary>立即应用当前策略；调用方在卸载或关闭时释放订阅。</summary>
    public IDisposable Subscribe(IUiAnimationController controller) {
        ArgumentNullException.ThrowIfNull(controller);
        return new Subscription(this, controller);
    }

    private sealed class Subscription : IDisposable {
        private readonly UiAnimationPolicy _policy;
        private IUiAnimationController? _controller;

        internal Subscription(UiAnimationPolicy policy, IUiAnimationController controller) {
            _policy = policy;
            _controller = controller;
            policy.Changed += PolicyChanged;
            try { controller.SetAnimationsEnabled(policy.IsEnabled); }
            catch { Dispose(); throw; }
        }

        private void PolicyChanged(object? sender, EventArgs args) => _controller?.SetAnimationsEnabled(_policy.IsEnabled);

        public void Dispose() {
            _policy.Changed -= PolicyChanged;
            _controller = null;
        }
    }

    public void SetRequested(bool value) {
        bool previous = IsEnabled;
        _requested = value;
        if (previous != IsEnabled) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetSystemEnabled(bool value) {
        bool previous = IsEnabled;
        _systemEnabled = value;
        if (previous != IsEnabled) Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool _requested = requested;
    private bool _systemEnabled = systemEnabled;
}
