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

        public async Task<Grpc_WpMetaData> GetWallpaperAsync(string folderPath) {
            Grpc_WpMetaData grpc_data = await _client.GetWallpaperAsync(
                new Grpc_GetWallpaperRequest() { FolderPath = folderPath });

            return grpc_data;
        }

        public async Task<bool> PreviewWallpaperAsync(IWpMetadata data) {
            Grpc_WpPlayerData wpPlayerdata = DataAssist.MetadataToGrpcPlayerData(data);
            //Grpc_WpPlayerData wpPlayerdata = new() {
            //    WallpaperUid = data.BasicData.WallpaperUid,
            //    RType = (Grpc_RuntimeType)data.RuntimeData.RType,
            //    FilePath = data.BasicData.FilePath,
            //    FolderPath = data.BasicData.FolderPath,
            //    ThumbnailPath = data.BasicData.ThumbnailPath,
            //    WpEffectFilePathTemplate = data.RuntimeData.WpEffectFilePathTemplate,
            //    WpEffectFilePathTemporary = data.RuntimeData.WpEffectFilePathTemporary, // control
            //    WpEffectFilePathUsing = data.RuntimeData.WpEffectFilePathUsing,
            //};

            var response = await _client.PreviewWallpaperAsync(
                new Grpc_PreviewWallpaperRequest() {
                    WpPlayerData = wpPlayerdata,
                });

            return response.IsStarted;
        }

        //public async Task PreviewWallpaperAsync(string monitorContent) {
        //    await _client.PreviewWallpaperAsync(
        //        new Grpc_PreviewWallpaperRequest() {
        //            MonitorContet = monitorContent,
        //            IsCurrentWp = true,
        //        });
        //}

        public async Task<Grpc_RestartWallpaperResponse> RestartAllWallpapersAsync() {
            Grpc_RestartWallpaperResponse response = await _client.RestartAllWallpapersAsync(new Empty());

            return response;
        }

        public async Task<Grpc_SetWallpaperResponse> SetWallpaperAsync(
            IMonitor monitor, IWpMetadata metaData, CancellationToken token) {
            var request = new Grpc_SetWallpaperRequest {
                FolderPath = metaData.BasicData.FolderPath,
                MonitorId = monitor.DeviceId,
            };

            Grpc_SetWallpaperResponse response = await _client.SetWallpaperAsync(request, cancellationToken: token);

            return response;
        }

        public async Task UpdateWallpaperAsync(
            IMonitor monitor, IWpMetadata data, CancellationToken token) {
            Grpc_WpPlayerData grpc_data = new() {
                RType = (Grpc_RuntimeType)data.RuntimeData.RType,
                FolderPath = data.BasicData.FolderPath,
                FilePath = data.BasicData.FilePath,
                WpEffectFilePathUsing = data.RuntimeData.WpEffectFilePathUsing,
            };

            await _client.UpdateWallpaperAsync(
                new Grpc_UpdateWpRequest() {
                    WpPlayerData = grpc_data,
                    MonitorId = monitor.DeviceId,
                }, cancellationToken: token);
        }
        #endregion

        #region data
        public async Task<Grpc_WpBasicData?> CreateBasicDataAsync(
            string folderPath,
            string filePath,
            FileType ftype,
            CancellationToken token) {
            Grpc_WpBasicData grpc_data = await _client.CreateMetadataBasicAsync(
                new Grpc_CreateMetadataBasicRequest() {
                    FolderPath = folderPath,
                    FilePath = filePath,
                    FType = (Grpc_FileType)ftype
                },
                null,
                null,
                token);

            return grpc_data;
        }

        public async Task<Grpc_WpRuntimeData?> CreateRuntimeDataAsync(
            string filePath,
            string folderPath,
            RuntimeType rtype,
            CancellationToken token) {
            Grpc_WpRuntimeData grpc_data = await _client.CreateMetadataRuntimeAsync(
                new Grpc_CreateMetadataRuntimeRequest() {
                    FilePath = filePath,
                    FolderPath = folderPath,
                    RType = (Grpc_RuntimeType)rtype,
                },
                cancellationToken: token);

            return grpc_data;
        }

        public async Task<string?> CreateRuntimeDataUsingAsync(
            string folderPath,
            string wpEffectFilePathTemplate,
            string monitorContent,
            CancellationToken token = default) {
            Grpc_StringValue res = await _client.CreateMetadataRuntimeUsingAsync(
                new Grpc_CreateMetadataRuntimeRequest() {
                    FolderPath = folderPath,
                    WpEffectFilePathTemplate = wpEffectFilePathTemplate,
                    MonitorContent = monitorContent,
                },
                cancellationToken: token);

            return res.Value;
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

        public async Task<Grpc_WpMetaData?> UpdateFileDataAsync(IWpMetadata data, CancellationToken token) {
            Grpc_WpBasicData? basciData = await _client.UpdateBasicDataAsync(
                new Grpc_UpdateBasicDataRequest() {
                    FilePath = data.BasicData.FilePath,
                    FolderPath = data.BasicData.FolderPath,
                    FolderName = data.BasicData.FolderName,
                    FType = (Grpc_FileType)data.BasicData.FType,
                },
                cancellationToken: token);
            if (basciData == null) return null;
            basciData.WallpaperUid = data.BasicData.WallpaperUid;

            Grpc_WpRuntimeData? runtimeData = await _client.UpdateRuntimeDataAsync(
                new Grpc_UpdateRuntimeDataRequest() {
                    FolderPath = data.BasicData.FolderPath,
                    RType = (Grpc_RuntimeType)data.RuntimeData.RType,
                },
                cancellationToken: token);
            if (runtimeData == null) return null;
            runtimeData.WpEffectFilePathUsing = data.RuntimeData.WpEffectFilePathUsing;

            Grpc_WpMetaData grpc_data = new() {
                WpBasicData = basciData,
                WpRuntimeData = runtimeData
            };

            return grpc_data;
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
