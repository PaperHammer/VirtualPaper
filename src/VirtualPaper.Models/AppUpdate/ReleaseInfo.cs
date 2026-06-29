using VirtualPaper.Cores.AppUpdate.Models;

namespace VirtualPaper.Models.AppUpdate {
    public class ReleaseInfo {
        public Version? Version { get; set; }
        public string? AppBuild { get; set; }
        public string Changelog { get; set; } = string.Empty;
        public DateTime CheckedTime { get; set; }
        public UpdateManifest? Manifest { get; set; }

        // For install-style update
        public Uri? InstallerUri { get; set; }
        public Uri? InstallerShaUri { get; set; }

        // For restart-style update - asset download URIs
        public Dictionary<string, Uri> PluginAssetUris { get; set; } = new();

        public bool IsRestartUpdate => Manifest?.IsRestartUpdate == true;
        public bool IsInstallUpdate => Manifest == null || Manifest.IsInstallUpdate;
    }
}
