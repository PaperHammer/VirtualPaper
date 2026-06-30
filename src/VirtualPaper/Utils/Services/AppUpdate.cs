using System.Text.Json;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Cores.AppUpdate.Models;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Utils.Services {
    public class GithubReleaseClient : IGithubReleaseClient {
        private const string PLUGIN_MANIFEST_ASSEST_NAME = "plugin-manifest.json";
        private const string PLUGIN_MANIFEST_SHA256_ASSET_NAME = "PLUGIN_MANIFEST_SHA256.txt";

        public async Task<ReleaseInfo> GetLatestRelease(bool isBeta) {
            var userName = "PaperHammer";
            var repositoryName = isBeta ? "VirtualPaper-beta" : "VirtualPaper_Mirror_Test";
            //var repositoryName = isBeta ? "VirtualPaper-beta" : "VirtualPaper";
            var gitRelease = await GithubUtil.GetLatestRelease(repositoryName, userName, 0);
            Version version = GithubUtil.GetVersion(gitRelease);
            string changelog = gitRelease.Body;

            var result = new ReleaseInfo {
                Version = version,
                Changelog = changelog
            };

            // Check for manifest
            var manifestAsset = GithubUtil.FindAsset(gitRelease, PLUGIN_MANIFEST_ASSEST_NAME);
            var manifestSha256Asset = GithubUtil.FindAsset(gitRelease, PLUGIN_MANIFEST_SHA256_ASSET_NAME);
            if (manifestAsset != null && manifestSha256Asset != null) {
                try {
                    // Download manifest and its expected hash
                    var manifestContent = await GithubUtil.DownloadAssetContent(manifestAsset);
                    var expectedHash = (await GithubUtil.DownloadAssetContent(manifestSha256Asset)).Trim().ToLowerInvariant();

                    // Verify manifest hash
                    var actualHash = FileUtil.GetChecksumSHA256FromContent(manifestContent);
                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase)) {
                        throw new InvalidOperationException($"Manifest SHA256 mismatch: expected {expectedHash}, got {actualHash}");
                    }

                    var manifest = JsonSerializer.Deserialize(manifestContent, UpdateManifestContext.Default.UpdateManifest);
                    if (manifest != null) {
                        result.Manifest = manifest;
                        result.AppBuild = manifest.AppBuild;

                        if (manifest.IsRestartUpdate) {
                            // Plugin update: gather plugin asset URIs, skip installer
                            foreach (var (pluginName, pluginInfo) in manifest.Plugins) {
                                var asset = GithubUtil.FindAsset(gitRelease, pluginInfo.Asset);
                                if (asset != null) {
                                    result.PluginAssetUris[pluginName] = new Uri(asset.BrowserDownloadUrl);
                                }
                            }
                            return result;
                        }
                    }
                }
                catch (Exception ex) {                    
                    ArcLog.GetLogger<GithubReleaseClient>().Error("Failed to parse or verify manifest", ex);
                }
            }

            // Install-style update: gather installer info
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
        public int CompareAssemblyVersion(Version? version) {
            return GithubUtil.CompareAssemblyVersion(version);
        }
    }
}
