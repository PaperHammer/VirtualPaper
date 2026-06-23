using System.Text.Json;
using VirtualPaper.Common.Utils;
using VirtualPaper.Cores.AppUpdate.Models;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Utils.Services {
    public class GithubReleaseClient : IGithubReleaseClient {
        private const string ManifestAssetName = "update_manifest.json";

        public async Task<ReleaseInfo> GetLatestRelease(bool isBeta) {
            var userName = "PaperHammer";
            var repositoryName = isBeta ? "VirtualPaper-beta" : "VirtualPaper";
            var gitRelease = await GithubUtil.GetLatestRelease(repositoryName, userName, 0);
            Version version = GithubUtil.GetVersion(gitRelease);
            string changelog = gitRelease.Body;

            var result = new ReleaseInfo {
                Version = version,
                Changelog = changelog
            };

            // Check for manifest
            var manifestAsset = GithubUtil.FindAsset(gitRelease, ManifestAssetName);
            if (manifestAsset != null) {
                try {
                    var manifestContent = await GithubUtil.DownloadAssetContent(manifestAsset);
                    var manifest = JsonSerializer.Deserialize(manifestContent, UpdateManifestContext.Default.UpdateManifest);
                    if (manifest != null) {
                        result.Manifest = manifest;

                        if (manifest.IsRestartUpdate) {
                            // Gather plugin asset URIs
                            foreach (var (pluginName, pluginInfo) in manifest.Plugins) {
                                var asset = GithubUtil.FindAsset(gitRelease, pluginInfo.Asset);
                                if (asset != null) {
                                    result.PluginAssetUris[pluginName] = new Uri(asset.BrowserDownloadUrl);
                                }
                            }
                        }
                    }
                }
                catch {
                    // Manifest parsing failed, fall through to install-style
                }
            }

            // Always gather installer info (for install-style or as fallback)
            var installerAsset = GithubUtil.FindAsset(gitRelease, "virtualpaper_setup_x64_full");
            if (installerAsset != null) {
                result.InstallerUri = new Uri(installerAsset.BrowserDownloadUrl);
            }

            var shaAsset = GithubUtil.FindAsset(gitRelease, "SHA256");
            if (shaAsset != null) {
                result.InstallerShaUri = new Uri(shaAsset.BrowserDownloadUrl);
            }

            return result;
        }
    }

    public class AssemblyVersionComparer : IVersionComparer {
        public int CompareAssemblyVersion(Version version) {
            return GithubUtil.CompareAssemblyVersion(version);
        }
    }
}
