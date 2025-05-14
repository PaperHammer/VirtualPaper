using System.Collections.ObjectModel;
using System.Drawing;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface IMonitorManagerClient : IDisposable {
        event EventHandler MonitorChanged;
        
        ReadOnlyCollection<IMonitor> Monitors { get; }
        IMonitor PrimaryMonitor { get; }
        Rectangle VirtulScreenBounds { get; }

        Task IdentifyMonitorsAsync();
    }
}
