using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using VirtualPaper.Common;
using VirtualPaper.TestSupport;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class ApplicationUi {
    public static async Task SelectLanguageAsync(AutomationElement root, string locale) {
        var language = SupportedLanguages.Languages.Single(item => item.Codes[0] == locale);
        string expected = LocalizedTestStrings.Read(AppContext.BaseDirectory, locale, "AppSettings_SelBarItem1_General");
        await Task.Run(() => {
            Select(Find(root, "App.Navigation.Settings"));
            SelectComboIndex(Find(root, "Settings.General.Language"), SupportedLanguages.Languages.IndexOf(language), SupportedLanguages.Languages.Count);
        });
        await AppProcessSession.WaitAsync(() => Task.Run(() => Find(root, "Settings.Tab.General").Current.Name == expected
            && Find(root, "Settings.General.Language").Current.IsEnabled), "Live language " + locale);
    }

    public static void ResizeOwnedWindow(nint window, int width, int height, bool physicalPixels = false) {
        double scale = physicalPixels ? 1 : GetDpiForWindow(window) / 96d;
        Check(scale > 0 && SetWindowPos(window, 0, 0, 0, (int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 6),
            "Could not resize the owned application window");
    }

    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    public static IDisposable Foreground(nint window) => new ForegroundLease(window);

    public static async Task ActivateOwnedWindowAsync(nint window) {
        using var foreground = Foreground(window);
        if (SetForegroundWindow(window) || GetForegroundWindow() == window) return;
        // Windows may deny programmatic activation after a background build.
        // Click only this verified window's title bar to give it real input focus.
        var bounds = AutomationElement.FromHandle(window).Current.BoundingRectangle;
        var point = new NativePoint { X = (int)(bounds.Left + 80), Y = (int)(bounds.Top + 12) };
        Check(GetAncestor(WindowFromPoint(point), 2) == window, "The test host title bar is covered by another window");
        Check(GetCursorPos(out var previous) && SetCursorPos(point.X, point.Y), "Could not position the pointer over the test host");
        try {
            mouse_event(0x0002, 0, 0, 0, 0);
            mouse_event(0x0004, 0, 0, 0, 0);
            var timer = Stopwatch.StartNew();
            while (GetForegroundWindow() != window && timer.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(50);
            Check(GetForegroundWindow() == window, "The test host did not receive foreground input focus");
        }
        finally { SetCursorPos(previous.X, previous.Y); }
    }

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

    public static async Task VerifyToolTipAsync(nint ownerWindow, AutomationElement element, string expected) {
        using var foreground = Foreground(ownerWindow);
        Check(SetForegroundWindow(ownerWindow) || GetForegroundWindow() == ownerWindow, "Could not activate tooltip owner");
        var bounds = element.Current.BoundingRectangle;
        var point = new NativePoint { X = (int)(bounds.Left + bounds.Width / 2), Y = (int)(bounds.Top + bounds.Height / 2) };
        Check(GetAncestor(WindowFromPoint(point), 2) == ownerWindow, "The tooltip target is covered by another window");
        Check(GetCursorPos(out var previous) && SetCursorPos(point.X, point.Y), "Could not hover the owned tooltip target");
        try {
            int processId = element.Current.ProcessId;
            await AppProcessSession.WaitAsync(() => Task.Run(() => {
                try {
                    var windows = AutomationElement.RootElement.FindAll(TreeScope.Children,
                        new PropertyCondition(AutomationElement.ProcessIdProperty, processId)).Cast<AutomationElement>();
                    foreach (var window in windows) {
                        IEnumerable<AutomationElement> tips = window.Current.ControlType == ControlType.ToolTip ? [window] : window.FindAll(TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolTip)).Cast<AutomationElement>();
                        foreach (var tip in tips.Where(tip => !tip.Current.IsOffscreen)) {
                            if (tip.Current.Name == expected || tip.FindAll(TreeScope.Descendants,
                                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)).Cast<AutomationElement>()
                                .Any(text => text.Current.Name == expected)) return true;
                        }
                    }
                }
                catch (ElementNotAvailableException) { } // A previous language's tooltip may close during enumeration.
                return false;
            }), "Localized navigation tooltip: " + expected);
        }
        finally { SetCursorPos(previous.X, previous.Y); }
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
        var keys = new NativeInput[] {
            new() { Type = 1, Data = new() { Keyboard = new() { Key = 0x10 } } },
            new() { Type = 1, Data = new() { Keyboard = new() { Key = 0x79 } } },
            new() { Type = 1, Data = new() { Keyboard = new() { Key = 0x79, Flags = 2 } } },
            new() { Type = 1, Data = new() { Keyboard = new() { Key = 0x10, Flags = 2 } } }
        };
        Check(GetForegroundWindow() == ownerWindow, "The wallpaper window lost focus before opening its menu.");
        Check(SendInput((uint)keys.Length, keys, Marshal.SizeOf<NativeInput>()) == keys.Length,
            "Could not send the complete context-menu key sequence.");
    }

    public static void OpenPointerContextMenu(nint ownerWindow, AutomationElement element) => ClickPointer(ownerWindow, element, 0x0008, 0x0010);

    public static void ClickPointerElement(nint ownerWindow, AutomationElement element) {
        using var foreground = Foreground(ownerWindow);
        ClickPointer(ownerWindow, element, 0x0002, 0x0004);
    }

    public static void MovePointerOverOwnedWindow(nint ownerWindow) {
        using var foreground = Foreground(ownerWindow);
        Check(SetForegroundWindow(ownerWindow) || GetForegroundWindow() == ownerWindow, "Could not activate the owned screen saver");
        var bounds = AutomationElement.FromHandle(ownerWindow).Current.BoundingRectangle;
        var point = new NativePoint { X = (int)(bounds.Left + bounds.Width / 2), Y = (int)(bounds.Top + bounds.Height / 2) };
        Check(GetAncestor(WindowFromPoint(point), 2) == ownerWindow, "Screen saver wake target is covered by another window");
        Check(GetCursorPos(out var previous) && SetCursorPos(point.X, point.Y), "Could not position wake pointer");
        try { mouse_event(0x0001, 1, 0, 0, 0); }
        finally { SetCursorPos(previous.X, previous.Y); }
    }

    private static void ClickPointer(nint ownerWindow, AutomationElement element, uint down, uint up) {
        BringIntoView(element);
        Check(SetForegroundWindow(ownerWindow) || GetForegroundWindow() == ownerWindow, "Could not activate the test UI");
        var bounds = element.Current.BoundingRectangle;
        var point = new NativePoint { X = (int)(bounds.Left + Math.Min(80, bounds.Width / 2)),
            Y = (int)(bounds.Top + Math.Min(12, bounds.Height / 2)) };
        nint hit = WindowFromPoint(point);
        GetWindowThreadProcessId(hit, out int processId);
        Check(processId == element.Current.ProcessId || GetAncestor(hit, 2) == ownerWindow,
            "Pointer target is covered by another application's window");
        Check(GetCursorPos(out var previous) && SetCursorPos(point.X, point.Y), "Could not position the pointer over the owned row");
        try {
            mouse_event(down, 0, 0, 0, 0);
            mouse_event(up, 0, 0, 0, 0);
        }
        finally { SetCursorPos(previous.X, previous.Y); }
    }

    public static void TypeAtEditorSurface(nint ownerWindow, AutomationElement surface, string text) {
        using var foreground = Foreground(ownerWindow);
        ClickPointer(ownerWindow, surface, 0x0002, 0x0004);
        var focusTimer = Stopwatch.StartNew();
        while (true) {
            var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
            var name = new System.Text.StringBuilder(256);
            if (GetGUIThreadInfo(0, ref info)) GetClassName(info.Focus, name, name.Capacity);
            if (name.ToString().StartsWith("Chrome_", StringComparison.Ordinal) && GetAncestor(info.Focus, 2) == ownerWindow) break;
            Check(focusTimer.Elapsed < TimeSpan.FromSeconds(5), "The editor browser did not receive native keyboard focus: " + name);
            Thread.Sleep(20);
        }
        Check(GetForegroundWindow() == ownerWindow, "The owned editor window lost activation before typing");
        keybd_event(0x11, 0, 0, 0);
        try {
            keybd_event(0x24, 0, 1, 0);
            keybd_event(0x24, 0, 3, 0);
        }
        finally { keybd_event(0x11, 0, 2, 0); }
        var input = new NativeInput[text.Length * 2];
        for (int i = 0; i < text.Length; i++) {
            input[i * 2] = new() { Type = 1, Data = new() { Keyboard = new() { Scan = text[i], Flags = 4 } } };
            input[i * 2 + 1] = new() { Type = 1, Data = new() { Keyboard = new() { Scan = text[i], Flags = 6 } } };
        }
        Check(GetForegroundWindow() == ownerWindow, "The owned editor window lost activation during typing");
        for (int i = 0; i < input.Length; i += 2) {
            Check(GetForegroundWindow() == ownerWindow, "The owned editor window lost activation during typing");
            Check(SendInput(2, [input[i], input[i + 1]], Marshal.SizeOf<NativeInput>()) == 2,
                "Could not send text to the owned editor surface: " + Marshal.GetLastWin32Error());
            Thread.Sleep(20); // Pace real key events so browser input is not flooded in one dispatch.
        }
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
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, nuint extraInfo);
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out int processId);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, nuint extraInfo);
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public NativeInputData Data; }
    [StructLayout(LayoutKind.Explicit)] private struct NativeInputData {
        [FieldOffset(0)] public NativeKeyboard Keyboard;
        [FieldOffset(0)] public NativeMouse Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeKeyboard { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeMouse { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)] private struct GuiThreadInfo {
        public uint Size, Flags;
        public nint Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public int Left, Top, Right, Bottom;
    }
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, System.Text.StringBuilder name, int count);

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

    public static AutomationElement DialogButton(AutomationElement root, string id) {
        // TabView also exposes a CloseButton. Wait for the real dialog and
        // restrict lookup to it instead of accidentally closing an editor tab.
        var parent = Find(root, "PrimaryButton", visible: true);
        while ((parent = TreeWalker.RawViewWalker.GetParent(parent)) != null) {
            var buttons = parent.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
                .Cast<AutomationElement>().Where(e => !e.Current.IsOffscreen && e.Current.IsEnabled).ToArray();
            if (!buttons.Any(e => e.Current.AutomationId == "SecondaryButton")) continue;
            var matches = buttons.Where(e => e.Current.AutomationId == id).ToArray();
            if (matches.Length == 1) return matches[0];
        }
        throw new InvalidOperationException("Unsaved dialog ancestor not found");
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
            // Backdrop changes close the UI; language changes temporarily disable
            // this control while saving. Neither permits a trailing Collapse call.
            if (!closesWindow && combo.Current.IsEnabled) expand.Collapse();
        }
    }

    public static string SelectedLabel(AutomationElement combo) {
        var selected = ((SelectionPattern)combo.GetCurrentPattern(SelectionPattern.Pattern)).Current.GetSelection();
        Check(selected.Length == 1, $"Expected one selection in {combo.Current.AutomationId}");
        return selected[0].Current.Name;
    }
}
