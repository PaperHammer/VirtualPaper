using System.ComponentModel;
using Downloader;
using VirtualPaper.Services.Interfaces;
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;
using IDownloadService = VirtualPaper.Services.Interfaces.IDownloadService;

namespace VirtualPaper.Services.Download {
    public class MultiDownloadService : IDownloadService {
        public event EventHandler<DownloadCompletedEventArgs>? DownloadFileCompleted;
        public event EventHandler<DownloadProgressEventArgs>? DownloadProgressChanged;
        public event EventHandler<DownloadEventArgs>? DownloadStarted;

        private double _previousDownloadedSize = -1;
        private readonly DownloadService _downloader;

        public MultiDownloadService() {
            //CPU can get toasty.. should rate limit to 100MB/s ?
            var downloadOpt = new DownloadConfiguration() {
                BufferBlockSize = 8000, // usually, hosts support max to 8000 bytes, default values is 8000
                ChunkCount = 1, // file parts to download, default value is 1
                //MaximumBytesPerSecond = 1024 * 1024 * 1, // download speed limit
                MaxTryAgainOnFailover = 5, // the maximum number of times to fail
                ParallelDownload = false, // download parts of file as parallel or not. Web value is false
                Timeout = 10000, // timeout (millisecond) Per stream block reader, default values is 1000
                // clear package chunks data when download completed with failure, default value is false
                ClearPackageOnCompletionWithFailure = false,
                // Before starting the download, reserve the storage space of the file as file size, default value is false
                ReserveStorageSpaceBeforeStartingDownload = false,
            };

            _downloader = new DownloadService(downloadOpt);
            _downloader.DownloadStarted += Downloader_DownloadStarted;
            // Provide any information about chunker downloads, like progress percentage Per chunk, speed, total received bytes and received bytes array to streaming.
            // _downloader.ChunkDownloadProgressChanged += OnChunkDownloadProgressChanged;
            // Provide any information about download progress, like progress percentage of sum of chunks, total speed, average speed, total received bytes and received bytes array to streaming.
            _downloader.DownloadProgressChanged += OnDownloadProgressChanged;
            // Download completed event that can include occurred errors or cancelled or download completed successfully.
            _downloader.DownloadFileCompleted += OnDownloadFileCompleted;
        }

        public async Task DownloadFile(Uri url, string filePath) {
            await _downloader.DownloadFileTaskAsync(url.AbsoluteUri, filePath);
        }

        private void Downloader_DownloadStarted(object? sender, DownloadStartedEventArgs e) {
            DownloadStarted?.Invoke(this,
                new DownloadEventArgs() {
                    TotalSize = Math.Truncate(ByteToMegabyte(e.TotalBytesToReceive)),
                    FileName = e.FileName
                }
            );
        }

        private void OnDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e) {
            var downloadedSize = Math.Truncate(ByteToMegabyte(e.ReceivedBytesSize));
            if (downloadedSize == _previousDownloadedSize)
                return;

            DownloadProgressEventArgs args = new() {
                TotalSize = Math.Truncate(ByteToMegabyte(e.TotalBytesToReceive)),
                DownloadedSize = downloadedSize,
                Percentage = e.ProgressPercentage,
            };
            _previousDownloadedSize = downloadedSize;

            DownloadProgressChanged?.Invoke(this, args);
        }

        private void OnDownloadFileCompleted(object? sender, AsyncCompletedEventArgs e) {
            DownloadCompletedEventArgs args = new();

            if (e.Cancelled) {
                //user cancelled
            }
            else if (e.Error != null) {
                args.IsCompleted = false;
                args.IsNormal = false;
                args.Msg = e.Error.Message;

                DownloadFileCompleted?.Invoke(this, args);
            }
            else {
                args.IsCompleted = true;
                args.IsNormal = true;

                DownloadFileCompleted?.Invoke(this, args);
            }
        }

        static double ByteToMegabyte(double bytes) {
            return bytes / 1024f / 1024f;
        }

        public void Cancel() {
            _downloader?.CancelAsync();
            _downloader?.Dispose();
        }
    }
}
