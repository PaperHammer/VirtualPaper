using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Downloader;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Services.Interfaces;
using IDownloadService = VirtualPaper.Services.Interfaces.IDownloadService;

namespace VirtualPaper.Services.Download {
    public partial class MultiDownloadService : IDownloadService {
        public MultiDownloadService() {
            //CPU can get toasty.. should rate limit to 100MB/s ?
            var downloadOpt = new DownloadConfiguration() {
                BufferBlockSize = 8000, // usually, hosts support max to 8000 bytes, default values is 8000
                ChunkCount = 4, // 支持多线程断点续传  file parts to download, default value is 1
                //MaximumBytesPerSecond = 1024 * 1024 * 1, // download speed limit
                MaxTryAgainOnFailover = 5, // the maximum number of times to fail
                ParallelDownload = false, // download parts of file as parallel or not. Web value is false
                Timeout = 10000, // timeout (millisecond) Per stream block reader, default values is 1000
                // clear package chunks _data when download completed with failure, default value is false
                ClearPackageOnCompletionWithFailure = false,
                // Before starting the download, reserve the storage space of the file as file size, default value is false
                ReserveStorageSpaceBeforeStartingDownload = false,
            };

            _downloader = new DownloadService(downloadOpt);
            _parallelDownloaders = new List<DownloadService>();
        }


        /// <summary>
        /// 异步下载文件并逐步返回下载进度。
        /// </summary>
        public async IAsyncEnumerable<DownloadProgress> DownloadAsync(
            Uri uri,
            string saveFilePath,
            [EnumeratorCancellation] CancellationToken token) {
            await _downloader.Clear();

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var channel = Channel.CreateUnbounded<DownloadProgress>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

            void OnProgressChanged(object? sender, DownloadProgressChangedEventArgs e) {
                if (token.IsCancellationRequested)
                    return;

                var percent = (float)e.ProgressPercentage;
                var speed = (float)(e.BytesPerSecondSpeed / 1024.0 / 1024.0); // MB/s
                var remaining = TimeSpan.FromSeconds(
                    e.TotalBytesToReceive > 0
                        ? (e.TotalBytesToReceive - e.ReceivedBytesSize) / Math.Max(e.BytesPerSecondSpeed, 1)
                        : 0);

                // 尝试异步写入通道
                channel.Writer.TryWrite(new DownloadProgress(percent, speed, remaining, e.ReceivedBytesSize, e.TotalBytesToReceive));
            }

            void OnCompleted(object? sender, AsyncCompletedEventArgs e) {
                if (e.Error != null)
                    tcs.TrySetException(e.Error);
                else if (e.Cancelled)
                    tcs.TrySetCanceled(token);
                else
                    tcs.TrySetResult();

                channel.Writer.TryComplete();
            }

            _downloader.DownloadProgressChanged += OnProgressChanged;
            _downloader.DownloadFileCompleted += OnCompleted;

            try {
                var downloadTask = _downloader.DownloadFileTaskAsync(uri.AbsoluteUri, saveFilePath, token)
                    .ContinueWith(t => {
                        // 捕获潜在未观察异常
                        if (t.IsFaulted)
                            _ = t.Exception; // 标记已观察
                    }, TaskContinuationOptions.ExecuteSynchronously);

                // 异步读取 Channel 中的进度（消费者）
                await foreach (var progress in channel.Reader.ReadAllAsync(token))
                    yield return progress;

                await Task.WhenAll(downloadTask, tcs.Task);
            }
            finally {
                _downloader.DownloadProgressChanged -= OnProgressChanged;
                _downloader.DownloadFileCompleted -= OnCompleted;
            }
        }

