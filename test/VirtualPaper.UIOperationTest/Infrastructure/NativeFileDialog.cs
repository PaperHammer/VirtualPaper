using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class NativeFileDialog {
    public static void OpenFile(Process ui, nint ownerWindow, string path) => ChooseFile(ui, ownerWindow, path);
    public static void OpenFiles(Process ui, nint ownerWindow, IReadOnlyList<string> paths, Action<string>? trace = null) {
        Check(paths.Count > 1, "Multi-file acceptance requires more than one input.");
        string directory = Path.GetDirectoryName(Path.GetFullPath(paths[0]))!;
        Check(paths.All(path => string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), directory, StringComparison.OrdinalIgnoreCase)),
            "Native multi-file inputs must share a directory.");
        ChooseFile(ui, ownerWindow, "", directory, trace, paths.Select(Path.GetFileName).ToArray()!);
    }
    public static void SaveFile(Process ui, nint ownerWindow, string path) => ChooseFile(ui, ownerWindow, path);
    public static void ChooseFolder(Process ui, nint ownerWindow, string path) => ChooseFile(ui, ownerWindow, path);
    public static void Cancel(Process ui, nint ownerWindow) => ChooseFile(ui, ownerWindow, null);

    private static void ChooseFile(Process ui, nint ownerWindow, string? path, string? directory = null,
        Action<string>? trace = null, IReadOnlyList<string>? files = null) {
        string log = Path.Combine(VirtualPaper.Common.Constants.CommonPaths.LogDir, "UIOperation", "native-picker.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        void Record(string message) {
            File.AppendAllText(log, $"{DateTime.UtcNow:O} owner={ownerWindow} {message}\n");
            trace?.Invoke(message);
        }
        Record($"Requested {(path == null ? "cancel" : "selection")}: {directory ?? path}");
        try { ChooseFileCore(ui, ownerWindow, path, directory, Record, files); }
        catch (Exception ex) {
            Record(ex.ToString());
            throw;
        }
    }

    private static void ChooseFileCore(Process ui, nint ownerWindow, string? path, string? directory,
        Action<string> trace, IReadOnlyList<string>? files) {
        var timer = Stopwatch.StartNew();
        AutomationElement? dialog = null;
        nint pickerWindow = 0;
        var candidates = new HashSet<string>();
        while (dialog == null && timer.Elapsed < TimeSpan.FromSeconds(10)) {
            try {
                var ownerRoot = AutomationElement.FromHandle(ownerWindow);
                if ((pickerWindow = FindPickerWindow(ownerRoot, ownerWindow)) != 0) {
                    dialog = AutomationElement.FromHandle(pickerWindow);
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
                        if ((pickerWindow = FindPickerWindow(window, ownerWindow)) != 0) {
                            dialog = AutomationElement.FromHandle(pickerWindow);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex) when (IsTransientProviderError(ex)) {
                // Native picker and XAML popup hosts can be replaced during opening.
            }
            if (dialog == null) Thread.Sleep(100);
        }
        if (dialog == null) {
            var root = AutomationElement.FromHandle(ownerWindow);
            var controls = root.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
                .Where(item => item.Current.ControlType == ControlType.Window || item.Current.ControlType == ControlType.Button
                    || item.Current.ControlType == ControlType.ComboBox || (item.Current.ControlType == ControlType.Edit
                        && !item.Current.AutomationId.StartsWith("System.", StringComparison.Ordinal)))
                .Take(120).Select(item => $"{item.Current.ControlType.ProgrammaticName}: {item.Current.AutomationId} {item.Current.Name} class={item.Current.ClassName}");
            throw new InvalidOperationException($"No native file dialog owned by the test UI appeared. Owned windows: {string.Join("; ", candidates)}\n{string.Join("\n", controls)}");
        }
        var filename = WaitForFilename(pickerWindow);
        trace($"Picker hwnd={pickerWindow}, class={dialog.Current.ClassName}");
        trace($"Filename: class={filename.Current.ClassName}, id={filename.Current.AutomationId}, directoryNavigation={directory != null}");
        if (directory != null) {
            // Navigate once, then select real Shell rows. Repeating long absolute
            // paths can exceed the native filename edit's text limit.
            SetFilename(pickerWindow, directory);
            trace("Opening the multi-file input directory.");
            InvokeAction(pickerWindow, 1);
            UiOperationWait.Until(() => FindFilename(AutomationElement.FromHandle(pickerWindow)),
                edit => edit != null && edit.TryGetCurrentPattern(ValuePattern.Pattern, out var value)
                    && ((ValuePattern)value).Current.Value.Length == 0,
                "Navigate to the multi-file input folder", IsTransientProviderError, TimeSpan.FromSeconds(5));
            trace("Input directory navigation completed.");
        }
        if (files != null) {
            var selected = UiOperationWait.Until(() => {
                var rows = AutomationElement.FromHandle(pickerWindow).FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().ToArray();
                return files.Select(file => rows.SingleOrDefault(row => row.Current.Name == file ||
                    row.Current.Name == Path.GetFileNameWithoutExtension(file))).OfType<AutomationElement>().ToArray();
            }, rows => rows.Length == files.Count, "Show all multi-file input rows", IsTransientProviderError, TimeSpan.FromSeconds(5));
            for (int i = 0; i < selected.Length; i++) {
                var selection = (SelectionItemPattern)selected[i].GetCurrentPattern(SelectionItemPattern.Pattern);
                if (i == 0) selection.Select(); else selection.AddToSelection();
            }
            Check(selected.All(row => ((SelectionItemPattern)row.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected),
                "Native picker did not retain every selected file row.");
            trace($"Selected {selected.Length} actual Shell file rows.");
        }
        else if (path != null) {
            SetFilename(pickerWindow, path);
            trace($"Filename selection retained ({path.Length} characters).");
        }
        int actionId = path == null ? 2 : 1;
        InvokeAction(pickerWindow, actionId, () => {
            trace($"Action {actionId} submitted; waiting for the native picker to close.");
            // Keep the pointer over the submitted control until Shell consumes
            // the real click. A missing/replaced UIA provider is not success.
            UiOperationWait.Until(() => !IsWindow(pickerWindow) || !IsWindowVisible(pickerWindow), closed => closed,
                $"Native picker action {actionId} (hwnd={pickerWindow})", timeout: TimeSpan.FromSeconds(5));
        });
        trace($"Picker action {actionId} completed.");
    }

    private static void InvokeAction(nint pickerWindow, int actionId, Action? afterInput = null) {
        // Shell list items also use ID 1. Require a button type or the native
        // Button class; some Windows picker versions expose Open as a Pane
        // with no UIA patterns. Keep submission local to this owned control.
        var condition = new AndCondition(
            new PropertyCondition(AutomationElement.AutomationIdProperty, actionId.ToString()),
            new OrCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.SplitButton),
                new PropertyCondition(AutomationElement.ClassNameProperty, "Button")));
        var open = UiOperationWait.Until(
            () => AutomationElement.FromHandle(pickerWindow).FindFirst(TreeScope.Descendants, condition),
            control => control is { Current.IsEnabled: true, Current.IsOffscreen: false },
            $"Ready native picker action {actionId}", IsTransientProviderError)!;
        nint button = open.Current.NativeWindowHandle;
        if (button != 0 && open.Current.ClassName == "Button" && GetDlgCtrlID(button) == actionId) {
            // A real click transfers focus out of the filename edit and lets
            // Shell commit its value. It also avoids synchronous UIA/COM calls
            // inside the native Save handler on Windows Server.
            ApplicationUi.ClickPointerElement(pickerWindow, open, afterInput);
        }
        else {
            ((InvokePattern)open.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            afterInput?.Invoke();
        }
    }

    private static AutomationElement WaitForFilename(nint pickerWindow) => UiOperationWait.Until(
        () => FindFilename(AutomationElement.FromHandle(pickerWindow)),
        filename => filename is { Current.IsEnabled: true, Current.IsOffscreen: false }
            && filename.TryGetCurrentPattern(ValuePattern.Pattern, out _),
        $"Ready filename control in picker {pickerWindow}", IsTransientProviderError)!;

    private static void SetFilename(nint pickerWindow, string value) => UiOperationWait.Until(() => {
        var edit = FindFilename(AutomationElement.FromHandle(pickerWindow));
        if (edit == null || !edit.Current.IsEnabled || edit.Current.IsOffscreen
            || !edit.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)) return false;
        edit.SetFocus();
        ((ValuePattern)pattern).SetValue(value);
        return ((ValuePattern)pattern).Current.Value == value;
    }, retained => retained, "Retain the picker filename", IsTransientProviderError);

    private static nint FindPickerWindow(AutomationElement root, nint ownerWindow) {
        // Resolve the native root during discovery, inside its retry boundary.
        // A brokered picker may move out of the owner's UIA subtree afterward.
        var ancestor = FindFilename(root);
        while (ancestor != null) {
            nint handle = ancestor.Current.NativeWindowHandle;
            if (handle != 0) {
                nint window = GetAncestor(handle, 2);
                return window != ownerWindow && IsWindowVisible(window) ? window : 0;
            }
            ancestor = TreeWalker.RawViewWalker.GetParent(ancestor);
        }
        return 0;
    }

    private static bool IsTransientProviderError(Exception ex) => ex is ElementNotAvailableException
        || ex is COMException && (ex.HResult == unchecked((int)0x80010012)
            || ex.HResult == unchecked((int)0x80010108) || ex.HResult == unchecked((int)0x80040201));

    private static AutomationElement? FindFilename(AutomationElement root) {
        var host = root.FindFirst(TreeScope.Descendants, new OrCondition(
            new PropertyCondition(AutomationElement.AutomationIdProperty, "FileNameControlHost"),
            new PropertyCondition(AutomationElement.AutomationIdProperty, "FolderNameControlHost")));
        return host?.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit))
            ?? root.FindFirst(TreeScope.Descendants, new AndCondition(
                new OrCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1148"),
                    // The native folder picker uses the standard edt1 control, not FileNameControlHost.
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "1152")),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)));
    }

    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern int GetDlgCtrlID(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
}
