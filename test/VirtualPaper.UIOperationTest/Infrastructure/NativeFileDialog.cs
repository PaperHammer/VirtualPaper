using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class NativeFileDialog {
    public static void OpenFile(Process ui, nint ownerWindow, string path) => ChooseFile(ui, ownerWindow, path);
    public static void SaveFile(Process ui, nint ownerWindow, string path) => ChooseFile(ui, ownerWindow, path);
    public static void Cancel(Process ui, nint ownerWindow) => ChooseFile(ui, ownerWindow, null);

    private static void ChooseFile(Process ui, nint ownerWindow, string? path) {
        var timer = Stopwatch.StartNew();
        AutomationElement? dialog = null;
        var candidates = new HashSet<string>();
        while (dialog == null && timer.Elapsed < TimeSpan.FromSeconds(10)) {
            try {
                var ownerRoot = AutomationElement.FromHandle(ownerWindow);
                if (FindFilename(ownerRoot) != null) {
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
                        if (FindFilename(window) != null) { dialog = window; break; }
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
        var filename = FindFilename(dialog)
            ?? throw new InvalidOperationException("Native file dialog has no filename edit control");
        if (path != null) {
            ApplicationUi.SetText(filename, path);
            Check(((ValuePattern)filename.GetCurrentPattern(ValuePattern.Pattern)).Current.Value == path,
                "Native file dialog did not retain the test filename");
        }
        int actionId = path == null ? 2 : 1;
        // Shell list items also use ID 1. Require a button type or the native
        // Button class; some Windows picker versions expose Open as a Pane
        // with no UIA patterns. Keep submission local to this owned control.
        var open = dialog.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.AutomationIdProperty, actionId.ToString()),
            new OrCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.SplitButton),
                new PropertyCondition(AutomationElement.ClassNameProperty, "Button"))));
        if (open == null) {
            var buttons = dialog.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
                .Where(item => item.Current.ControlType == ControlType.Button || item.Current.ControlType == ControlType.SplitButton
                    || item.Current.AutomationId == actionId.ToString())
                .Select(item => $"{item.Current.ControlType.ProgrammaticName} {item.Current.AutomationId}: {item.Current.Name} " +
                    $"class={item.Current.ClassName} hwnd={item.Current.NativeWindowHandle} patterns=" +
                    string.Join(",", item.GetSupportedPatterns().Select(pattern => pattern.ProgrammaticName)));
            throw new InvalidOperationException($"Native file dialog has no action {actionId}: " + string.Join("; ", buttons));
        }
        nint button = open.Current.NativeWindowHandle;
        nint pickerWindow = button == 0 ? 0 : GetAncestor(button, 2);
        if (button != 0 && open.Current.ClassName == "Button" && GetDlgCtrlID(button) == actionId) {
            // SaveFilePicker needs outgoing COM calls while validating the path.
            // UIA Invoke can run in an input-synchronous SendMessage context
            // and cause RPC_E_CANTCALLOUT_ININPUTSYNCCALL. Queue this button's
            // native click instead, then require the picker to finish below.
            SetForegroundWindow(GetAncestor(button, 2));
            Check(PostMessage(button, 0x00F5, 0, 0), "Native file dialog rejected BM_CLICK on its action button");
        }
        else {
            ((InvokePattern)open.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        }
        var submitted = Stopwatch.StartNew();
        while (submitted.Elapsed < TimeSpan.FromSeconds(5)) {
            if (pickerWindow != 0 && !IsWindow(pickerWindow)) return;
            try {
                if (FindFilename(dialog) == null) return;
            }
            catch (ElementNotAvailableException) { return; }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x80010012) && pickerWindow != 0) {
                // The picker broker can exit while UIA traverses its retiring
                // provider. Still require its actual native window to close.
            }
            Thread.Sleep(100);
        }
        var remaining = dialog.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
            .Where(item => item.Current.ControlType == ControlType.Edit || item.Current.ControlType == ControlType.Button
                || item.Current.ControlType == ControlType.Text)
            .Select(item => $"{item.Current.ControlType.ProgrammaticName} {item.Current.AutomationId} {item.Current.Name}" +
                (item.TryGetCurrentPattern(ValuePattern.Pattern, out var value) ? $" = {((ValuePattern)value).Current.Value}" : ""));
        throw new InvalidOperationException($"File dialog did not finish action {actionId}:\n{string.Join("\n", remaining)}");
    }

    private static AutomationElement? FindFilename(AutomationElement root) {
        var host = root.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "FileNameControlHost"));
        return host?.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit))
            ?? root.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.AutomationIdProperty, "1148"),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)));
    }

    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern int GetDlgCtrlID(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
