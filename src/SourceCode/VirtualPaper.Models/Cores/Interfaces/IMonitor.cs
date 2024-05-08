using System.Drawing;

namespace VirtualPaper.Models.Cores.Interfaces
{
    public interface IMonitor : IEquatable<IMonitor>
    {
        string DeviceId { get; set; }
        string DeviceName { get; set; }
        string MonitorName { get; set; }

        IntPtr HMonitor { get; set; }
        string Content { get; set; }
        bool IsPrimary { get; set; }

        Rectangle WorkingArea { get; set; }
        Rectangle Bounds { get; set; }
    }
}
