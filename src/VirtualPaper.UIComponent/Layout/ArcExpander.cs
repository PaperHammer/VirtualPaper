using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Layout;

/// <summary>保留 WinUI 原生模板与交互，仅适配自己的展开过渡、箭头及菜单。</summary>
public class ArcExpander : Expander, IUiAnimationController {
    private FrameworkElement? _templateRoot;
    private AnimatedIcon? _chevron;
    private object? _animatedSource;
    private IconSource? _fallback;
    private FontIconSource? _staticChevron;
    private bool _animationsEnabled = UiAnimation.IsEnabled;
    private FlyoutBase? _suppressedFlyout;
    private IDisposable? _flyoutLease;

    public ArcExpander() {
        DefaultStyleKey = typeof(Expander);
        UiAnimation.Attach(this);
        Unloaded += ArcExpander_Unloaded;
        RegisterPropertyChangedCallback(IsExpandedProperty, (_, _) => UpdateChevron());
        RegisterPropertyChangedCallback(ExpandDirectionProperty, (_, _) => {
            RestoreChevron();
            ResolveChevron();
            UpdateChevron();
        });
        RegisterPropertyChangedCallback(ContextFlyoutProperty, (_, _) => UpdateFlyout());
    }

    protected override void OnApplyTemplate() {
        UiAnimationStateManager.Apply(_templateRoot, true);
        RestoreChevron();
        base.OnApplyTemplate();
        // 只读取原生模板已知的根和具名部件，不枚举内容区。
        _templateRoot = VisualTreeHelper.GetChildrenCount(this) > 0 ? VisualTreeHelper.GetChild(this, 0) as FrameworkElement : null;
        ResolveChevron();
        SetAnimationsEnabled(_animationsEnabled);
    }

    private void ResolveChevron() {
        if (GetTemplateChild("ExpanderHeader") is not ToggleButton header) return;
        header.ApplyTemplate();
        if (VisualTreeHelper.GetChildrenCount(header) == 0) return;
        var root = VisualTreeHelper.GetChild(header, 0) as FrameworkElement;
        _chevron = root?.FindName("ExpandCollapseChevron") as AnimatedIcon;
        _animatedSource = _chevron?.Source;
        _fallback = _chevron?.FallbackIconSource;
    }

    public void SetAnimationsEnabled(bool enabled) {
        _animationsEnabled = enabled;
        UiAnimationStateManager.Apply(_templateRoot, enabled);
        UpdateChevron();
        UpdateFlyout();
    }

    private void ArcExpander_Unloaded(object sender, RoutedEventArgs args) {
        // 卸载只清理模板接入和菜单租约，不重新创建动画箭头；下次加载再应用策略。
        UiAnimationStateManager.Apply(_templateRoot, true);
        _flyoutLease?.Dispose();
        _flyoutLease = null;
        _suppressedFlyout = null;
    }

    private void UpdateChevron() {
        if (_chevron == null || _animatedSource == null || _fallback == null) return;
        if (_animationsEnabled) {
            _chevron.FallbackIconSource = _fallback;
            _chevron.SetValue(AnimatedIcon.SourceProperty, _animatedSource);
            return;
        }
        _staticChevron ??= new FontIconSource { FontSize = 12 };
        if (_fallback is FontIconSource original) _staticChevron.FontFamily = original.FontFamily;
        bool up = IsExpanded != (ExpandDirection == ExpandDirection.Up);
        _staticChevron.Glyph = up ? "\uE70E" : "\uE70D";
        _chevron.FallbackIconSource = _staticChevron;
        _chevron.Source = null;
    }

    private void RestoreChevron() {
        if (_chevron != null && _animatedSource != null) {
            _chevron.FallbackIconSource = _fallback;
            _chevron.SetValue(AnimatedIcon.SourceProperty, _animatedSource);
        }
        _chevron = null;
        _animatedSource = null;
        _fallback = null;
    }

    private void UpdateFlyout() {
        var target = !_animationsEnabled && IsLoaded ? ContextFlyout : null;
        if (ReferenceEquals(target, _suppressedFlyout)) return;
        _flyoutLease?.Dispose();
        _suppressedFlyout = target;
        _flyoutLease = target == null ? null : UiFlyoutAnimation.Suppress(target);
    }
}
