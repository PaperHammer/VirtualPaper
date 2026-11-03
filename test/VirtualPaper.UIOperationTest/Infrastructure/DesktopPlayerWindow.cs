using System.Runtime.InteropServices;
using System.Text;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class DesktopPlayerWindow {
    // WPF ShowInTaskbar=false windows can have a hidden owner and no Process.MainWindowHandle.
    public static nint FindVisibleTopLevel(int processId) {
        nint found = 0;
        EnumWindows((hwnd, _) => {
            GetWindowThreadProcessId(hwnd, out uint ownerId);
            if (ownerId != processId || !IsWindowVisible(hwnd)) return true;
            found = hwnd;
            return false;
        }, 0);
        return found;
    }

    public static nint Find(int processId, out string[] windows) {
        nint found = 0;
        var candidates = new List<string>();
        bool Visit(nint hwnd, nint _) {
            GetWindowThreadProcessId(hwnd, out uint ownerId);
            if (ownerId != processId) return true;
            var name = new StringBuilder(256);
            // GetParent returns a popup's owner; SetParent does not change its
            // WS_POPUP style. GA_PARENT reads the actual desktop parent instead.
            nint parent = GetAncestor(hwnd, 1);
            GetClassName(parent, name, name.Capacity);
            candidates.Add($"{hwnd:X}: parent {parent:X} {name}");
            if (name.ToString() is not ("WorkerW" or "Progman")) return true;
            found = hwnd;
            return false;
        }
        EnumWindows((hwnd, _) => {
            if (!Visit(hwnd, 0)) return false;
            EnumChildWindows(hwnd, Visit, 0);
            return found == 0;
        }, 0);
        windows = candidates.ToArray();
        return found;
    }

    private delegate bool EnumWindow(nint hwnd, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumWindow callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int count);
}
