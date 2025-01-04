using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using NLog;
using VirtualPaper.Common;
using VirtualPaper.Common.Models;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.Update;

namespace VirtualPaper.Grpc.Client {
    public partial class AppUpdaterClient : IAppUpdaterClient {
        public event EventHandler<AppUpdaterEventArgs>? UpdateChecked;

        public AppUpdateStatus Status { get; private set; } = AppUpdateStatus.notchecked;
        public DateTime LastCheckTime { get; private set; } = DateTime.MinValue;
        public Version LastCheckVersion { get; private set; } = new Version(0, 0, 0, 0);
        public string LastCheckChangelog { get; private set; } = string.Empty;
        public Uri LastCheckUri { get; private set; }

        public AppUpdaterClient() {
            _client = new Grpc_UpdateService.Grpc_UpdateServiceClient(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));

            Task.Run(() => {
                UpdateStatusRefresh().ConfigureAwait(false);
            }).Wait();

            _cancellationTokenUpdateChecked = new CancellationTokenSource();
            _updateCheckedChangedTask = Task.Run(() => SubscribeUpdateCheckedStream(_cancellationTokenUpdateChecked.Token));
        }

        public async Task CheckUpdate() {
            await _client.CheckUpdateAsync(new Empty());
        }

        public async Task StartUpdate() {
            await _client.StartDownloadAsync(new Empty());
        }

        private async Task UpdateStatusRefresh() {
            var resp = await _client.GetUpdateStatusAsync(new Empty());
            Status = (AppUpdateStatus)((int)resp.Status);
            LastCheckTime = resp.Time.ToDateTime().ToLocalTime();
            LastCheckChangelog = resp.Changelog;
            try {
                LastCheckVersion = string.IsNullOrEmpty(resp.Version) ? null : new Version(resp.Version);
                LastCheckUri = string.IsNullOrEmpty(resp.Url) ? null : new Uri(resp.Url);
            }
            catch { /* TODO */ }
        }

        private async Task SubscribeUpdateCheckedStream(CancellationToken token) {
            try {
                using var call = _client.SubscribeUpdateChecked(new Empty(), cancellationToken: token);
                while (await call.ResponseStream.MoveNext(token)) {
                    await _updateCheckedLock.WaitAsync(token);
                    try {
                        var resp = call.ResponseStream.Current;
                        await UpdateStatusRefresh();
                        UpdateChecked?.Invoke(this, new AppUpdaterEventArgs(Status, LastCheckVersion, LastCheckTime, LastCheckUri, LastCheckChangelog));
                    }
                    finally {
                        _updateCheckedLock.Release();
                    }
                }
            }
            catch (Exception e) {
                _logger.Error(e);
            }
        }

        #region Dispose
        private bool _disposedValue;
        protected virtual void Dispose(bool disposing) {
            if (!_disposedValue) {
                if (disposing) {
                    _cancellationTokenUpdateChecked?.Cancel();
                    _updateCheckedChangedTask?.Wait();
                }
                _disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly Grpc_UpdateService.Grpc_UpdateServiceClient _client;
        private readonly SemaphoreSlim _updateCheckedLock = new(1, 1);
        private readonly CancellationTokenSource _cancellationTokenUpdateChecked;
        private readonly Task _updateCheckedChangedTask;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
