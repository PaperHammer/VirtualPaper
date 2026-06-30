using VirtualPaper.Models.AppUpdate;

namespace VirtualPaper.Utils.Interfcaes {
    public interface IGithubReleaseClient {
        Task<ReleaseInfo> GetLatestRelease(bool isBeta);
    }

    public interface IVersionComparer {
        int CompareAssemblyVersion(Version? version);
    }
}
