using System.Collections.ObjectModel;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Cores.Monitor {
    /// <summary>
    /// 可用显示器配置
    /// </summary>
    public interface IMonitorManager {
        event EventHandler MonitorUpdated;
        event EventHandler MonitorPropertyUpdated;

        ObservableCollection<Models.Cores.Monitor> Monitors { get; }
        Models.Cores.Monitor PrimaryMonitor { get; }
        Rectangle VirtualScreenBounds { get; }

        Models.Cores.Monitor GetMonitorByHWnd(IntPtr hWnd);
        Models.Cores.Monitor GetMonitorByPoint(Point point);
        uint OnHwndCreated(IntPtr hWnd, out bool register);
        bool IsMultiScreen();
        bool MonitorExists(IMonitor display);
        IntPtr OnWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        void UpdateTargetMonitorThu(int monitorIdx, string thumbnailPath);
    }
}
