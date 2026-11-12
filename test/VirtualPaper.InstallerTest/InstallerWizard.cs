using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace VirtualPaper.InstallerTest;

internal sealed class WizardResult {
    public bool Passed { get; set; }
    public List<string> Actions { get; } = [];
    public string? Error { get; set; }
}

/// <summary>Operate only windows belonging to this setup process and its descendants.</summary>
internal static class InstallerWizard {
    public static WizardResult Run(InstallerOptions options) {
        var result = new WizardResult();
        var processes = new Dictionary<int, Process>();
        var timer = Stopwatch.StartNew();
        var seenPids = new HashSet<int>();
        string? lastAction = null;
        TimeSpan lastActionAt = TimeSpan.Zero;
        TimeSpan? exitedAt = null;
        try {
            string log = Path.Combine(Path.GetDirectoryName(options.ResultsFile!)!, options.Uninstall ? "uninstall.log" : "install.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            // Every selected language must complete the same required wizard interactions.
            // No silent switches here: license, Next, Install, Finish and uninstall dialogs are real.
            string arguments = options.Uninstall ? $"/NORESTART /LOG=\"{log}\""
                : $"/LANG={options.Language} /NORESTART /NOAUTOLAUNCH /DIR=\"{options.InstallDir}\" /LOG=\"{log}\"";
            var process = Process.Start(new ProcessStartInfo(options.WizardExecutable!, arguments) {
                UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(options.WizardExecutable!)!,
                WindowStyle = ProcessWindowStyle.Normal
            }) ?? throw new InvalidOperationException("Installer did not start.");
            seenPids.Add(process.Id);
            processes.Add(process.Id, process);
            while (timer.Elapsed < options.Timeout) {
                // Inno's setup loader/uninstaller can hand off to a temporary child executable.
                foreach (var id in Descendants(seenPids)) {
                    seenPids.Add(id);
                    if (processes.ContainsKey(id)) continue;
                    try { processes.Add(id, ProcessAttachment.RetainHandle(Process.GetProcessById(id))); }
                    catch (ArgumentException) { }
                    catch (InvalidOperationException) { } // A short-lived child can exit before attachment.
                }
                var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
                foreach (AutomationElement window in windows) {
                    try {
                        if (!seenPids.Contains(window.Current.ProcessId) || window.Current.IsOffscreen) continue;
                        string? action = Advance(window, options.Uninstall, result, lastAction, timer.Elapsed - lastActionAt);
                        if (action != null) { lastAction = action; lastActionAt = timer.Elapsed; }
                    } catch (ElementNotAvailableException) { } // Expected during page/window transitions.
                }
                if (processes.Values.All(p => p.HasExited)) {
                    exitedAt ??= timer.Elapsed;
                    if (timer.Elapsed - exitedAt.Value >= TimeSpan.FromSeconds(1)) break;
                } else exitedAt = null;
                Thread.Sleep(200);
            }
            if (processes.Values.Any(p => !p.HasExited)) throw new TimeoutException("Installer wizard did not finish before the deadline.");
            foreach (var child in processes.Values) {
                if (child.ExitCode != 0) throw new InvalidOperationException($"Installer process {child.Id} exited with {child.ExitCode}.");
            }
            string[] required = options.Uninstall ? ["confirm-uninstall", "acknowledge-uninstall"] : ["accept-license", "install", "finish"];
            if (required.Any(action => !result.Actions.Contains(action)))
                throw new InvalidOperationException($"Wizard exited without required interactions: {string.Join(", ", required.Except(result.Actions))}");
            result.Passed = true;
        } catch (Exception exception) {
            result.Error = exception.ToString();
            var windows = new List<string>();
            EnumWindows((window, _) => {
                GetWindowThreadProcessId(window, out int pid);
                if (!seenPids.Contains(pid)) return true;
                void Describe(nint handle) {
                    var label = new System.Text.StringBuilder(1024);
                    var type = new System.Text.StringBuilder(256);
                    GetWindowText(handle, label, label.Capacity);
                    GetClassName(handle, type, type.Capacity);
                    windows.Add($"PID={pid} HWND={handle:X} class={type} id={GetDlgCtrlID(handle)} visible={IsWindowVisible(handle)} enabled={IsWindowEnabled(handle)} text={label}");
                }
                Describe(window);
                EnumChildWindows(window, (child, _) => { Describe(child); return true; }, 0);
                return true;
            }, 0);
            File.WriteAllLines(options.ResultsFile + ".windows.txt", windows);
            Console.Error.WriteLine(exception);
        } finally {
            foreach (var process in processes.Values) {
                try {
                    if (!process.HasExited) { process.Kill(true); process.WaitForExit(5000); }
                } catch (Exception exception) {
                    result.Passed = false;
                    result.Error += $"\nWizard recovery failed: {exception.Message}";
                } finally { process.Dispose(); }
            }
        }
        return result;
    }

    private static string? Advance(AutomationElement window, bool uninstall, WizardResult result, string? previous, TimeSpan sinceAction) {
        if (uninstall && AdvanceNativeUninstallDialog(window, previous, sinceAction) is { } nativeAction) {
            result.Actions.Add(nativeAction);
            Console.WriteLine($"Wizard: {window.Current.Name}: {nativeAction}");
            return nativeAction;
        }
        var controls = window.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
            .Where(e => !e.Current.IsOffscreen).ToArray();
        string text = string.Join("\n", controls.Select(e => e.Current.Name));
        var buttons = controls.Where(e => e.Current.ControlType == ControlType.Button && e.Current.IsEnabled).ToArray();
        AutomationElement? target = null;
        string? action = null;
        if (uninstall) {
            if (InstallerWizardText.DialogAction(text) == "keep-user-data") {
                target = FindDialogButton(buttons, "no", "7"); action = "keep-user-data";
            } else if (InstallerWizardText.DialogAction(text) == "confirm-uninstall") {
                target = FindDialogButton(buttons, "yes", "6"); action = "confirm-uninstall";
            } else if (InstallerWizardText.DialogAction(text) == "acknowledge-uninstall") {
                target = FindDialogButton(buttons, "ok", "1"); action = "acknowledge-uninstall";
            }
        } else {
            if (text.Contains("this will install", StringComparison.OrdinalIgnoreCase) && text.Contains("continue", StringComparison.OrdinalIgnoreCase)
                || text.Contains("现在将安装", StringComparison.Ordinal) && text.Contains("继续", StringComparison.Ordinal)
                || text.Contains("Cet assistant va installer", StringComparison.OrdinalIgnoreCase) && text.Contains("continuer", StringComparison.OrdinalIgnoreCase)) {
                target = FindDialogButton(buttons, "yes", "6"); action = "confirm-install";
            }
            var accept = controls.FirstOrDefault(e => e.Current.IsEnabled && e.Current.ControlType == ControlType.RadioButton &&
                InstallerWizardText.IsLicenseAcceptance(e.Current.Name));
            if (target is null && accept != null && !result.Actions.Contains("accept-license")) {
                target = accept; action = "accept-license";
            } else if (target is null) {
                foreach (string step in new[] { "finish", "install", "next" }) {
                    target = buttons.FirstOrDefault(button => InstallerWizardText.InstallAction(button.Current.Name) == step);
                    if (target != null) { action = step; break; }
                }
            }
        }
        if (target is null || action is null) return null;
        // Several pages use Next; throttle it, then re-read live controls. Never use global keystrokes.
        if (action == previous && sinceAction < TimeSpan.FromSeconds(1)) return null;
        string title = window.Current.Name;
        if (target.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var item)) ((SelectionItemPattern)item).Select();
        else if (target.Current.NativeWindowHandle != 0) {
            // Queue the verified native button's click. Synchronous UIA/SendMessage
            // can remain inside a modal handler while Inno opens its next dialog.
            // Exit codes and required interactions are still checked afterwards.
            if (!PostMessage(new IntPtr(target.Current.NativeWindowHandle), 0x00F5, IntPtr.Zero, IntPtr.Zero))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Wizard button did not respond.");
        }
        else if (target.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) ((InvokePattern)invoke).Invoke();
        else throw new InvalidOperationException($"Wizard control cannot be operated: {target.Current.Name}");
        result.Actions.Add(action);
        Console.WriteLine($"Wizard: {title}: {action}");
        return action;
    }

