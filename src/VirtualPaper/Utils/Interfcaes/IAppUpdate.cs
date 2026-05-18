namespace VirtualPaper.Utils.Interfcaes {
    public interface IGithubReleaseClient {
        Task<(Uri exeUri, Uri shaUri, Version version, string changelog)>
            GetLatestRelease(bool isBeta);
    }

    public interface IVersionComparer {
        int CompareAssemblyVersion(Version version);
    }
}
