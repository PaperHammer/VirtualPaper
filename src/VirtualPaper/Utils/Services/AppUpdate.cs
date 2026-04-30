using VirtualPaper.Common.Utils;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Utils.Services {
    public class GithubReleaseClient : IGithubReleaseClient {
        public async Task<(Uri exeUri, Uri shaUri, Version version, string changelog)> GetLatestRelease(bool isBeta) {
            var userName = "PaperHammer";
            var repositoryName = isBeta ? "VirtualPaper-beta" : "VirtualPaper";
            var gitRelease = await GithubUtil.GetLatestRelease(repositoryName, userName, 0);
            Version version = GithubUtil.GetVersion(gitRelease);

            var gitUrl = await GithubUtil.GetAssetUrl(
                "virtualpaper_setup_x64_full",
                gitRelease, repositoryName, userName);
            Uri exeUri = new(gitUrl);
            string changelog = gitRelease.Body;

            gitUrl = await GithubUtil.GetAssetUrl(
                "SHA256",
                gitRelease, repositoryName, userName);
            Uri shaUri = new(gitUrl);

            return (exeUri, shaUri, version, changelog);
        }
    }

    public class AssemblyVersionComparer : IVersionComparer {
        public int CompareAssemblyVersion(Version version) {
            return GithubUtil.CompareAssemblyVersion(version);
        }
    }
}
