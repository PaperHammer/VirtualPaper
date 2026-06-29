using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Models.Events;

namespace VirtualPaper.Cores.AppUpdate {
    public interface IAppUpdaterService {
        event EventHandler<AppUpdaterEventArgs> UpdateChecked;

        //string LastCheckChangelog { get; }
        //DateTime LastCheckTime { get; }
        //Uri LastCheckUri { get; }
        //Uri? LastCheckShaUri { get; }
        //Version LastCheckVersion { get; }
        AppUpdateStatus Status { get; }
        ReleaseInfo? LastReleaseInfo { get; }

        Task<AppUpdateStatus> CheckUpdate(int fetchDelay = 45000);
        void Start();
        void Stop();
    }
}
