using System.Windows.Threading;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Grpc.Service.Update;
using VirtualPaper.Models.Events;

namespace VirtualPaper.GrpcServers {
    public class AppUpdateServer(
        IAppUpdaterService updater) : Grpc_UpdateService.Grpc_UpdateServiceBase {
        public override async Task<Empty> CheckUpdate(Empty _, ServerCallContext context) {
            await _updater.CheckUpdate(0);

            return await Task.FromResult(new Empty());
        }

        public override Task<Empty> StartDownload(Empty _, ServerCallContext context) {
            if (_updater.Status == AppUpdateStatus.Available) {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new ThreadStart(delegate {
                    App.AppUpdateDialog(new AppUpdaterEventArgs(_updater.Status, _updater.LastReleaseInfo));
                }));
            }

            return Task.FromResult(new Empty());
        }

        public override Task<Grpc_UpdateResponse> GetUpdateStatus(Empty _, ServerCallContext context) {
            return Task.FromResult(new Grpc_UpdateResponse() {
                Status = (Grpc_UpdateStatus)((int)_updater.Status),               
                Changelog = _updater.LastReleaseInfo?.Changelog ?? string.Empty,
                Uri = _updater.LastReleaseInfo?.InstallerUri?.OriginalString ?? string.Empty,
                ShaUri = _updater.LastReleaseInfo?.InstallerShaUri?.OriginalString ?? string.Empty,
                Version = _updater.LastReleaseInfo?.Version?.ToString() ?? string.Empty,
                AppBuild = _updater.LastReleaseInfo?.AppBuild ?? string.Empty,
                Time = Timestamp.FromDateTime(_updater.LastReleaseInfo?.CheckedTime.ToUniversalTime() ?? DateTime.UtcNow),
            });
        }

        public override async Task SubscribeUpdateChecked(Empty _, IServerStreamWriter<Empty> responseStream, ServerCallContext context) {
            try {
                while (!context.CancellationToken.IsCancellationRequested) {
                    var tcs = new TaskCompletionSource<bool>();
                    _updater.UpdateChecked += Updater_UpdateChecked;
                    void Updater_UpdateChecked(object? sender, AppUpdaterEventArgs e) {
                        _updater.UpdateChecked -= Updater_UpdateChecked;
                        tcs.TrySetResult(true);
                    }
                    using var item = context.CancellationToken.Register(() => { tcs.TrySetResult(false); });
                    await tcs.Task;

                    if (context.CancellationToken.IsCancellationRequested) {
                        _updater.UpdateChecked -= Updater_UpdateChecked;
                        break;
                    }

                    await responseStream.WriteAsync(new Empty());
                }
            }
            catch (Exception e) {
                App.Log.Error(e);
            }
        }

        private readonly IAppUpdaterService _updater = updater;
    }
}
