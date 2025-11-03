namespace VirtualPaper.Services.Interfaces {
    public interface IDownloadService {
        IAsyncEnumerable<DownloadProgress> DownloadAsync(Uri uri, string saveFilePath, CancellationToken token);
        Task<string> DownloadShaTxtAsync(Uri shaUri, CancellationToken token);
        Task<bool> VerifyFileIntegrityAsync(string filePath, string expectedSha256, CancellationToken token = default);
        void Pause();
        void Resume();
    }

    public record DownloadProgress(float Percent, float Speed, TimeSpan Remaining);
}
