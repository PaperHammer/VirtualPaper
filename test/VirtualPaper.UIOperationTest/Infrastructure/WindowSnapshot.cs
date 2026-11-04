using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class WindowSnapshot {
    public static void Save(nint hwnd, string path) {
        if (!GetWindowRect(hwnd, out var bounds)) return;
        using var bitmap = new Bitmap(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        using var graphics = Graphics.FromImage(bitmap);
        nint dc = graphics.GetHdc();
        bool captured;
        try { captured = PrintWindow(hwnd, dc, 2); }
        finally { graphics.ReleaseHdc(dc); }
        if (!captured) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path, ImageFormat.Png);
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect bounds);
    [DllImport("user32.dll")] private static extern bool PrintWindow(nint hwnd, nint dc, uint flags);
}
