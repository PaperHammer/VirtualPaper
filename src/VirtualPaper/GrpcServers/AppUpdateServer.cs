using System.Windows.Threading;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Cores.AppUpdate.Models;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Grpc.Service.Update;
using VirtualPaper.Models.Events;

namespace VirtualPaper.GrpcServers {
    public class AppUpdateServer(
        IAppUpdaterService updater) : Grpc_UpdateService.Grpc_UpdateServiceBase {
        public override async Task<Empty> CheckUpdate(Empty _, ServerCallContext context) {
            await _updater.CheckUpdateAsync(0);

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
            var release = _updater.LastReleaseInfo;
            return Task.FromResult(new Grpc_UpdateResponse() {
                Status = (Grpc_UpdateStatus)((int)_updater.Status),
                Changelog = release?.Changelog ?? string.Empty,
                InstallerUri = release?.InstallerUri?.OriginalString ?? string.Empty,
                InstallerShaUri = release?.InstallerShaUri?.OriginalString ?? string.Empty,
                Version = release?.Version?.ToString() ?? string.Empty,
                AppBuild = release?.AppBuild ?? string.Empty,
                CheckedTime = Timestamp.FromDateTime(release?.CheckedTime.ToUniversalTime() ?? DateTime.UtcNow),
                PluginPatchUri = release?.PluginPatchUri?.OriginalString ?? string.Empty,
                PluginPatchSha256Uri = release?.PluginPatchSha256Uri?.OriginalString ?? string.Empty,
                AppCompManifest = release?.AppCompManifest != null
                    ? System.Text.Json.JsonSerializer.Serialize(release.AppCompManifest, UpdateManifestContext.Default.AppCompManifest)
                    : string.Empty,
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
