using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcDotNetNamedPipes;
using NLog;
using System.Collections.ObjectModel;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;
using static VirtualPaper.Common.Errors;
using Monitor = VirtualPaper.Models.Cores.Monitor;

namespace VirtualPaper.Grpc.Client
{
    public class WallpaperControlClient : IWallpaperControlClient
    {
        public ReadOnlyCollection<WallpaperBasicData> Wallpapers => _wallpapers.AsReadOnly();

        public string BaseDirectory { get; private set; } = string.Empty;

        public Version AssemblyVersion { get; private set; }

        public event EventHandler? WallpaperChanged;
        public event EventHandler<Exception>? WallpaperError;

        public WallpaperControlClient()
        {
            _client = new WallpaperControlService.WallpaperControlServiceClient(new NamedPipeChannel(".", Constants.SingleInstance.GrpcPipeServerName));
            
            //TODO: Wait timeout
            Task.Run(async () =>
            {
                _wallpapers.AddRange(await GetWallpapers().ConfigureAwait(false));
                var status = (await GetCoreStats().ConfigureAwait(false));
                BaseDirectory = status.BaseDirectory;
                AssemblyVersion = new Version(status.AssemblyVersion);
            }).Wait();

            _cancellationTokenWallpaperChanged = new CancellationTokenSource();
            _wallpaperChangedTask = Task.Run(() => SubscribeWallpaperChangedStream(_cancellationTokenWallpaperChanged.Token));

            _cancellationTokenWallpaperError = new CancellationTokenSource();
            _wallpaperErrorTask = Task.Run(() => SubscribeWallpaperErrorStream(_cancellationTokenWallpaperError.Token));
        }

        private async Task<GetCoreStatsResponse> GetCoreStats() => await _client.GetCoreStatsAsync(new Empty());

        public async Task CloseAllWallpapersAsync()
        {
            await _client.CloseAllWallpapersAsync(new());
        }

        public async Task CloseWallpaperAsync(IMonitor monitor)
        {
            await _client.CloseWallpaperMonitorAsync(new CloseWallpaperMonitorRequest()
            {
                MonitorId = monitor.DeviceId,
            });
        }

        public async Task PreviewWallpaperAsync(IMetaData metaData, bool isLibraryPreview)
        {
            WpMetaData wpData = new()
            {
                Type = (Service.WallpaperControl.WallpaperType)metaData.Type,
                FolderPath = metaData.FolderPath,
                FilePath = metaData.FilePath,
                WpCustomizePath = metaData.WpCustomizePath, // library
                WpCustomizePathTmp = metaData.WpCustomizePathTmp, // control
                WpCustomizePathUsing = metaData.WpCustomizePathUsing,
            };

            await _client.PreviewWallpaperAsync(
                new PreviewWallpaperRequest()
                {
                    WpMetaData = wpData,
                    IsLibraryPreview = isLibraryPreview
                });
        }

        public async Task ModifyPreviewAsync(string controlName, string propertyName, string val)
        {
            ModifyPreviewRequest modifyPreviewRequest = new()
            {
                ControlName = controlName,
                PropertyName = propertyName,
                Value = val
            };

            await _client.ModifyPreviewAsync(modifyPreviewRequest);
        }

        public async Task<UpdateWpResponse> UpdateWpAsync(
            IMonitor monitor, IMetaData metaData, CancellationToken token)
        {
            WpMetaData wpData = new()
            {
                Type = (Service.WallpaperControl.WallpaperType)metaData.Type,
                FolderPath = metaData.FolderPath,
                FilePath = metaData.FilePath,
                WpCustomizePathUsing = metaData.WpCustomizePathUsing,
            };

            return await _client.UpdateWpAsync(
                new UpdateWpRequest()
                {
                    WpMetaData = wpData,
                    MonitorId = monitor.DeviceId,
                }, cancellationToken: token);
        }

        public async Task<WpMetaData> GetWallpaperAsync(string folderPath)
        {
            return await _client.GetWallpaperAsync(
                new GetWallpaperRequest() { FolderPath = folderPath });
        }

        public async Task<SetWallpaperResponse> SetWallpaperAsync(
            IMonitor monitor, IMetaData metaData, CancellationToken token)
        {
            var request = new SetWallpaperRequest
            {
                FolderPath = metaData.FolderPath,
                MonitorId = monitor.DeviceId,
                RunningState = (RunningState)(int)metaData.State,
            };
            return await _client.SetWallpaperAsync(request, cancellationToken: token);
        }

        public async Task<RestartWallpaperResponse> RestartAllWallpaperAsync()
        {
            return await _client.RestartAllWallpaperAsync(new Empty());
        }

        public async Task<WpMetaData?> CreateWallpaperAsync(string folderPath, string filePath, Common.WallpaperType type, CancellationToken token)
        {
            try
            {
                return await _client.CreateWallpaperAsync(
                    new CreateWallpaperRequest() { FolderPath = folderPath, FilePath = filePath, Type = (Service.WallpaperControl.WallpaperType)type },
                    null,
                    null,
                    token);
            }
            catch (RpcException ex)
            {
                if (ex.StatusCode == StatusCode.Cancelled)
                    throw new OperationCanceledException();
            }

            return null;
        }

        public async Task ResetWpCustomizeAsync(string wpCustomizePathTmp, Service.WallpaperControl.WallpaperType type)
        {
            await _client.ResetWpCustomizeAsync(new()
            {
                WpCustomizePath = wpCustomizePathTmp,
                Type = type
            });
        }

