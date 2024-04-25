using System.Collections.ObjectModel;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Cores.Monitor
{
    /// <summary>
    /// 可用显示器配置
    /// </summary>
    public interface IMonitorManager
    {
        event EventHandler MonitorUpdated;

        ObservableCollection<Models.Cores.Monitor> Monitors { get; }

        Models.Cores.Monitor PrimaryMonitor { get; }
        Models.Cores.Monitor GetMonitorByHWnd(IntPtr hWnd);
        Models.Cores.Monitor GetMonitorByPoint(Point point);
        
        Rectangle VirtualScreenBounds { get; }
        uint OnHwndCreated(IntPtr hWnd, out bool register);
        IntPtr OnWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        
        bool IsMultiScreen();
        bool MonitorExists(IMonitor display);
    }
}
