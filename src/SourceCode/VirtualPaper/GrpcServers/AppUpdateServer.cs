using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using NLog;
using System.Windows.Threading;
using VirtualPaper.Common.Models;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Grpc.Service.Update;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.GrpcServers
{
    public class AppUpdateServer(IAppUpdaterService updater) : UpdateService.UpdateServiceBase
    {
        public override async Task<Empty> CheckUpdate(Empty _, ServerCallContext context)
        {
            await _updater.CheckUpdate(0);
            return await Task.FromResult(new Empty());
        }

        public override Task<Empty> StartDownload(Empty _, ServerCallContext context)
        {
            if (updater.Status == AppUpdateStatus.available)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new ThreadStart(delegate
                {
                    App.AppUpdateDialog(updater.LastCheckUri, updater.LastCheckChangelog);
                }));
            }
            return Task.FromResult(new Empty());
        }

        public override Task<UpdateResponse> GetUpdateStatus(Empty _, ServerCallContext context)
        {
            return Task.FromResult(new UpdateResponse()
            {
                Status = (UpdateStatus)((int)_updater.Status),
                Changelog = _updater.LastCheckChangelog ?? string.Empty,
                Url = _updater.LastCheckUri?.OriginalString ?? string.Empty,
                Version = _updater.LastCheckVersion?.ToString() ?? string.Empty,
                Time = Timestamp.FromDateTime(_updater.LastCheckTime.ToUniversalTime()),
            });
        }

        public override async Task SubscribeUpdateChecked(Empty _, IServerStreamWriter<Empty> responseStream, ServerCallContext context)
        {
            try
            {
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    _updater.UpdateChecked += Updater_UpdateChecked;
                    void Updater_UpdateChecked(object? sender, AppUpdaterEventArgs e)
                    {
                        _updater.UpdateChecked -= Updater_UpdateChecked;
                        tcs.TrySetResult(true);
                    }
                    using var item = context.CancellationToken.Register(() => { tcs.TrySetResult(false); });
                    await tcs.Task;

                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        _updater.UpdateChecked -= Updater_UpdateChecked;
                        break;
                    }

                    await responseStream.WriteAsync(new Empty());
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }

        private void Download_DownloadStarted(object? sender, DownloadEventArgs e)
        {
            _totalSize = e.TotalSize;
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IAppUpdaterService _updater = updater;
        private double _totalSize;
    }
}
