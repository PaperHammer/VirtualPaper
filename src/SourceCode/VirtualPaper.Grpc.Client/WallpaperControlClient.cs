using System.Collections.ObjectModel;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcDotNetNamedPipes;
using NLog;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using static VirtualPaper.Common.Errors;

namespace VirtualPaper.Grpc.Client {
    public partial class WallpaperControlClient : IWallpaperControlClient {
        public event EventHandler? WallpaperChanged;
        public event EventHandler<Exception>? WallpaperError;

        public Version AssemblyVersion { get; private set; }
        public string BaseDirectory { get; private set; } = string.Empty;
        public ReadOnlyCollection<IWpMetadata> Wallpapers => _wallpapers.AsReadOnly();

        public WallpaperControlClient() {
            _client = new Grpc_WallpaperControlService.Grpc_WallpaperControlServiceClient(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));

            Task.Run(async () => {
                _wallpapers.AddRange(await GetWallpapersAsync().ConfigureAwait(false));
                var status = await GetCoreStats().ConfigureAwait(false);
                BaseDirectory = status.BaseDirectory;
                AssemblyVersion = new Version(status.AssemblyVersion);
            }).Wait();

            _ctsWallpaperChanged = new CancellationTokenSource();
            _wallpaperChangedTask = Task.Run(() => SubscribeWallpaperChangedStream(_ctsWallpaperChanged.Token));

            _ctsWallpaperError = new CancellationTokenSource();
            _wallpaperErrorTask = Task.Run(() => SubscribeWallpaperErrorStream(_ctsWallpaperError.Token));
        }

        #region wallpaper actions
        public async Task CloseAllWallpapersAsync() {
            await _client.CloseAllWallpapersAsync(new Empty());
        }

        public async Task CloseWallpaperAsync(IMonitor monitor) {
            await _client.CloseWallpaperByMonitorAsync(new Grpc_CloseWallpaperByMonitorRequest() {
                MonitorId = monitor.DeviceId,
            });
        }

        public async Task CloseAllPreviewAsync() {
            await _client.CloseAllPreviewAsync(new Empty());
        }

        public async Task<Grpc_WpMetaData> GetWallpaperAsync(string folderPath,string monitorContent, string rtype) {
            Grpc_WpMetaData grpc_data = await _client.GetWallpaperAsync(
                new Grpc_GetWallpaperRequest() { 
                    FolderPath = folderPath, 
                    MonitorContent = monitorContent, 
                    RType = rtype });

            return grpc_data;
        }

        public async Task<bool> AdjustWallpaperAsync(string monitorDeviceId, CancellationToken token) {
            var response = await _client.AdjustWallpaperAsync(
                new Grpc_AdjustWallpaperRequest() {
                    MonitorDeviceId = monitorDeviceId,
                },
                cancellationToken: token);

            return response.IsOk;
        }

        public async Task<bool> PreviewWallpaperAsync(string monitorDeviceId, CancellationToken token) {
            var response = await _client.PreviewWallpaperAsync(
                new Grpc_PreviewWallpaperRequest() {
                    MonitorDeviceId = monitorDeviceId,
                },
                cancellationToken: token);

            return response.IsOk;
        }

        public async Task<bool> PreviewWallpaperAsync(IWpBasicData data, RuntimeType rtype, CancellationToken token) {
            Grpc_WpPlayerData wpPlayerdata = DataAssist.MetadataToGrpcPlayingData(data, rtype);

            var response = await _client.PreviewWallpaperAsync(
                new Grpc_PreviewWallpaperRequest() {
                    WpPlayerData = wpPlayerdata,
                },
                cancellationToken: token);

            return response.IsOk;
        }

        public async Task<Grpc_RestartWallpaperResponse> RestartAllWallpapersAsync() {
            Grpc_RestartWallpaperResponse response = await _client.RestartAllWallpapersAsync(new Empty());

            return response;
        }

        public async Task<Grpc_SetWallpaperResponse> SetWallpaperAsync(
            IMonitor monitor, IWpBasicData data, RuntimeType rtype, CancellationToken token) {
            Grpc_WpPlayerData wpPlayerdata = DataAssist.MetadataToGrpcPlayingData(data, rtype);

            var request = new Grpc_SetWallpaperRequest {
                WpPlayerData = wpPlayerdata,
                MonitorId = monitor.DeviceId,
            };

            Grpc_SetWallpaperResponse response = await _client.SetWallpaperAsync(request, cancellationToken: token);

            return response;
        }
        #endregion

        #region data
        public async Task<Grpc_WpBasicData?> CreateBasicDataAsync(
            string filePath,
            FileType ftype,
            CancellationToken token) {
            Grpc_WpBasicData grpc_data = await _client.CreateMetadataBasicAsync(
                new Grpc_CreateMetadataBasicRequest() {
                    FilePath = filePath,
                    FType = (Grpc_FileType)ftype
                },
                cancellationToken: token);

            return grpc_data;
        }
        public IWpMetadata GetWpMetadataByMonitorThu(string thumbnailPath) {
            return _wallpapers.Find(x => x.BasicData.ThumbnailPath == thumbnailPath)!;
        }
        #endregion

        #region utils
        public async Task ChangeWallpaperLayoutFolrderPathAsync(string previousDir, string newDir) {
            Grpc_ChangePathRequest request = new() {
                PreviousDir = previousDir,
                NewDir = newDir,
            };

            await _client.ChangeWallpaperLayoutFolrderPathAsync(request);
        }

