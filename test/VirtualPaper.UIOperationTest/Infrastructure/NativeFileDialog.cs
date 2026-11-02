using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class NativeFileDialog {
    public static void OpenFile(Process ui, nint ownerWindow, string path) {
        var timer = Stopwatch.StartNew();
        AutomationElement? dialog = null;
        var candidates = new HashSet<string>();
        while (dialog == null && timer.Elapsed < TimeSpan.FromSeconds(10)) {
            try {
                var ownerRoot = AutomationElement.FromHandle(ownerWindow);
                if (ownerRoot.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "1148")) != null) {
                    dialog = ownerRoot;
                    break;
                }
                foreach (AutomationElement window in AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition)) {
                    nint hwnd = window.Current.NativeWindowHandle;
                    if (hwnd == 0) continue;
                    bool owned = window.Current.ProcessId == ui.Id;
                    for (nint owner = GetWindow(hwnd, 4); !owned && owner != 0; owner = GetWindow(owner, 4))
                        owned = owner == ownerWindow;
                    if (owned) {
                        candidates.Add($"{window.Current.ClassName} {window.Current.Name}");
                        if (window.FindFirst(TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.AutomationIdProperty, "1148")) != null) { dialog = window; break; }
                    }
                }
            }
            catch (ElementNotAvailableException) {
                // Native picker and XAML popup hosts can be replaced during opening.
            }
            if (dialog == null) Thread.Sleep(100);
        }
        if (dialog == null) {
            var root = AutomationElement.FromHandle(ownerWindow);
            var controls = root.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>().Take(100)
                .Select(item => $"{item.Current.ControlType.ProgrammaticName}: {item.Current.AutomationId} {item.Current.Name}");
            throw new InvalidOperationException($"No native file dialog owned by the test UI appeared. Owned windows: {string.Join("; ", candidates)}\n{string.Join("\n", controls)}");
        }
        var filename = dialog.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.AutomationIdProperty, "1148"),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)));
        Check(filename != null, "Native file dialog has no filename edit control");
        filename!.SetFocus();
        ApplicationUi.SetText(filename, path);
        Check(((ValuePattern)filename.GetCurrentPattern(ValuePattern.Pattern)).Current.Value == path,
            "Native file dialog did not retain the test filename");
        // Submit from the filename field. Shell list items reuse numeric IDs,
        // and the picker broker's action button varies between Windows versions.
        nint inputWindow = GetAncestor(filename.Current.NativeWindowHandle, 2);
        ApplicationUi.PressEnter(inputWindow == 0 ? ownerWindow : inputWindow, filename);
        var submitted = Stopwatch.StartNew();
        while (submitted.Elapsed < TimeSpan.FromSeconds(5)) {
            try {
                if (dialog.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "1148")) == null) return;
            }
            catch (ElementNotAvailableException) { return; }
            Thread.Sleep(100);
        }
        var remaining = dialog.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
            .Where(item => item.Current.AutomationId == "1148" || item.Current.ControlType == ControlType.Button)
            .Select(item => $"{item.Current.ControlType.ProgrammaticName} {item.Current.AutomationId} {item.Current.Name}" +
                (item.TryGetCurrentPattern(ValuePattern.Pattern, out var value) ? $" = {((ValuePattern)value).Current.Value}" : ""));
        throw new InvalidOperationException($"File dialog did not accept the test image:\n{string.Join("\n", remaining)}");
    }

    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
}
