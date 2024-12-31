using VirtualPaper.Common.Models;

namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface IAppUpdaterClient : IDisposable {
        string LastCheckChangelog { get; }
        DateTime LastCheckTime { get; }
        Uri LastCheckUri { get; }
        Version LastCheckVersion { get; }
        AppUpdateStatus Status { get; }

        event EventHandler<AppUpdaterEventArgs> UpdateChecked;

        Task CheckUpdate();
        Task StartUpdate();
    }
}
