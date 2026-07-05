using VirtualPaper.Cores.AppUpdate.Models;

namespace VirtualPaper.Models.AppUpdate {
    public class ReleaseInfo {
        public Version? Version { get; set; }
        public string? AppBuild { get; set; }
        public string Changelog { get; set; } = string.Empty;
        public DateTime CheckedTime { get; set; }

        // For plugin update (plugins_patch.zip)
        public Uri? PluginPatchUri { get; set; }
        public Uri? PluginPatchSha256Uri { get; set; }
        public AppCompManifest? AppCompManifest { get; set; }

        // For installer update
        public Uri? InstallerUri { get; set; }
        public Uri? InstallerShaUri { get; set; }

        public bool IsPluginsUpdate => PluginPatchUri != null;
        public bool IsInstallerUpdate => !IsPluginsUpdate;
    }
}
