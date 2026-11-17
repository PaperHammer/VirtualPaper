function Set-CIUIOperationResolution {
    param([int] $Width, [int] $Height)
    if (-not ('VirtualPaper.CIUiDisplay' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace VirtualPaper {
    public static class CIUiDisplay {
        // DEVMODEW retains the current device's other mode fields unchanged.
        [StructLayout(LayoutKind.Explicit, Size = 220)]
        private struct DisplayMode {
            [FieldOffset(68)] public ushort Size;
            [FieldOffset(72)] public uint Fields;
            [FieldOffset(172)] public uint Width;
            [FieldOffset(176)] public uint Height;
        }
        public static void Resize(int width, int height) {
            var mode = new DisplayMode { Size = 220 };
            if (!EnumDisplaySettingsW(null, -1, ref mode))
                throw new InvalidOperationException("Cannot read the CI display mode.");
            mode.Width = (uint)width; mode.Height = (uint)height;
            mode.Fields |= 0x80000 | 0x100000;
            int result = ChangeDisplaySettingsW(ref mode, 2); // CDS_TEST
            if (result != 0) throw new InvalidOperationException("CI display mode is unsupported: " + result);
            result = ChangeDisplaySettingsW(ref mode, 0); // Session only; do not update the registry.
            if (result != 0) throw new InvalidOperationException("Cannot apply the CI display mode: " + result);
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettingsW(string device, int index, ref DisplayMode mode);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsW(ref DisplayMode mode, uint flags);
    }
}
'@
    }
    [VirtualPaper.CIUiDisplay]::Resize($Width, $Height)
}

function Initialize-CIUIOperationDisplay {
    param($Environment,
        [scriptblock] $Resize = { param($Width, $Height) Set-CIUIOperationResolution $Width $Height },
        [scriptblock] $Probe = { Get-UIOperationEnvironment })
    # Never alter a developer's display, a locked session or a self-hosted desktop.
    if ($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_ENVIRONMENT -cne 'github-hosted' -or
        -not $Environment.Available -or
        ($Environment.Checks.DisplayWidth -ge 1920 -and $Environment.Checks.DisplayHeight -ge 1080)) { return $Environment }
    & $Resize 1920 1080
    $updated = & $Probe
    if (-not $updated.Available -or $updated.Checks.DisplayWidth -lt 1920 -or $updated.Checks.DisplayHeight -lt 1080) {
        throw 'CI display preparation did not provide an active 1920x1080 desktop.'
    }
    $updated | Add-Member -NotePropertyName OriginalDisplay -NotePropertyValue $Environment.Checks
    return $updated
}
