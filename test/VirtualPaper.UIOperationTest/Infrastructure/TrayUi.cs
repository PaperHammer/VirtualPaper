using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using VirtualPaper.Common;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class TrayUi {
    internal sealed record OpenMenu(AutomationElement Element, nint Window);

    public static OpenMenu Open(int coreId) {
        string name = $"Virtual Paper ({TestProcessEnvironment.SessionId})";
        nint taskbar = FindWindow("Shell_TrayWnd", null);
        Check(taskbar != 0, "Explorer taskbar is not available");
        var shell = AutomationElement.FromHandle(taskbar);
        (AutomationElement Element, nint Window)? icon = FindIcon(name, taskbar);
        if (icon == null) {
            var overflow = shell.FindFirst(TreeScope.Descendants, new OrCondition(
                new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTray.OverflowButton"),
                new PropertyCondition(AutomationElement.AutomationIdProperty, "1502"),
                new AndCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTrayIcon"),
                    new OrCondition(
                        new PropertyCondition(AutomationElement.NameProperty, "显示隐藏的图标"),
                        new PropertyCondition(AutomationElement.NameProperty, "Show hidden icons")))));
            if (overflow == null || overflow.Current.IsOffscreen) {
                string path = Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", "tray-discovery.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllLines(path, shell.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
                    .Select(e => $"{e.Current.ControlType.ProgrammaticName} id={e.Current.AutomationId} class={e.Current.ClassName} name={e.Current.Name} offscreen={e.Current.IsOffscreen} bounds={e.Current.BoundingRectangle}"));
                throw new InvalidOperationException("Notification overflow button is unavailable; see tray-discovery.txt");
            }
            ApplicationUi.Invoke(overflow!);
            var timer = Stopwatch.StartNew();
            while (icon == null && timer.Elapsed < TimeSpan.FromSeconds(5)) {
                icon = FindIcon(name, taskbar);
                if (icon == null) Thread.Sleep(100);
            }
        }
        Check(icon != null, "Could not find this test session's tray icon: " + name);
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
        throw new TimeoutException("The owned Core tray menu did not open");
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

    private static (AutomationElement Element, nint Window)? FindIcon(string name, nint taskbar) {
        foreach (nint handle in new[] { taskbar, FindWindow("TopLevelWindowForOverflowXamlIsland", null),
            FindWindow("NotifyIconOverflowWindow", null) }.Where(h => h != 0).Distinct()) {
            var icon = AutomationElement.FromHandle(handle).FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, name)).Cast<AutomationElement>()
                .FirstOrDefault(e => !e.Current.IsOffscreen && !e.Current.BoundingRectangle.IsEmpty);
            if (icon != null) return (icon, handle);
        }
        return null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? title);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, nuint extraInfo);
}