        public async Task ChangeWallpaperLayoutFolrderPathAsync(string previousDir, string newDir)
        {
            var request = new ChangePathRequest()
            {
                PreviousDir = previousDir,
                NewDir = newDir,
            };
            await _client.ChangeWallpaperLayoutFolrderPathAsync(request);
        }

        //public async Task SendMessageWallpaperAsync(IMetaData metaData, IpcMessage msg)
        //{
        //    await _client.SendMessageWallpaperAsync(new WallpaperMessageRequest()
        //    {
        //        MonitorId = string.Empty,
        //        FolderPath = metaData.FolderPath,
        //        Msg = JsonUtil.Serialize(msg),
        //    });
        //}

        public async Task SendMessageWallpaperAsync(IMonitor monitor, IMetaData metaData, IpcMessage msg)
        {
            await _client.SendMessageWallpaperAsync(new WallpaperMessageRequest()
            {
                MonitorId = monitor.DeviceId,
                FolderPath = metaData.FolderPath,
                Msg = JsonUtil.Serialize(msg),
            });
        }

        public async Task TakeScreenshotAsync(string monitorId, string savePath)
        {
            await _client.TakeScreenshotAsync(new WallpaperScreenshotRequest()
            {
                MonitorId = monitorId,
                SavePath = savePath,
            });
        }

        private async Task<List<WallpaperBasicData>> GetWallpapers()
        {
            var resp = new List<GetWallpapersResponse>();
            using var call = _client.GetWallpapers(new Empty());
            while (await call.ResponseStream.MoveNext())
            {
                var response = call.ResponseStream.Current;
                resp.Add(response);
            }

            var wallpapers = new List<WallpaperBasicData>();
            foreach (var item in resp)
            {
                wallpapers.Add(new WallpaperBasicData()
                {
                    VirtualPaperUid = item.VirtualPaperUid,
                    FolderPath = item.FolderPath,
                    ThumbnailPath = item.ThumbnailPath,
                    WpCustomizePathUsing = item.WpCustomizePathUsing,
                    Tyep = (Common.WallpaperType)(int)item.Type,
                    Monitor = new Monitor()
                    {
                        DeviceId = item.Monitor.DeviceId,
                        MonitorName = item.Monitor.DisplayName,
                        DeviceName = item.Monitor.DeviceName,
                        HMonitor = new IntPtr(item.Monitor.HMonitor),
                        IsPrimary = item.Monitor.IsPrimary,
                        Content = item.Monitor.Content,
                        Bounds = new System.Drawing.Rectangle(
                        item.Monitor.Bounds.X,
                        item.Monitor.Bounds.Y,
                        item.Monitor.Bounds.Width,
                        item.Monitor.Bounds.Height),
                        WorkingArea = new System.Drawing.Rectangle(
                        item.Monitor.WorkingArea.X,
                        item.Monitor.WorkingArea.Y,
                        item.Monitor.WorkingArea.Width,
                        item.Monitor.WorkingArea.Height),

                    },
                });
            }

            return wallpapers;
        }

        private async Task SubscribeWallpaperChangedStream(CancellationToken token)
        {
            try
            {
                using var call = _client.SubscribeWallpaperChanged(new Empty());
                while (await call.ResponseStream.MoveNext(token))
                {
                    await _wallpaperChangedLock.WaitAsync(token);
                    try
                    {
                        var response = call.ResponseStream.Current;

                        _wallpapers.Clear();
                        _wallpapers.AddRange(await GetWallpapers());
                        WallpaperChanged?.Invoke(this, EventArgs.Empty);
                    }
                    finally
                    {
                        _wallpaperChangedLock.Release();
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }

        private async Task SubscribeWallpaperErrorStream(CancellationToken token)
        {
            try
            {
                using var call = _client.SubscribeWallpaperError(new Empty());
                while (await call.ResponseStream.MoveNext(token))
                {
                    var response = call.ResponseStream.Current;

                    var exp = response.Error switch
                    {
                        ErrorType.Workerw => new WorkerWException(response.ErrorMsg),
                        ErrorType.WallpaperNotFound => new WallpaperNotFoundException(response.ErrorMsg),
                        ErrorType.WallpaperNotAllowed => new WallpaperNotAllowedException(response.ErrorMsg),
                        ErrorType.WallpaperPluginNotFound => new WallpaperPluginNotFoundException(response.ErrorMsg),
                        ErrorType.WallpaperPluginFail => new WallpaperPluginException(response.ErrorMsg),
                        ErrorType.WallpaperPluginMediaCodecMissing => new WallpaperPluginMediaCodecException(response.ErrorMsg),
                        ErrorType.ScreenNotFound => new ScreenNotFoundException(response.ErrorMsg),
                        _ => new Exception("Unhandled Error"),
                    };
                    WallpaperError?.Invoke(this, exp);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }        
        }

        #region dispose
        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cancellationTokenWallpaperChanged?.Cancel();
                    _cancellationTokenWallpaperError?.Cancel();
                    Task.WaitAll(_wallpaperChangedTask, _wallpaperErrorTask);
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly List<WallpaperBasicData> _wallpapers = [];
        private readonly WallpaperControlService.WallpaperControlServiceClient _client;
        private readonly SemaphoreSlim _wallpaperChangedLock = new(1, 1);
        private readonly CancellationTokenSource _cancellationTokenWallpaperChanged, _cancellationTokenWallpaperError;
        private readonly Task _wallpaperChangedTask, _wallpaperErrorTask;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
