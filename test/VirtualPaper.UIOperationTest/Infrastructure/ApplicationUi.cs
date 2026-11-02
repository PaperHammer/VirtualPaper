using System.Diagnostics;
using System.Windows.Automation;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class ApplicationUi {
    public static AutomationElement Find(AutomationElement root, string id) {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10)) {
            var element = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id));
            if (element != null && element.Current.IsEnabled) return element;
            Thread.Sleep(100);
        }
        throw new TimeoutException($"UI Automation element not found: {id}");
    }

    public static void Select(AutomationElement element) {
        BringIntoView(element);
        if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selection))
            ((SelectionItemPattern)selection).Select();
        else
            ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    public static void BringIntoView(AutomationElement element) {
        if (!element.Current.IsOffscreen) return;
        if (element.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var scrollItem)) {
            ((ScrollItemPattern)scrollItem).ScrollIntoView();
            for (int i = 0; element.Current.IsOffscreen && i < 10; i++) Thread.Sleep(100);
            Check(!element.Current.IsOffscreen, $"Element stayed outside the viewport: {element.Current.AutomationId}");
            return;
        }
        var parent = TreeWalker.ControlViewWalker.GetParent(element);
        while (parent != null) {
            if (parent.TryGetCurrentPattern(ScrollPattern.Pattern, out var scrolling)) {
                var scroll = (ScrollPattern)scrolling;
                if (scroll.Current.VerticallyScrollable) {
                    scroll.SetScrollPercent(ScrollPattern.NoScroll, 0);
                    Thread.Sleep(100);
                }
                for (int i = 0; element.Current.IsOffscreen && scroll.Current.VerticallyScrollable && i < 10; i++) {
                    scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.LargeIncrement);
                    Thread.Sleep(100);
                }
                Check(!element.Current.IsOffscreen, $"Could not scroll to {element.Current.AutomationId}");
                return;
            }
            parent = TreeWalker.ControlViewWalker.GetParent(parent);
        }
        Check(!element.Current.IsOffscreen, $"No scroll container for {element.Current.AutomationId}");
    }

    public static string SelectComboIndex(AutomationElement combo, int index, int count) {
        BringIntoView(combo);
        var expand = (ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern);
        expand.Expand();
        try {
            var timer = Stopwatch.StartNew();
            AutomationElementCollection items;
            do {
                items = combo.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
                if (items.Count == count) break;
                Thread.Sleep(100);
            } while (timer.Elapsed < TimeSpan.FromSeconds(5));
            Check(items.Count == count, $"Expected {count} options in {combo.Current.AutomationId}, found {items.Count}");
            string label = items[index].Current.Name;
            ((SelectionItemPattern)items[index].GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            return label;
        }
        finally { expand.Collapse(); }
    }

    public static string SelectedLabel(AutomationElement combo) {
        var selected = ((SelectionPattern)combo.GetCurrentPattern(SelectionPattern.Pattern)).Current.GetSelection();
        Check(selected.Length == 1, $"Expected one selection in {combo.Current.AutomationId}");
        return selected[0].Current.Name;
    }
}
