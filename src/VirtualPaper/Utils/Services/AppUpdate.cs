using System.Text.Json;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Cores.AppUpdate.Models;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Utils.Services {
    public class GithubReleaseClient : IGithubReleaseClient {
        private const string PLUGINS_PATCH_ASSET_NAME = "plugins_patch.zip";
        private const string PLUGINS_PATCH_SHA256_ASSET_NAME = "PLUGINS_PATCH_SHS256.txt";
        private const string APP_COMP_MANIFEST_ASSET_NAME = "app_comp_manifest.json";

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

            // Check for plugin update: plugins_patch.zip + PLUGINS_PATCH_SHS256.txt
            var patchAsset = GithubUtil.FindAsset(gitRelease, PLUGINS_PATCH_ASSET_NAME);
            var patchSha256Asset = GithubUtil.FindAsset(gitRelease, PLUGINS_PATCH_SHA256_ASSET_NAME);
            if (patchAsset != null && patchSha256Asset != null) {
                result.PluginPatchUri = new Uri(patchAsset.BrowserDownloadUrl);
                result.PluginPatchSha256Uri = new Uri(patchSha256Asset.BrowserDownloadUrl);

                // Download app_comp_manifest.json from release assets for build info
                var appCompManifestAsset = GithubUtil.FindAsset(gitRelease, APP_COMP_MANIFEST_ASSET_NAME);
                if (appCompManifestAsset != null) {
                    try {
                        var manifestContent = await GithubUtil.DownloadAssetContent(appCompManifestAsset);
                        var appCompManifest = JsonSerializer.Deserialize(manifestContent, UpdateManifestContext.Default.AppCompManifest);
                        if (appCompManifest != null) {
                            result.AppCompManifest = appCompManifest;
                            result.AppBuild = appCompManifest.AppBuildNumber;
                        }
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<GithubReleaseClient>().Error("Failed to parse app_comp_manifest", ex);
                    }
                }

                return result;
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
