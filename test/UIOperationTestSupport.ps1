function Resolve-UIOperationEnvironment($State) {
    $code = $null
    $reason = $null
    if (-not $State.Windows) {
        $code = 'WindowsRequired'; $reason = 'Real WinUI acceptance requires Windows.'
    } elseif (-not $State.UserInteractive -or $State.SessionId -eq 0 -or
        -not $State.SessionActive -or -not $State.WindowStationVisible -or
        -not $State.InputDesktopAccessible -or $State.DesktopName -ne 'Default' -or
        $State.InputDesktopName -ne $State.DesktopName) {
        $code = 'NoInteractiveDesktop'; $reason = 'An active, unlocked Windows input desktop is unavailable.'
    } elseif ($State.MonitorCount -lt 1 -or $State.DisplayWidth -lt 1 -or $State.DisplayHeight -lt 1) {
        $code = 'NoDisplay'; $reason = 'No physical or virtual display area is available.'
    } elseif (-not $State.DesktopShellAvailable) {
        $code = 'NoDesktopShell'; $reason = 'Explorer desktop required by wallpaper acceptance is unavailable.'
    }
    [pscustomobject]@{ Available = $null -eq $code; ReasonCode = $code; Reason = $reason; Checks = $State }
}

function Get-UIOperationEnvironment {
    if (-not $IsWindows) { return Resolve-UIOperationEnvironment ([pscustomobject]@{ Windows = $false }) }
    if (-not ('VirtualPaper.TestDesktopProbe' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace VirtualPaper {
    public sealed class TestDesktopState {
        public bool Windows = true, UserInteractive, SessionActive, WindowStationVisible,
            InputDesktopAccessible, DesktopShellAvailable;
        public int SessionId, MonitorCount, DisplayWidth, DisplayHeight, InputDesktopError;
        public string DesktopName = "", InputDesktopName = "";
    }
    public static class TestDesktopProbe {
        public static TestDesktopState Read() {
            var state = new TestDesktopState { UserInteractive = Environment.UserInteractive };
            using (var process = Process.GetCurrentProcess()) state.SessionId = process.SessionId;
            if (!state.UserInteractive || state.SessionId == 0) return state;
            IntPtr session;
            int length;
            if (!WTSQuerySessionInformationW(IntPtr.Zero, state.SessionId, 8, out session, out length))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try { state.SessionActive = length >= 4 && Marshal.ReadInt32(session) == 0; }
            finally { WTSFreeMemory(session); }
            if (!state.SessionActive) return state;
            UserObjectFlags flags;
            if (!GetFlags(GetProcessWindowStation(), 1, out flags, Marshal.SizeOf(typeof(UserObjectFlags)), out length))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            state.WindowStationVisible = (flags.Flags & 1) != 0;
            if (!state.WindowStationVisible) return state;
            state.DesktopName = Name(GetThreadDesktop(GetCurrentThreadId()));
            // Read only: never switch desktops, unlock a session or change system settings.
            IntPtr desktop = OpenInputDesktop(0, false, 1);
            if (desktop == IntPtr.Zero) { state.InputDesktopError = Marshal.GetLastWin32Error(); return state; }
            try {
                state.InputDesktopAccessible = true;
                state.InputDesktopName = Name(desktop);
            } finally { CloseDesktop(desktop); }
            state.MonitorCount = GetSystemMetrics(80);
            state.DisplayWidth = GetSystemMetrics(78);
            state.DisplayHeight = GetSystemMetrics(79);
            state.DesktopShellAvailable = FindWindowW("Progman", null) != IntPtr.Zero || FindWindowW("WorkerW", null) != IntPtr.Zero;
            return state;
        }
        private static string Name(IntPtr handle) {
            var name = new StringBuilder(256);
            int length;
            if (!GetName(handle, 2, name, name.Capacity * 2, out length)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return name.ToString();
        }
        [StructLayout(LayoutKind.Sequential)] private struct UserObjectFlags { public int Inherit, Reserved; public uint Flags; }
        [DllImport("user32.dll")] private static extern IntPtr GetProcessWindowStation();
        [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint threadId);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", EntryPoint="GetUserObjectInformationW", SetLastError=true)] private static extern bool GetFlags(IntPtr handle, int index, out UserObjectFlags flags, int size, out int needed);
        [DllImport("user32.dll", EntryPoint="GetUserObjectInformationW", CharSet=CharSet.Unicode, SetLastError=true)] private static extern bool GetName(IntPtr handle, int index, StringBuilder name, int size, out int needed);
        [DllImport("user32.dll", SetLastError=true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
        [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr FindWindowW(string name, string title);
        [DllImport("wtsapi32.dll", SetLastError=true)] private static extern bool WTSQuerySessionInformationW(IntPtr server, int sessionId, int infoClass, out IntPtr buffer, out int bytes);
        [DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(IntPtr buffer);
    }
}
'@
    }
    Resolve-UIOperationEnvironment ([VirtualPaper.TestDesktopProbe]::Read())
}

function New-UIOperationSkipReport($Environment) {
    if ($Environment.Available -cne $false) { throw 'A UI skip requires an unavailable desktop.' }
    [pscustomobject]@{
        Status = 'Skipped'; Passed = $false; Skipped = $true; SkipReason = $Environment.ReasonCode
        ExpectedCount = 0; Results = @(); Environment = $Environment
    }
}

function Get-UIOperationReportOutcome($Report, [switch]$AllowFailure) {
    if ($Report.Skipped -eq $true -or $Report.Status -eq 'Skipped') {
        $environment = $Report.Environment
        if ($Report.Status -ne 'Skipped' -or $Report.Skipped -cne $true -or $Report.Passed -cne $false -or
            $Report.ExpectedCount -cne 0 -or $null -eq $Report.Results -or @($Report.Results).Count -ne 0 -or
            $environment.Available -cne $false -or
            $Report.SkipReason -notin @('WindowsRequired','NoInteractiveDesktop','NoDisplay','NoDesktopShell') -or
            $Report.SkipReason -ne $environment.ReasonCode -or [string]::IsNullOrWhiteSpace($environment.Reason)) {
            if ($AllowFailure) { return 'Failed' }
            throw 'Invalid environment-skip report; failed or executed tests cannot be accepted as skipped.'
        }
        return 'Skipped'
    }
    $names = @($Report.Results | ForEach-Object { $_.Name })
    $applications = @($Report.Results | Where-Object { $_.Name -like 'Application.*' })
    if ($Report.Passed -ne $true -or $Report.ExpectedCount -lt 1 -or
        @($Report.Results).Count -ne $Report.ExpectedCount -or
        @($Report.Results | Where-Object { $_.Passed -ne $true }).Count -gt 0 -or
        @($names | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0 -or
        @($names | Sort-Object -Unique).Count -ne $names.Count -or $applications.Count -lt 1 -or
        @($applications | Where-Object { $_.Evidence.CoreProcessId -lt 1 -or
            @($_.Evidence.UiProcessIds).Count -lt 1 -or $_.Evidence.ProcessesExitedNormally -ne $true }).Count -gt 0) {
        if ($AllowFailure) { return 'Failed' }
        throw 'WinUI operation results are missing, incomplete or failed.'
    }
    return 'Passed'
}
