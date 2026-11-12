using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Layout;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class InterfaceAnimationTests {
    // 与 NavigationSettings 使用同一验收项；实际模板行为不能用无界面策略测试替代。
    public static async Task VerifyControlsAsync(UiTestContext context) {
        var page = await context.OpenAsync<LocalizationTestPage>(new());
        var flyout = new Flyout { Content = new TextBlock { Text = "Popup content" } };
        var expander = new ArcExpander { Header = "Animation test", Content = new TextBlock { Text = "Expanded content" }, ContextFlyout = flyout };
        try {
            UiAnimation.Policy.SetSystemEnabled(true);
            UiAnimation.Policy.SetRequested(true);
            page.TestSurface.Child = expander;
            await context.WaitAsync(() => expander.IsLoaded && VisualTreeHelper.GetChildrenCount(expander) > 0);
            await context.WaitForLayoutAsync();
            var root = (FrameworkElement)VisualTreeHelper.GetChild(expander, 0);
            Check(VisualStateManager.GetCustomVisualStateManager(root) == null,
                "Enabled animations unnecessarily installed a template state manager");
            Check(flyout.AreOpenCloseAnimationsEnabled, "Enabled animations changed the default flyout behavior");
            // 接口参数由组件自身执行，不依赖再次读取全局偏好来猜测当前状态。
            ((IUiAnimationController)expander).SetAnimationsEnabled(false);
            Check(VisualStateManager.GetCustomVisualStateManager(root) != null,
                "The component ignored the interface's disabled state");
            expander.IsExpanded = true;
            var content = Descendants<FrameworkElement>(expander).Single(item => item.Name == "ExpanderContent");
            Check(content.Visibility == Visibility.Visible && content.RenderTransform is CompositeTransform { TranslateY: 0 },
                "The explicit component state did not complete expansion");
            expander.IsExpanded = false;
            Check(content.Visibility == Visibility.Collapsed, "The explicit component state did not complete collapse");
            expander.SetAnimationsEnabled(true);
            UiAnimation.Policy.SetRequested(false);
            Check(VisualStateManager.GetCustomVisualStateManager(root) != null, "Expander did not attach the animation policy");
            Check(!flyout.AreOpenCloseAnimationsEnabled, "Disabling animations did not affect the context flyout");

            expander.IsExpanded = true;
            Check(content.Visibility == Visibility.Visible, "Animation-off expansion hid its content");
            Check(content.RenderTransform is CompositeTransform { TranslateY: 0 }, "Expansion did not reach its final position immediately");
            expander.IsExpanded = false;
            Check(content.Visibility == Visibility.Collapsed, "Animation-off collapse still waited for the closing animation");
            var icon = Descendants<AnimatedIcon>(expander).First();
            Check(icon.Source == null && icon.FallbackIconSource is FontIconSource { Glyph: "\uE70D" },
                "Collapsed expander did not show its static direction");
            var staticArrow = icon.FallbackIconSource;
            expander.IsExpanded = true;
            Check(ReferenceEquals(staticArrow, icon.FallbackIconSource) && staticArrow is FontIconSource { Glyph: "\uE70E" },
                "Expanding allocated another static arrow instead of updating its direction");
            expander.IsExpanded = false;

            UiAnimation.Policy.SetRequested(true);
            Check(icon.Source != null, "Re-enabling animation did not restore the animated chevron");
            Check(VisualStateManager.GetCustomVisualStateManager(root) == null && flyout.AreOpenCloseAnimationsEnabled,
                "Re-enabling animation retained template interception or changed the flyout default");
            expander.IsExpanded = true;
            UiAnimation.Policy.SetRequested(false);
            Check(content.Visibility == Visibility.Visible && content.RenderTransform is CompositeTransform { TranslateY: 0 },
                "Disabling during expansion did not finish the transition");
            Check(icon.FallbackIconSource is FontIconSource { Glyph: "\uE70E" }, "Expanded arrow points in the wrong direction");

            page.TestSurface.Child = null;
            await context.WaitAsync(() => !expander.IsLoaded);
            Check(VisualStateManager.GetCustomVisualStateManager(root) == null, "Unloading retained the animation registration");
            Check(icon.Source == null, "Unloading unnecessarily restarted the animated chevron");
            page.TestSurface.Child = expander;
            await context.WaitAsync(() => expander.IsLoaded);
            expander.IsExpanded = false;
            Check(content.Visibility == Visibility.Collapsed, "Reloading lost the animation policy");
            UiAnimation.Policy.SetRequested(true);
            page.TestSurface.Child = null;
            await context.WaitAsync(() => !expander.IsLoaded);
            page.TestSurface.Child = expander;
            await context.WaitAsync(() => expander.IsLoaded);
            Check(VisualStateManager.GetCustomVisualStateManager(root) == null,
                "Reloading with animation enabled unnecessarily restored template interception");
            // 两个控件共享菜单时，卸载其中一个仍须保留关闭状态，全部释放后恢复原值。
            page.TestSurface.Child = null;
            await context.WaitAsync(() => !expander.IsLoaded);
            var second = new ArcExpander { Header = "Shared menu", ContextFlyout = flyout };
            var stack = new StackPanel { Children = { expander, second } };
            UiAnimation.Policy.SetRequested(false);
            page.TestSurface.Child = stack;
            await context.WaitAsync(() => expander.IsLoaded && second.IsLoaded);
            stack.Children.Remove(second);
            await context.WaitAsync(() => !second.IsLoaded);
            Check(!flyout.AreOpenCloseAnimationsEnabled, "Unloading one owner restored a shared flyout too early");
            UiAnimation.Policy.SetRequested(true);
            Check(flyout.AreOpenCloseAnimationsEnabled, "Re-enabling animation did not restore the shared flyout default");
            context.Evidence["ExpanderImmediateStateChevronToggleAndUnloadVerified"] = true;
            context.Evidence["AnimationEnabledFastPathStaticIconReuseAndFlyoutRestoreVerified"] = true;
        }
        finally {
            page.TestSurface.Child = null;
            UiAnimation.Policy.SetRequested(true);
            UiAnimation.Policy.SetSystemEnabled(new Windows.UI.ViewManagement.UISettings().AnimationsEnabled);
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) yield return result;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
