#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][int] $ProcessId,
    [string] $Title = 'Virtual Paper',
    [Parameter(Mandatory)][string] $Destination
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Native window export requires Windows.' }
$process = Get-Process -Id $ProcessId
if ($process.ProcessName -cne 'VirtualPaper.UI') { throw 'Choose a VirtualPaper UI preview process.' }
# Reuse the acceptance snapshot implementation. PrintWindow exports window pixels
# without the pointer/feedback overlay added by interactive capture tools.
if (-not ('VirtualPaper.UIOperationTest.Infrastructure.StoreSnapshot' -as [type])) {
    Add-Type -AssemblyName System.Drawing.Common
    $snapshot = Get-Content (Join-Path $PSScriptRoot '../../test/VirtualPaper.UIOperationTest/Infrastructure/WindowSnapshot.cs') -Raw
    $source = "using System; using System.IO; using System.Text;`n$snapshot" + @'

public static class StoreSnapshot {
    public static void SaveOwned(int processId, string title, string path) {
        nint found = 0;
        int count = 0;
        EnumWindows((window, _) => {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner != processId) return true;
            var text = new StringBuilder(512);
            GetWindowText(window, text, text.Capacity);
            if (text.ToString() == title) { found = window; count++; }
            return true;
        }, 0);
        if (count != 1) throw new InvalidOperationException("Expected one matching preview window.");
        WindowSnapshot.Save(found, path);
        if (!File.Exists(path)) throw new IOException("Native window export failed.");
        // Trim only the invisible resize frame, using Windows' visible bounds.
        if (GetWindowRect(found, out var outer) && DwmGetWindowAttribute(found, 9, out var visible, 16) == 0) {
            byte[] png;
            using (var bitmap = new System.Drawing.Bitmap(path)) {
                var area = System.Drawing.Rectangle.Intersect(new(0, 0, bitmap.Width, bitmap.Height),
                    new(visible.Left - outer.Left, visible.Top - outer.Top, visible.Right - visible.Left, visible.Bottom - visible.Top));
                if (area.Width <= 0 || area.Height <= 0) throw new IOException("Invalid visible window bounds.");
                using var crop = bitmap.Clone(area, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var buffer = new MemoryStream();
                crop.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
                png = buffer.ToArray();
            }
            File.WriteAllBytes(path, png);
        }
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    private delegate bool Callback(nint window, nint parameter);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out Rect rect, int size);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, nint parameter);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder text, int count);
}
'@
    Add-Type -TypeDefinition $source -ReferencedAssemblies @(
        [System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Size].Assembly.Location
        Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows.*.dll' | ForEach-Object FullName
    )
}
$path = [IO.Path]::GetFullPath($Destination)
$temporary = Join-Path ([IO.Path]::GetDirectoryName($path)) "$([guid]::NewGuid().ToString('N')).png"
try {
    [VirtualPaper.UIOperationTest.Infrastructure.StoreSnapshot]::SaveOwned($ProcessId, $Title, $temporary)
    Move-Item -LiteralPath $temporary -Destination $path -Force
    Write-Host "[OK] Pointer-free native PNG: $path"
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
