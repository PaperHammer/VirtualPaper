using System.Drawing;

namespace VirtualPaper.Models.Cores.Interfaces {
    public interface IMonitor : IEquatable<IMonitor> {
        bool IsStale { get; set; }
        bool IsCloned { get; }
        string DeviceId { get; set; }
        Rectangle WorkingArea { get; set; }
        Rectangle Bounds { get; set; }

        string Content { get; set; }
        int SystemIndex { get; set; }
        bool IsPrimary { get; set; }
        string ThumbnailPath { get; set; }

        IMonitor CloneWithPrimaryInfo();
    }
}