        /// <summary>
        /// 并行下载多个文件，返回聚合后的总进度
        /// </summary>
        public async IAsyncEnumerable<DownloadProgress> DownloadMultipleAsync(
            IEnumerable<(Uri uri, string saveFilePath)> downloads,
            [EnumeratorCancellation] CancellationToken token) {

            var downloadList = downloads.ToList();
            if (downloadList.Count == 0) yield break;

            if (downloadList.Count == 1) {
                await foreach (var p in DownloadAsync(downloadList[0].uri, downloadList[0].saveFilePath, token))
                    yield return p;
                yield break;
            }

            var channel = Channel.CreateUnbounded<DownloadProgress>(
                new UnboundedChannelOptions { SingleReader = true });

            var perPlugin = new (long received, long total, float speed)[downloadList.Count];
            var lockObj = new object();

            void ReportAggregate() {
                long totalReceived = 0, totalAll = 0;
                float totalSpeed = 0;
                lock (lockObj) {
                    for (int i = 0; i < perPlugin.Length; i++) {
                        totalReceived += perPlugin[i].received;
                        totalAll += perPlugin[i].total;
                        totalSpeed += perPlugin[i].speed;
                    }
                }
                float percent = totalAll > 0 ? (float)totalReceived / totalAll * 100 : 0;
                TimeSpan remaining = totalSpeed > 0
                    ? TimeSpan.FromSeconds((totalAll - totalReceived) / (totalSpeed * 1024.0 * 1024.0))
                    : TimeSpan.Zero;
                channel.Writer.TryWrite(new DownloadProgress(percent, totalSpeed, remaining, totalReceived, totalAll));
            }

            var tasks = downloadList.Select((item, index) => Task.Run(async () => {
                var downloadOpt = new DownloadConfiguration() {
                    BufferBlockSize = 8000,
                    ChunkCount = 4,
                    MaxTryAgainOnFailover = 5,
                    Timeout = 10000,
                    ClearPackageOnCompletionWithFailure = false,
                    ReserveStorageSpaceBeforeStartingDownload = false,
                };
                var downloader = new DownloadService(downloadOpt);
                
                lock (_parallelDownloaders) {
                    _parallelDownloaders.Add(downloader);
                }
                
                try {
                    var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                    downloader.DownloadProgressChanged += (s, e) => {
                        if (token.IsCancellationRequested) return;
                        lock (lockObj) {
                            perPlugin[index] = (e.ReceivedBytesSize, e.TotalBytesToReceive, (float)(e.BytesPerSecondSpeed / 1024.0 / 1024.0));
                        }
                        ReportAggregate();
                    };
                    downloader.DownloadFileCompleted += (s, e) => {
                        if (e.Error != null) tcs.TrySetException(e.Error);
                        else if (e.Cancelled) tcs.TrySetCanceled(token);
                        else tcs.TrySetResult();
                    };

                    var downloadTask = downloader.DownloadFileTaskAsync(item.uri.AbsoluteUri, item.saveFilePath, token)
                        .ContinueWith(t => { if (t.IsFaulted) _ = t.Exception; }, TaskContinuationOptions.ExecuteSynchronously);

                    await Task.WhenAll(downloadTask, tcs.Task);
                }
                finally {
                    lock (_parallelDownloaders) {
                        _parallelDownloaders.Remove(downloader);
                    }
                }
            }, token)).ToArray();

            // 在后台等待所有下载完成，然后关闭 channel
            _ = Task.Run(async () => {
                try {
                    await Task.WhenAll(tasks);
                }
                catch { }
                channel.Writer.TryComplete();
            });

            await foreach (var p in channel.Reader.ReadAllAsync(token))
                yield return p;

            // 等待所有下载任务完成（传播异常）
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 下载 SHA256.txt 并缓存其内容
        /// </summary>
        public async Task<string> DownloadShaTxtAsync(Uri shaUri, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(shaUri);

            string tempFilePath = Path.Combine(Path.GetTempPath(), $"virtualpaper_sha256_{Guid.NewGuid():N}.txt");

            try {
                await _downloader.DownloadFileTaskAsync(shaUri.AbsoluteUri, tempFilePath, token);

                string sha256Content = await File.ReadAllTextAsync(tempFilePath, token);
                sha256Content = sha256Content.Trim();

                if (sha256Content.Length != 64 || !System.Text.RegularExpressions.Regex.IsMatch(sha256Content, @"^[a-fA-F0-9]{64}$"))
                    throw new InvalidDataException("The downloaded SHA256 file format is invalid");

                return sha256Content;
            }
            finally {
                try {
                    if (File.Exists(tempFilePath))
                        File.Delete(tempFilePath);
                }
                catch {
                    // 忽略清理错误
                }
            }
        }

        /// <summary>
        /// 校验下载文件的完整性
        /// </summary>
        /// <param name="filePath">要校验的文件路径</param>
        /// <param name="expectedSha256">预期的SHA256值</param>
        /// <param name="token">取消令牌</param>
        /// <returns>校验结果</returns>
        public async Task<bool> VerifyFileIntegrityAsync(string filePath, string expectedSha256, CancellationToken token = default) {
            return await FileUtil.VerifyFileIntegrityAsync(filePath, expectedSha256, token);
        }


        public void Pause() {
            if (_downloader.Status == DownloadStatus.Running)
                _downloader.Pause();
            
            List<DownloadService> snapshot;
            lock (_parallelDownloaders) {
                snapshot = _parallelDownloaders.ToList();
            }
            
            Parallel.ForEach(snapshot, downloader => {
                if (downloader.Status == DownloadStatus.Running)
                    downloader.Pause();
            });
        }

        public void Resume() {
            if (_downloader.Status == DownloadStatus.Paused)
                _downloader.Resume();
            
            List<DownloadService> snapshot;
            lock (_parallelDownloaders) {
                snapshot = _parallelDownloaders.ToList();
            }
            
            Parallel.ForEach(snapshot, downloader => {
                if (downloader.Status == DownloadStatus.Paused)
                    downloader.Resume();
            });
        }

        public void Dispose() {
            _downloader?.Dispose();
            lock (_parallelDownloaders) {
                foreach (var downloader in _parallelDownloaders) {
                    downloader.Dispose();
                }
                _parallelDownloaders.Clear();
            }
        }

        private readonly DownloadService _downloader;
        private readonly List<DownloadService> _parallelDownloaders;
    }
}