    private static string? AdvanceNativeUninstallDialog(AutomationElement window, string? previous, TimeSpan sinceAction) {
        nint handle = window.Current.NativeWindowHandle;
        var className = new System.Text.StringBuilder(256);
        GetClassName(handle, className, className.Capacity);
        if (className.ToString() != "#32770") return null;
        // Standard message-box text/buttons are not consistently exposed by
        // UIA on desktop Windows and Server. The caller already verified PID ownership.
        var labels = new List<string>();
        var buttons = new List<nint>();
        EnumChildWindows(handle, (child, _) => {
            var label = new System.Text.StringBuilder(1024);
            GetWindowText(child, label, label.Capacity);
            labels.Add(label.ToString());
            var type = new System.Text.StringBuilder(256);
            GetClassName(child, type, type.Capacity);
            if (type.ToString() == "Button" && IsWindowVisible(child) && IsWindowEnabled(child)) buttons.Add(child);
            return true;
        }, 0);
        string text = string.Join("\n", labels);
        string? action = InstallerWizardText.DialogAction(text);
        int id = action switch { "keep-user-data" => 7, "confirm-uninstall" => 6, "acknowledge-uninstall" => 1, _ => 0 };
        if (action == null || action == previous && sinceAction < TimeSpan.FromSeconds(1)) return null;
        // The single OK button can use IDCANCEL (2), including on Chinese Windows.
        nint button = action == "acknowledge-uninstall" ? (buttons.Count == 1 ? buttons[0] : 0) : GetDlgItem(handle, id);
        if (button == 0 || !IsWindowEnabled(button)) return null;
        if (!PostMessage(button, 0x00F5, 0, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return action;
    }

    private static AutomationElement? Find(AutomationElement[] controls, string name) =>
        controls.FirstOrDefault(e => Normalize(e.Current.Name) == name);

    // Standard message-box labels follow the OS language, even with an English setup.
    private static AutomationElement? FindDialogButton(AutomationElement[] controls, string name, string id) =>
        Find(controls, name) ?? controls.FirstOrDefault(e => e.Current.AutomationId == id);

    private static string Normalize(string text) => InstallerWizardText.Normalize(text);

    private static IEnumerable<int> Descendants(HashSet<int> roots) {
        IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        var entries = new List<(int Id, int Parent)>();
        try {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            if (!Process32First(snapshot, ref entry)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            do { entries.Add(((int)entry.Id, (int)entry.Parent)); } while (Process32Next(snapshot, ref entry));
        } finally { CloseHandle(snapshot); }
        var family = new HashSet<int>(roots);
        bool added;
        do {
            added = false;
            foreach (var entry in entries) {
                if (family.Contains(entry.Parent) && family.Add(entry.Id)) added = true;
            }
        } while (added);
        return family;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry {
        public uint Size, Usage, Id;
        public UIntPtr DefaultHeapId;
        public uint ModuleId, Threads, Parent;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Executable;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(
        IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    private delegate bool EnumWindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out int processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern int GetDlgCtrlID(nint window);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint window, EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, System.Text.StringBuilder text, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, System.Text.StringBuilder text, int capacity);
    [DllImport("user32.dll")] private static extern nint GetDlgItem(nint window, int id);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint window);
}
