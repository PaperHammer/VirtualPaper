using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.Cores.Monitor
{
    public class MonitorManager : ObservableObject, IMonitorManager
    {
        public event EventHandler? MonitorUpdated;

        public ObservableCollection<Models.Cores.Monitor> Monitors { get; } = [];
        public Models.Cores.Monitor PrimaryMonitor => Monitors.FirstOrDefault(x => x.IsPrimary, null);

        private Rectangle _virtualScreenBounds = Rectangle.Empty;
        public Rectangle VirtualScreenBounds
        {
            get => _virtualScreenBounds;
            private set { _virtualScreenBounds = value; OnPropertyChanged(); }
        }

        public MonitorManager()
        {
            RefreshMonitorList();
        }

        public Models.Cores.Monitor GetMonitorByHWnd(nint hWnd)
        {
            IntPtr hMonitor = multiMonitorSupport
                ? Native.MonitorFromWindow(new HandleRef(null, hWnd), MONITOR_DEFAULTTONEAREST)
                : (IntPtr)PRIMARY_MONITOR;

            return GetMonitorByHMonitor(hMonitor);
        }

        public Models.Cores.Monitor GetMonitorByPoint(Point point)
        {
            IntPtr hMonitor;
            if (multiMonitorSupport)
            {
                var pt = new Native.POINT(  //POINTSTRUCT
                    (int)Math.Round((double)point.X),
                    (int)Math.Round((double)point.Y));
                hMonitor = Native.MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            }
            else
                hMonitor = (IntPtr)PRIMARY_MONITOR;

            return GetMonitorByHMonitor(hMonitor);
        }

        public bool IsMultiScreen()
        {
            return Monitors.Count > 1;
        }

        public uint OnHwndCreated(nint hWnd, out bool register)
        {
            register = false;
            return 0;
        }

        public nint OnWndProc(nint hwnd, uint msg, nint wParam, nint lParam)
        {
            if (msg == (uint)Native.WM.DISPLAYCHANGE)
            {
                RefreshMonitorList();
            }
            return IntPtr.Zero;
        }

        public bool MonitorExists(IMonitor display)
        {
            return Monitors.Any(x => display.Equals(x));
        }

        /// <summary>
        /// 更新可用显示器列表
        /// </summary>
        private void RefreshMonitorList()
        {
            multiMonitorSupport = Native.GetSystemMetrics((int)Native.SystemMetric.SM_CMONITORS) != 0;

            var hMonitors = GetHMonitors();

            // 标记旧显示器
            foreach (var monitor in Monitors)
            {
                monitor.IsStale = true;
            }

            // 写入新显示器
            for (int i = 0; i < hMonitors.Count; i++)
            {
                var monitor = GetMonitorByHMonitor(hMonitors[i]);
                monitor.Index = i + 1;
            }

            // 移除旧显示器
            var staleDisplayMonitors = Monitors
                .Where(x => x.IsStale).ToList();
            foreach (var monitor in staleDisplayMonitors)
            {
                Monitors.Remove(monitor);
            }
            staleDisplayMonitors.Clear();
            staleDisplayMonitors = null;

            /*
             * 通过标记来删除显示器信息的原因：
             *  避免并发问题：直接清空整个 Monitors 集合并在之后添加新的监视器信息可能会导致数据不一致，特别是在多线程环境下。通过标记过时的项并随后删除，可以确保在处理过程中不会丢失任何有效的监视器更新。
             *  维护引用一致性：如果其他组件或对象正在使用当前列表中的监视器实例，直接清空可能导致这些外部引用失效或指向不再存在的对象。而先标记后删除的方法可以提供一个更平滑的过渡过程，使得依赖于这些监视器信息的对象有机会在新数据到来前释放旧资源。
             *  逻辑分离：标记和删除步骤的分离使得代码逻辑更加清晰，容易理解和维护。首先识别哪些是需要移除的项（即旧数据或已不存在的显示器），然后进行清理操作。
             *  性能优化：对于大型集合，遍历一次并仅删除需要移除的元素通常比每次都重新创建整个集合要高效得多。
             *  增量更新：当系统频繁检测到显示器变化时，每次只移除和添加发生变化的显示器，而不是完全重建整个显示器列表，有利于提高效率和响应速度。
             */

            VirtualScreenBounds = GetVirtualScreenBounds();

            MonitorUpdated?.Invoke(this, EventArgs.Empty);
        }

        private Models.Cores.Monitor GetMonitorByHMonitor(IntPtr hMonitor)
        {
            Models.Cores.Monitor? monitor;

            if (!multiMonitorSupport || hMonitor == (IntPtr)PRIMARY_MONITOR)
            {
                monitor = GetMonitorByDeviceName(defaultDisplayDeviceName);

                if (monitor == null)
                {
                    monitor = new Models.Cores.Monitor(defaultDisplayDeviceName);
                    Monitors.Add(monitor);
                }

                monitor.Bounds = GetVirtualScreenBounds();
                monitor.DeviceId = GetDefaultMonitorDeviceId();
                monitor.MonitorName = "Monitor";
                monitor.HMonitor = hMonitor;
                monitor.IsPrimary = true;
                monitor.WorkingArea = GetWorkingArea();

                monitor.IsStale = false;
            }
            else
            {
                var info = new Native.MONITORINFOEX();// MONITORINFOEX();
                Native.GetMonitorInfo(new HandleRef(null, hMonitor), info);

                string deviceName = new string(info.szDevice).TrimEnd((char)0);

                monitor = GetMonitorByDeviceName(deviceName);

                monitor ??= CreateMonitorByMonitorInfo(deviceName);

                monitor.HMonitor = hMonitor;

                UpdateDisplayMonitor(monitor, info);
            }

            return monitor;
        }

        private Models.Cores.Monitor CreateMonitorByMonitorInfo(string deviceName)
        {
            var monitor = new Models.Cores.Monitor(deviceName);

            var displayDevice = GetMonitorDevice(deviceName);
            monitor.DeviceId = displayDevice.DeviceID;
            monitor.MonitorName = displayDevice.DeviceString;

            Monitors.Add(monitor);

            return monitor;
        }

        private Models.Cores.Monitor GetMonitorByDeviceName(string deviceName)
        {
            return Monitors.FirstOrDefault(x => x.DeviceName == deviceName);
        }

        private void UpdateDisplayMonitor(Models.Cores.Monitor monitor, Native.MONITORINFOEX info)
        {
            // 确保在 应用程序清单文件 里开启对每一块屏幕的 DPI 感知
            monitor.Bounds = new Rectangle(
                info.rcMonitor.Left, info.rcMonitor.Top,
                info.rcMonitor.Right - info.rcMonitor.Left,
                info.rcMonitor.Bottom - info.rcMonitor.Top);

            monitor.IsPrimary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0;

            monitor.WorkingArea = new Rectangle(
                info.rcWork.Left, info.rcWork.Top,
                info.rcWork.Right - info.rcWork.Left,
                info.rcWork.Bottom - info.rcWork.Top);

            monitor.IsStale = false;
        }

        /// <summary>
        /// 获取 HMonitor 类型的显示器句柄
        /// </summary>
        /// <returns></returns>
        private IList<IntPtr> GetHMonitors()
        {
            if (multiMonitorSupport)
            {
                var hMonitors = new List<IntPtr>();

                bool callback(IntPtr monitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr lParam)
                {
                    hMonitors.Add(monitor);
                    return true;
                }

                Native.EnumDisplayMonitors(new HandleRef(null, IntPtr.Zero), null, callback, IntPtr.Zero);

                return hMonitors;
            }

            return [(IntPtr)PRIMARY_MONITOR];
        }

        private static Native.DISPLAY_DEVICE GetMonitorDevice(string deviceName)
        {
            var result = new Native.DISPLAY_DEVICE();

            var displayDevice = new Native.DISPLAY_DEVICE();
            displayDevice.cb = Marshal.SizeOf(displayDevice);
            try
            {
                for (uint id = 0; Native.EnumDisplayDevices(deviceName, id, ref displayDevice, Native.EDD_GET_DEVICE_INTERFACE_NAME); id++)
                {
                    if (displayDevice.StateFlags.HasFlag(Native.DisplayDeviceStateFlags.AttachedToDesktop)
                        && !displayDevice.StateFlags.HasFlag(Native.DisplayDeviceStateFlags.MirroringDriver))
                    {
                        result = displayDevice;
                        break;
                    }

                    displayDevice.cb = Marshal.SizeOf(displayDevice);
                }
            }
            catch { }

            if (string.IsNullOrEmpty(result.DeviceID)
                || string.IsNullOrWhiteSpace(result.DeviceID))
            {
                result.DeviceID = GetDefaultMonitorDeviceId();
            }

            return result;
        }

        private static string GetDefaultMonitorDeviceId() => Native.GetSystemMetrics((int)Native.SystemMetric.SM_REMOTESESSION) != 0 ?
                    "\\\\?\\DISPLAY#REMOTEDISPLAY#" : "\\\\?\\DISPLAY#LOCALDISPLAY#";
        /// <summary>
        /// 获取 Windows 定义的显示器虚拟边界
        /// </summary>
        /// <returns></returns>
        private static Rectangle GetVirtualScreenBounds()
        {
            var location = new Point(Native.GetSystemMetrics(
                (int)Native.SystemMetric.SM_XVIRTUALSCREEN), Native.GetSystemMetrics((int)Native.SystemMetric.SM_YVIRTUALSCREEN));
            var size = new Size(Native.GetSystemMetrics(
                (int)Native.SystemMetric.SM_CXVIRTUALSCREEN), Native.GetSystemMetrics((int)Native.SystemMetric.SM_CYVIRTUALSCREEN));
            return new Rectangle(location, size);
        }

        private static Rectangle GetWorkingArea()
        {
            var rc = new Native.RECT();
            Native.SystemParametersInfo((int)Native.SPI.SPI_GETWORKAREA, 0, ref rc, 0);
            return new Rectangle(rc.Left, rc.Top,
                rc.Right - rc.Left, rc.Bottom - rc.Top);
        }

        private const int PRIMARY_MONITOR = unchecked((int)0xBAADF00D);

        private const int MONITORINFOF_PRIMARY = 0x00000001;
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        private static bool multiMonitorSupport;
        private const string defaultDisplayDeviceName = "DISPLAY";
    }
}
