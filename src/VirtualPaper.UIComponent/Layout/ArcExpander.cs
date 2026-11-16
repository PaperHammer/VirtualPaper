using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Layout;

/// <summary>保留 WinUI 原生模板与交互，仅适配自己的展开过渡、箭头及菜单。</summary>
public class ArcExpander : Expander, IUiAnimationController {
    private FrameworkElement? _templateRoot;
    private AnimatedIcon? _chevron;
    private Border? _chevronHost;
    private Grid? _chevronPanel;
    private object? _animatedSource;
    private FontIcon? _staticChevron;
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
        _chevronHost = root?.FindName("ExpandCollapseChevronBorder") as Border;
        _animatedSource = _chevron?.Source;
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
        if (_chevron == null || _chevronHost == null || _animatedSource == null) return;
        if (_animationsEnabled) {
            _chevron.SetValue(AnimatedIcon.SourceProperty, _animatedSource);
            _chevron.Visibility = Visibility.Visible;
            if (_staticChevron != null) _staticChevron.Visibility = Visibility.Collapsed;
            return;
        }
        if (_staticChevron == null) {
            _staticChevron = new FontIcon {
                Name = "StaticExpandCollapseChevron",
                FontSize = 12, IsTextScaleFactorEnabled = false,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            if (_chevron.FallbackIconSource is FontIconSource original) _staticChevron.FontFamily = original.FontFamily;
            _staticChevron.SetBinding(IconElement.ForegroundProperty, new Binding {
                Source = _chevron, Path = new PropertyPath(nameof(IconElement.Foreground))
            });
            // 原生箭头保持在模板内继承亮暗主题，静态箭头复用其前景色。
            _chevronPanel = new Grid();
            _chevronHost.Child = null;
            _chevronPanel.Children.Add(_chevron);
            _chevronPanel.Children.Add(_staticChevron);
            _chevronHost.Child = _chevronPanel;
        }
        bool up = IsExpanded != (ExpandDirection == ExpandDirection.Up);
        _staticChevron.Glyph = up ? "\uE70E" : "\uE70D";
        // 清空动画源不会保证显示 FallbackIconSource；直接放入静态箭头。
        _chevron.Source = null;
        _chevron.Visibility = Visibility.Collapsed;
        _staticChevron.Visibility = Visibility.Visible;
    }

    private void RestoreChevron() {
        if (_chevron != null && _animatedSource != null) {
            if (_chevronHost != null && _chevronPanel != null && ReferenceEquals(_chevronHost.Child, _chevronPanel)) {
                _chevronPanel.Children.Clear();
                _chevronHost.Child = _chevron;
            }
            _chevron.Visibility = Visibility.Visible;
            _chevron.SetValue(AnimatedIcon.SourceProperty, _animatedSource);
        }
        _staticChevron?.ClearValue(IconElement.ForegroundProperty);
        _staticChevron = null;
        _chevron = null;
        _chevronHost = null;
        _chevronPanel = null;
        _animatedSource = null;
    }

    private void UpdateFlyout() {
        var target = !_animationsEnabled && IsLoaded ? ContextFlyout : null;
        if (ReferenceEquals(target, _suppressedFlyout)) return;
        _flyoutLease?.Dispose();
        _suppressedFlyout = target;
        _flyoutLease = target == null ? null : UiFlyoutAnimation.Suppress(target);
    }
}
