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
            // English makes control matching deterministic; it is a shipped installer language.
            // No silent switches here: license, Next, Install, Finish and uninstall dialogs are real.
            string arguments = options.Uninstall ? $"/NORESTART /LOG=\"{log}\""
                : $"/LANG=english /NORESTART /NOAUTOLAUNCH /DIR=\"{options.InstallDir}\" /LOG=\"{log}\"";
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
                var windows = AutomationElement.RootElement.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
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
        var controls = window.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
            .Where(e => e.Current.IsEnabled && !e.Current.IsOffscreen).ToArray();
        string text = string.Join("\n", controls.Select(e => e.Current.Name));
        var buttons = controls.Where(e => e.Current.ControlType == ControlType.Button).ToArray();
        AutomationElement? target = null;
        string? action = null;
        if (uninstall) {
            if (text.Contains("delete data folder", StringComparison.OrdinalIgnoreCase)) {
                target = FindDialogButton(buttons, "no", "7"); action = "keep-user-data";
            } else if (text.Contains("completely remove", StringComparison.OrdinalIgnoreCase)) {
                target = FindDialogButton(buttons, "yes", "6"); action = "confirm-uninstall";
            } else if (text.Contains("successfully removed", StringComparison.OrdinalIgnoreCase)) {
                target = FindDialogButton(buttons, "ok", "1"); action = "acknowledge-uninstall";
            }
        } else {
            if (text.Contains("this will install", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("continue", StringComparison.OrdinalIgnoreCase)) {
                target = FindDialogButton(buttons, "yes", "6"); action = "confirm-install";
            }
            var accept = controls.FirstOrDefault(e => e.Current.ControlType == ControlType.RadioButton &&
                Normalize(e.Current.Name) == "i accept the agreement");
            if (target is null && accept != null && !result.Actions.Contains("accept-license")) {
                target = accept; action = "accept-license";
            } else if (target is null) {
                foreach (var (label, step) in new[] { ("finish", "finish"), ("install", "install"), ("next", "next") }) {
                    target = Find(buttons, label);
                    if (target != null) { action = step; break; }
                }
            }
        }
        if (target is null || action is null) return null;
        // Several pages use Next; throttle it, then re-read live controls. Never use global keystrokes.
        if (action == previous && sinceAction < TimeSpan.FromSeconds(1)) return null;
        string title = window.Current.Name;
        if (target.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var item)) ((SelectionItemPattern)item).Select();
        else if (target.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) ((InvokePattern)invoke).Invoke();
        else if (target.Current.NativeWindowHandle != 0) {
            // Some VCL controls expose a native HWND without an Invoke pattern.
            // BM_CLICK targets only this verified button; no coordinates/global input are used.
            if (SendMessageTimeout(new IntPtr(target.Current.NativeWindowHandle), 0x00F5, IntPtr.Zero,
                IntPtr.Zero, 2, 1000, out _) == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Wizard button did not respond.");
        }
        else throw new InvalidOperationException($"Wizard control cannot be operated: {target.Current.Name}");
        result.Actions.Add(action);
        Console.WriteLine($"Wizard: {title}: {action}");
        return action;
    }

    private static AutomationElement? Find(AutomationElement[] controls, string name) =>
        controls.FirstOrDefault(e => Normalize(e.Current.Name) == name);

    // Standard message-box labels follow the OS language, even with an English setup.
    private static AutomationElement? FindDialogButton(AutomationElement[] controls, string name, string id) =>
        Find(controls, name) ?? controls.FirstOrDefault(e => e.Current.AutomationId == id);

    private static string Normalize(string text) => text.Replace("&", "").Trim().Trim('<', '>').Trim().ToLowerInvariant();

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
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SendMessageTimeout(
        IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
}
