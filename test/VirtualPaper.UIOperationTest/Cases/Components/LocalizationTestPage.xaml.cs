using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace VirtualPaper.UIOperationTest.Cases;

public sealed partial class LocalizationTestPage : Page {
    internal Border TestSurface => Surface;
    public LocalizationTestPage() => InitializeComponent();

    internal void ShowContent(UIElement? content) {
        if (Surface.Child != null) ReleaseLocalization(Surface.Child);
        Surface.Child = content;
    }

    // Discarded fixtures must stop participating in later language changes.
    // The library otherwise keeps their managed wrappers until GC, after native
    // teardown has begun. This does not alter cached production pages.
    internal static void ReleaseLocalization(DependencyObject root) {
        root.ClearValue(WinUI3Localizer.Uids.UidProperty);
        if (ToolTipService.GetToolTip(root) is DependencyObject tip) ReleaseLocalization(tip);
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            ReleaseLocalization(VisualTreeHelper.GetChild(root, i));
    }
}