        public async Task<Grpc_MonitorData?> GetRunMonitorByWallpaperAsync(string wpUid) {
            Grpc_GetMonitorRequest request = new() {
                WpUid = wpUid,
            };
            Grpc_MonitorData? monitor_data = await _client.GetRunMonitorByWallpaperAsync(request);

            return monitor_data;
        }

        public async Task ModifyPreviewAsync(string controlName, string propertyName, string val) {
            Grpc_ModifyPreviewRequest modifyPreviewRequest = new() {
                ControlName = controlName,
                PropertyName = propertyName,
                Value = val
            };

            await _client.ModifyPreviewAsync(modifyPreviewRequest);
        }

        public async Task SendMessageWallpaperAsync(IMonitor monitor, IWpRuntimeData metaData, IpcMessage msg) {
            await _client.SendMessageWallpaperAsync(new Grpc_WallpaperMessageRequest() {
                MonitorId = monitor.DeviceId,
                FolderPath = metaData.FolderPath,
                Msg = JsonSerializer.Serialize(msg),
            });
        }

        public async Task TakeScreenshotAsync(string monitorId, string savePath) {
            await _client.TakeScreenshotAsync(new Grpc_WallpaperScreenshotRequest() {
                MonitorId = monitorId,
                SavePath = savePath,
            });
        }

        public async Task<Grpc_WpBasicData?> UpdateBasicDataAsync(IWpBasicData data, CancellationToken token) {
            Grpc_WpBasicData? grpc_basicData = await _client.UpdateBasicDataAsync(
                new Grpc_UpdateBasicDataRequest() {
                    FilePath = data.FilePath,
                    FolderPath = data.FolderPath,
                    FolderName = data.FolderName,
                    FType = (Grpc_FileType)data.FType,
                },
                cancellationToken: token);
            if (grpc_basicData == null) return null;
            grpc_basicData.WallpaperUid = data.WallpaperUid;

            return grpc_basicData;
        }
        #endregion

        #region private utils
        private async Task<List<IWpMetadata>> GetWallpapersAsync() {
            var wallpapers = new List<IWpMetadata>();
            using var call = _client.GetWallpapers(new Empty());
            while (await call.ResponseStream.MoveNext()) {
                var response = call.ResponseStream.Current;

                wallpapers.Add(new WpMetadata() {
                    BasicData = DataAssist.GrpcToBasicData(response.WpBasicData),
                    RuntimeData = DataAssist.GrpcToRuntimeData(response.WpRuntimeData),
                });
            }

            return wallpapers;
        }

        private async Task SubscribeWallpaperChangedStream(CancellationToken token) {
            try {
                using var call = _client.SubscribeWallpaperChanged(new Empty(), cancellationToken: token);
                while (await call.ResponseStream.MoveNext(token)) {
                    await _wallpaperChangedLock.WaitAsync(token);
                    try {
                        _ = call.ResponseStream.Current;

                        _wallpapers.Clear();
                        _wallpapers.AddRange(await GetWallpapersAsync());
                        WallpaperChanged?.Invoke(this, EventArgs.Empty);
                    }
                    finally {
                        _wallpaperChangedLock.Release();
                    }
                }
            }
            catch (Exception e) {
                _logger.Error(e);
            }
        }

        private async Task SubscribeWallpaperErrorStream(CancellationToken token) {
            try {
                using var call = _client.SubscribeWallpaperError(new Empty(), cancellationToken: token);
                while (await call.ResponseStream.MoveNext(token)) {
                    var response = call.ResponseStream.Current;

                    var exp = response.Error switch {
                        Grpc_ErrorType.Workerw => new WorkerWException(response.ErrorMsg),
                        Grpc_ErrorType.WallpaperNotFound => new WallpaperNotFoundException(response.ErrorMsg),
                        Grpc_ErrorType.WallpaperNotAllowed => new WallpaperNotAllowedException(response.ErrorMsg),
                        Grpc_ErrorType.WallpaperPluginNotFound => new WallpaperPluginNotFoundException(response.ErrorMsg),
                        Grpc_ErrorType.WallpaperPluginFail => new WallpaperPluginException(response.ErrorMsg),
                        Grpc_ErrorType.WallpaperPluginMediaCodecMissing => new WallpaperPluginMediaCodecException(response.ErrorMsg),
                        Grpc_ErrorType.ScreenNotFound => new ScreenNotFoundException(response.ErrorMsg),
                        _ => new Exception("Unhandled Error"),
                    };
                    WallpaperError?.Invoke(this, exp);
                }
            }
            catch (Exception ex) {
                _logger.Error(ex);
            }
        }

        private async Task<Grpc_GetCoreStatsResponse> GetCoreStats() {
            Grpc_GetCoreStatsResponse response = await _client.GetCoreStatsAsync(new Empty());

            return response;
        }
        #endregion

        #region dispose
        private bool _disposed;
        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                if (disposing) {
                    _ctsWallpaperChanged?.Cancel();
                    _ctsWallpaperError?.Cancel();
                    Task.WaitAll(_wallpaperChangedTask, _wallpaperErrorTask);
                }
                _disposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly List<IWpMetadata> _wallpapers = [];
        private readonly Grpc_WallpaperControlService.Grpc_WallpaperControlServiceClient _client;
        private readonly SemaphoreSlim _wallpaperChangedLock = new(1, 1);
        private readonly CancellationTokenSource _ctsWallpaperChanged, _ctsWallpaperError;
        private readonly Task _wallpaperChangedTask, _wallpaperErrorTask;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
