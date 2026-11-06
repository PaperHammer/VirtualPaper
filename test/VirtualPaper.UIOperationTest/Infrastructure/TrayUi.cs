using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using VirtualPaper.Common;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class TrayUi {
    internal sealed record OpenMenu(AutomationElement Element, nint Window);

    public static OpenMenu Open(int coreId) {
        string name = $"Virtual Paper ({coreId}:{TestProcessEnvironment.SessionId})";
        nint taskbar = FindWindow("Shell_TrayWnd", null);
        Check(taskbar != 0, "Explorer taskbar is not available");
        (AutomationElement Element, nint Window)? icon = null;
        var discovery = Stopwatch.StartNew();
        TimeSpan lastExpand = TimeSpan.FromSeconds(-1);
        // Explorer registers notifications asynchronously. Shell versions expose
        // different notification providers and capitalization in button names.
        while (icon == null && discovery.Elapsed < TimeSpan.FromSeconds(10)) {
            try {
                var windows = TrayWindows();
                icon = FindIcon(name, windows);
                if (icon != null) break;
                bool overflowOpen = windows.Any(w => IsWindowVisible(w) && IsOverflowWindow(w));
                if (!overflowOpen && discovery.Elapsed - lastExpand >= TimeSpan.FromSeconds(1)) {
                    foreach (var handle in windows.Where(w => !IsOverflowWindow(w))) {
                        var overflow = AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants, new OrCondition(
                            new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTray.OverflowButton"),
                            new PropertyCondition(AutomationElement.AutomationIdProperty, "1502"),
                            new AndCondition(
                                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                                new OrCondition(
                                    new PropertyCondition(AutomationElement.NameProperty, "显示隐藏的图标", PropertyConditionFlags.IgnoreCase),
                                    new PropertyCondition(AutomationElement.NameProperty, "Show hidden icons", PropertyConditionFlags.IgnoreCase),
                                    new PropertyCondition(AutomationElement.NameProperty, "Notification Chevron", PropertyConditionFlags.IgnoreCase)))));
                        if (overflow == null || overflow.Current.IsOffscreen) continue;
                        if (overflow.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) ((InvokePattern)invoke).Invoke();
                        else ApplicationUi.ClickPointerElement(handle, overflow);
                        lastExpand = discovery.Elapsed;
                        break;
                    }
                }
            }
            catch (ElementNotAvailableException) { } // Explorer can replace its provider during expansion.
            Thread.Sleep(100);
        }
        if (icon == null) {
            SaveDiscovery(name);
            throw new TimeoutException("The session tray icon did not appear in visible or overflow notification areas; see tray-discovery.txt: " + name);
        }
        ApplicationUi.OpenPointerContextMenu(icon!.Value.Window, icon.Value.Element);
        var wait = Stopwatch.StartNew();
        while (wait.Elapsed < TimeSpan.FromSeconds(5)) {
            foreach (AutomationElement window in AutomationElement.RootElement.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ProcessIdProperty, coreId))) {
                if (window.Current.IsOffscreen) continue;
                var menu = window.Current.AutomationId == "Tray.Menu" ? window : window.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "Tray.Menu"));
                if (menu == null || menu.Current.IsOffscreen) continue;
                nint handle = menu.Current.NativeWindowHandle;
                if (handle == 0) handle = window.Current.NativeWindowHandle;
                Check(handle != 0, "Tray menu has no native window for pixel capture");
                return new(menu, handle);
            }
            Thread.Sleep(100);
        }
        SaveDiscovery(name);
        throw new TimeoutException("The owned Core tray menu did not open; see tray-discovery.txt");
    }

    public static void Dismiss(OpenMenu menu) {
        Check(SetForegroundWindow(menu.Window) || GetForegroundWindow() == menu.Window, "Could not focus the owned tray menu");
        keybd_event(0x1B, 0, 0, 0);
        keybd_event(0x1B, 0, 2, 0);
        var wait = Stopwatch.StartNew();
        while (wait.Elapsed < TimeSpan.FromSeconds(3)) {
            try { if (menu.Element.Current.IsOffscreen) return; }
            catch (ElementNotAvailableException) { return; }
            Thread.Sleep(50);
        }
        throw new TimeoutException("Tray menu did not dismiss");
    }

    private static (AutomationElement Element, nint Window)? FindIcon(string name, nint[] windows) {
        foreach (nint handle in windows.Where(IsWindowVisible)) {
            // A visible tooltip repeats the icon name and can precede the real
            // icon in the tree. Only notification buttons are click targets.
            foreach (AutomationElement element in AutomationElement.FromHandle(handle).FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))) {
                if (!element.Current.IsEnabled || element.Current.IsOffscreen || element.Current.BoundingRectangle.IsEmpty) continue;
                string accessibleName = element.Current.Name;
                // Some Shell versions append a notification count to the tooltip.
                // Require the entire unique session label, never a generic app name.
                if (accessibleName?.Contains(name, StringComparison.Ordinal) == true) return (element, handle);
            }
        }
        return null;
    }

    private static nint[] TrayWindows() {
        var windows = new List<nint>();
        EnumWindows((window, _) => {
            string name = WindowClass(window);
            if (name is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "TopLevelWindowForOverflowXamlIsland" or "NotifyIconOverflowWindow")
                windows.Add(window);
            return true;
        }, 0);
        return windows.ToArray();
    }

    private static string WindowClass(nint window) {
        var name = new System.Text.StringBuilder(256);
        GetClassName(window, name, name.Capacity);
        return name.ToString();
    }

    private static bool IsOverflowWindow(nint window) => WindowClass(window) is "TopLevelWindowForOverflowXamlIsland" or "NotifyIconOverflowWindow";

    private static void SaveDiscovery(string expectedName) {
        string path = Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", "tray-discovery.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var log = new StreamWriter(path);
        log.WriteLine($"Expected: {expectedName}; OS: {Environment.OSVersion}");
        foreach (var handle in TrayWindows()) {
            log.WriteLine($"Window={handle:X} class={WindowClass(handle)} visible={IsWindowVisible(handle)}");
            try {
                foreach (AutomationElement element in AutomationElement.FromHandle(handle).FindAll(TreeScope.Descendants, Condition.TrueCondition))
                    log.WriteLine($"{element.Current.ControlType.ProgrammaticName} id={element.Current.AutomationId} class={element.Current.ClassName} name={element.Current.Name} offscreen={element.Current.IsOffscreen} bounds={element.Current.BoundingRectangle}");
            }
            catch (ElementNotAvailableException ex) { log.WriteLine(ex.Message); }
        }
    }

    private delegate bool EnumWindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, System.Text.StringBuilder name, int capacity);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? title);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, nuint extraInfo);
}
