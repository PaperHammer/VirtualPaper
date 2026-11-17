using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VirtualPaper.UIComponent.Utils;

/// <summary>只用于组件明确提供的模板根，不遍历页面或控件内容。</summary>
internal sealed class UiAnimationStateManager : VisualStateManager {
    internal static void Apply(FrameworkElement? templateRoot, bool enabled) {
        if (templateRoot == null) return;
        var current = GetCustomVisualStateManager(templateRoot);
        if (enabled) {
            if (current is UiAnimationStateManager) SetCustomVisualStateManager(templateRoot, null);
            return;
        }
        if (current != null) return;
        SetCustomVisualStateManager(templateRoot, new UiAnimationStateManager());
        // 这里只枚举当前组件自己的几个状态，不查找子组件；持续加载反馈不受影响。
        foreach (var group in GetVisualStateGroups(templateRoot)) {
            foreach (var transition in group.Transitions) transition.Storyboard?.SkipToFill();
            group.CurrentState?.Storyboard?.SkipToFill();
        }
    }

    protected override bool GoToStateCore(Control control, FrameworkElement templateRoot, string stateName,
        VisualStateGroup group, VisualState state, bool useTransitions) {
        bool result = base.GoToStateCore(control, templateRoot, stateName, group, state, false);
        if (result) state.Storyboard?.SkipToFill();
        return result;
    }
}
