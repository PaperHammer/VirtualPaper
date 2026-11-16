using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace VirtualPaper.UIComponent.Utils;

/// <summary>仅在主 UI 中初始化；系统通知切回 UI 线程，不修改 Windows 的全局动画设置。</summary>
public static class UiAnimation {
    public static UiAnimationPolicy Policy { get; } = new();
    public static bool IsEnabled => Policy.IsEnabled;

    /// <summary>仅由实现接口的组件在构造时调用一次；不扫描或探测子控件。</summary>
    public static void Attach<T>(T component) where T : FrameworkElement, IUiAnimationController =>
        _ = new ElementBinding(component, component);

    private sealed class ElementBinding {
        private readonly FrameworkElement _element;
        private readonly IUiAnimationController _controller;
        private IDisposable? _subscription;

        internal ElementBinding(FrameworkElement element, IUiAnimationController controller) {
            _element = element;
            _controller = controller;
            element.Loaded += Loaded;
            element.Unloaded += Unloaded;
            if (element.IsLoaded) Loaded(element, new RoutedEventArgs());
        }

        private void Loaded(object sender, RoutedEventArgs args) {
            if (_subscription != null) return;
            var subscription = Policy.Subscribe(_controller);
            if (_element.IsLoaded) _subscription = subscription;
            else subscription.Dispose();
        }

        private void Unloaded(object sender, RoutedEventArgs args) {
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    public static void Initialize(bool enabled) {
        Shutdown();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _system = new UISettings();
        Policy.SetRequested(enabled);
        _system.AnimationsEnabledChanged += SystemAnimationsChanged;
        Policy.SetSystemEnabled(_system.AnimationsEnabled);
    }

    private static void SystemAnimationsChanged(UISettings sender, UISettingsAnimationsEnabledChangedEventArgs args) {
        _dispatcher?.TryEnqueue(() => {
            if (ReferenceEquals(sender, _system)) Policy.SetSystemEnabled(sender.AnimationsEnabled);
        });
    }

    public static void Shutdown() {
        if (_system != null) _system.AnimationsEnabledChanged -= SystemAnimationsChanged;
        _system = null;
        _dispatcher = null;
    }

    private static UISettings? _system;
    private static DispatcherQueue? _dispatcher;
}
