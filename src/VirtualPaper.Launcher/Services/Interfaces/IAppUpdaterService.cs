using System;
using System.Threading.Tasks;
using VirtualPaper.Common.Events;

namespace VirtualPaper.Launcher.Services.Interfaces {
    public interface IAppUpdaterService {
        event EventHandler<AppUpdaterEventArgs> UpdateChecked;

        string LastCheckChangelog { get; }
        DateTime LastCheckTime { get; }
        Uri LastCheckUri { get; }
        Uri? LastCheckShaUri { get; }
        Version LastCheckVersion { get; }
        AppUpdateStatus Status { get; }

        Task<AppUpdateStatus> CheckUpdate(int fetchDelay = 45000);
        Task<(Uri exeUri, Uri shaUri, Version version, string changelog)> GetLatestRelease(bool isBeta);
        void Start();
        void Stop();
    }
}
