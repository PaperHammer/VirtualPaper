using System.IO;
using VirtualPaper.Common.Logging;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Cores.AppUpdate.Specific {
    public abstract class UpdateServiceBase<T>(IDownloadService downloadService) where T : class {
        protected async Task<(bool Success, string? FilePath, string? Error)> DownloadFileAsync(
            Uri uri, string targetDir, string fileName,
            IProgress<DownloadProgress>? progress, CancellationToken token) {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);

            try {
                Directory.CreateDirectory(targetDir);

                var filePath = Path.Combine(targetDir, fileName);
                await foreach (var p in downloadService.DownloadAsync(uri, filePath, linkedCts.Token)) {
                    progress?.Report(p);
                }

                return (true, filePath, null);
            }
            catch (Exception ex) when (!token.IsCancellationRequested) {
                ArcLog.GetLogger<T>().Error($"{typeof(T).Name} download failed", ex);
                return (false, null, ex.Message);
            }
            finally {
                linkedCts.Cancel();
            }
        }
    }
}
