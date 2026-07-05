using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Models.Events;

namespace VirtualPaper.Cores.AppUpdate {
    public interface IAppUpdaterService {
        event EventHandler<AppUpdaterEventArgs> UpdateChecked;

        AppUpdateStatus Status { get; }
        ReleaseInfo? LastReleaseInfo { get; }

        Task<AppUpdateStatus> CheckUpdateAsync(int fetchDelay = 45000);
        void Start();
        void Stop();
    }
}
