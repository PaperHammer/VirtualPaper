using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class ApplicationUi {
    public static IDisposable Foreground(nint window) => new ForegroundLease(window);

    private sealed class ForegroundLease : IDisposable {
        private readonly nint _window;
        private readonly bool _wasTopmost;
        public ForegroundLease(nint window) {
            _window = window;
            _wasTopmost = (GetWindowLongPtr(window, -20).ToInt64() & 8) != 0;
            Check(SetWindowPos(window, -1, 0, 0, 0, 0, 3), "Could not raise the owned test window above desktop overlays");
        }
        public void Dispose() {
            if (!_wasTopmost) SetWindowPos(_window, -2, 0, 0, 0, 0, 0x13);
        }
    }

    public static void Invoke(AutomationElement element) {
        BringIntoView(element);
        ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    public static void Expand(AutomationElement element) {
        BringIntoView(element);
        ((ExpandCollapsePattern)element.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
    }

    public static void SetText(AutomationElement element, string value) {
        BringIntoView(element);
        if (element.Current.ControlType != ControlType.Edit)
            element = element.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)) ?? element;
        if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)) {
            element = element.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit))
                ?? throw new InvalidOperationException("No editable text control");
            pattern = element.GetCurrentPattern(ValuePattern.Pattern);
        }
        ((ValuePattern)pattern).SetValue(value);
    }

    public static void OpenContextMenu(nint ownerWindow, AutomationElement element) {
        BringIntoView(element);
        Check(SetForegroundWindow(ownerWindow) || GetForegroundWindow() == ownerWindow, "Could not activate the test UI");
        element.SetFocus();
        keybd_event(0x10, 0, 0, 0);
        try {
            keybd_event(0x79, 0, 0, 0);
            keybd_event(0x79, 0, 2, 0);
        }
        finally { keybd_event(0x10, 0, 2, 0); }
    }

    public static void DismissFlyout(nint ownerWindow) {
        Check(SetForegroundWindow(ownerWindow) || GetForegroundWindow() == ownerWindow, "Could not activate the test UI");
        keybd_event(0x1B, 0, 0, 0);
        keybd_event(0x1B, 0, 2, 0);
    }

    public static void WaitUntilGone(AutomationElement root, string id) {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(5)) {
            try {
                if (Matches(root, id, includePopups: true).All(element => element.Current.IsOffscreen)) return;
            }
            catch (ElementNotAvailableException) {
                try { _ = root.Current.ProcessId; }
                catch (ElementNotAvailableException) { return; } // The containing window itself closed.
            }
            Thread.Sleep(100);
        }
        throw new TimeoutException($"UI element did not close: {id}");
    }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, nuint extraInfo);

    public static AutomationElement Find(AutomationElement root, string id, bool visible = false, bool enabled = true) {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10)) {
            try {
                var element = Matches(root, id, includePopups: visible).FirstOrDefault(item => (!enabled || item.Current.IsEnabled) && (!visible || !item.Current.IsOffscreen));
                if (element != null) return element;
            }
            catch (ElementNotAvailableException) { }
            Thread.Sleep(100);
        }
        throw new TimeoutException($"UI Automation element not found: {id}");
    }

    public static AutomationElement FindWindow(int processId, string descendantId) {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10)) {
            foreach (AutomationElement window in AutomationElement.RootElement.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ProcessIdProperty, processId))) {
                try {
                    if (window.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.AutomationIdProperty, descendantId)) is { } control
                        && !control.Current.IsOffscreen) return window;
                }
                catch (ElementNotAvailableException) { }
            }
            Thread.Sleep(100);
        }
        throw new TimeoutException($"Window in process {processId} with control {descendantId} not found");
    }

    private static IEnumerable<AutomationElement> Matches(AutomationElement root, string id, bool includePopups) {
        var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, id);
        foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, condition)) yield return element;
        if (!includePopups) yield break;
        // WinUI can expose a flyout as a separate top-level automation window,
        // especially after another flyout has reused its popup host. Stay within
        // this exact UI process; never search other running application instances.
        foreach (AutomationElement window in AutomationElement.RootElement.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ProcessIdProperty, root.Current.ProcessId))) {
            if (window.Equals(root)) continue;
            foreach (AutomationElement element in window.FindAll(TreeScope.Descendants, condition)) yield return element;
        }
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

    public static string SelectComboIndex(AutomationElement combo, int index, int count, bool closesWindow = false) {
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
        finally {
            // Settings such as language can synchronously close their UI as a
            // result of Select. Their caller must observe the actual restart.
            if (!closesWindow) expand.Collapse();
        }
    }

    public static string SelectedLabel(AutomationElement combo) {
        var selected = ((SelectionPattern)combo.GetCurrentPattern(SelectionPattern.Pattern)).Current.GetSelection();
        Check(selected.Length == 1, $"Expected one selection in {combo.Current.AutomationId}");
        return selected[0].Current.Name;
    }
}
