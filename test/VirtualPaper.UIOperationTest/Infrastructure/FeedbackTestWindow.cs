using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.UIComponent.Navigation;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIOperationTest.Infrastructure;

// A real, owned window for component acceptance. Application cases continue to
// use AppProcessSession and the installed Core/UI production launch path.
internal sealed class FeedbackTestWindow : ArcWindow {
    public override ArcWindowHost ContentHost { get; } = new();
    public override ArcWindowManagerKey Key => new(ArcWindowKey.Main);
    internal ArcNavigationContentView Surface { get; }

    internal FeedbackTestWindow(UIElement content) {
        UiTestContext.Check(ArcWindowManager.GetArcWindow(Key) == null, "Another component fixture still owns the main-window key");
        Surface = new ArcNavigationContentView { InfobarMessages = InfobarMessages };
        Surface.ContentGrid.Children.Add(content);
        ContentHost.RootContent = Surface;
        Content = ContentHost;
        Width = 900;
        Height = 700;
        // Test the production content/owner registration without depending on
        // the installed application's shell icon or native title-bar setup.
        ArcWindowManager.TrackWindow(Key, this);
    }

    internal async Task ShowAsync(UiTestContext context) {
        Activate();
        await ApplicationUi.ActivateOwnedWindowAsync(WinRT.Interop.WindowNative.GetWindowHandle(this));
        await context.WaitAsync(() => Surface.IsLoaded && Surface.ActualWidth > 0, "Lay out feedback fixture");
        await context.WaitForLayoutAsync();
    }

    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var item in Descendants<T>(child)) yield return item;
        }
    }
}
