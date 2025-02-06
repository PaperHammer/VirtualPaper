using System.ComponentModel;

namespace VirtualPaper.Common.Models {
    public class AppUpdaterEventArgs : EventArgs {
        public AppUpdaterEventArgs(AppUpdateStatus updateStatus, Version updateVersion, DateTime updateDate, Uri updateUri, string changeLog) {
            UpdateStatus = updateStatus;
            UpdateVersion = updateVersion;
            UpdateUri = updateUri;
            UpdateDate = updateDate;
            ChangeLog = changeLog;
        }

        public AppUpdateStatus UpdateStatus { get; }
        public Version UpdateVersion { get; }
        public Uri UpdateUri { get; }
        public DateTime UpdateDate { get; }
        public string ChangeLog { get; }
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
    }
}
