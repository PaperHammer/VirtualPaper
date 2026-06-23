using VirtualPaper.Cores.AppUpdate.Models;

namespace VirtualPaper.Utils.Interfcaes {
    public interface IGithubReleaseClient {
        Task<ReleaseInfo> GetLatestRelease(bool isBeta);
    }

    public interface IVersionComparer {
        int CompareAssemblyVersion(Version version);
    }

    public class ReleaseInfo {
        public Version Version { get; set; } = new(0, 0, 0, 0);
        public string Changelog { get; set; } = string.Empty;
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
