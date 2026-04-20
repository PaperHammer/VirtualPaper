using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Downloader;
using VirtualPaper.Launcher.Services.Interfaces;
using IDownloadService = VirtualPaper.Launcher.Services.Interfaces.IDownloadService;

namespace VirtualPaper.Launcher.Services.Download {
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
                channel.Writer.TryWrite(new DownloadProgress(percent, speed, remaining));
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
        /// 下载 SHA256.txt 并缓存其内容
        /// </summary>
        public async Task<string> DownloadShaTxtAsync(Uri shaUri, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(shaUri);

            string tempFilePath = Path.Combine(Path.GetTempPath(), $"virtualpaper_sha256_{Guid.NewGuid():N}.txt");

            try {
                await _downloader.DownloadFileTaskAsync(shaUri.AbsoluteUri, tempFilePath, token);

                string sha256Content = await File.ReadAllTextAsync(tempFilePath, token);
                sha256Content = sha256Content.Trim();

                if (!IsValidSHA256(sha256Content))
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
            if (!File.Exists(filePath) || !IsValidSHA256(expectedSha256))
                return false;

            string actualSha256 = await CalculateFileSHA256Async(filePath, token);

            return string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase);
        }


        public void Pause() {
            if (_downloader.Status == DownloadStatus.Running)
                _downloader.Pause();
        }

        public void Resume() {
            if (_downloader.Status == DownloadStatus.Paused)
                _downloader.Resume();
        }

        public void Dispose() {
            _downloader?.Dispose();
        }

        #region Private Methods
        /// <summary>
        /// 计算文件的SHA256哈希值
        /// </summary>
        private static async Task<string> CalculateFileSHA256Async(string filePath, CancellationToken token) {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);

            var hashBytes = await sha256.ComputeHashAsync(fileStream, token);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// 验证SHA256字符串格式
        /// </summary>
        private static bool IsValidSHA256(string sha256) {
            if (string.IsNullOrEmpty(sha256) || sha256.Length != 64)
                return false;
            return SHA256Regex().IsMatch(sha256);
        }
        #endregion

        ///// <summary>
        ///// 内部轻量级异步通道（单消费者）
        ///// </summary>
        //private sealed class Channel<T> {
        //    private readonly Queue<T> _queue = new();
        //    private readonly SemaphoreSlim _signal = new(0);

        //    public void TryWrite(T value) {
        //        lock (_queue) {
        //            _queue.Enqueue(value);
        //        }
        //        _signal.Release();
        //    }

        //    public async IAsyncEnumerable<T> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token) {
        //        while (!token.IsCancellationRequested) {
        //            await _signal.WaitAsync(token);
        //            T item;
        //            lock (_queue) {
        //                if (_queue.Count == 0) continue;
        //                item = _queue.Dequeue();
        //            }
        //            yield return item;
        //        }
        //    }
        //}

        private readonly DownloadService _downloader;

        [System.Text.RegularExpressions.GeneratedRegex(@"^[a-fA-F0-9]{64}$")]
        private static partial System.Text.RegularExpressions.Regex SHA256Regex();
    }
}
