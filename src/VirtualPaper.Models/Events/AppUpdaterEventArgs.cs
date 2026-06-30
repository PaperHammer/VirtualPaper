using System.ComponentModel;
using VirtualPaper.Models.AppUpdate;

namespace VirtualPaper.Models.Events {
    public class AppUpdaterEventArgs(AppUpdateStatus updateStatus, ReleaseInfo? release) : EventArgs {
        public AppUpdateStatus UpdateStatus { get; } = updateStatus;
        public ReleaseInfo? Release { get; } = release;
    }

    public enum AppUpdateStatus {
        [Description("Software is up-to-date.")]
        Uptodate,
        [Description("Update available.")]
        Available,
        [Description("Installed software version higher than whats available online.")]
        Invalid,
        [Description("Update not checked yet.")]
        Notchecked,
        [Description("Update check failed.")]
        Error,
        [Description("Installer ready.")]
        InstallerReady,
        [Description("Plugins ready.")]
        PluginsReady,
    }
}
