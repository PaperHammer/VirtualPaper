using System;

namespace VirtualPaper.Launcher.Models {
    public record AppUpdateInfo(Uri DownloadUri, Uri SHAUri, string Version, string ChangeLog);
}
