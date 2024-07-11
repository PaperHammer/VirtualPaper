using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Newtonsoft.Json;
using NLog;
using System.Diagnostics;
using System.Reflection;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils;
using static VirtualPaper.Common.Errors;

namespace VirtualPaper.GrpcServers
{
    internal class WallpaperControlServer(
        IWallpaperControl desktopWpControl,
        IMonitorManager monitorManager,
        IUserSettingsService userSetting) : WallpaperControlService.WallpaperControlServiceBase
    {
        public override Task<GetCoreStatsResponse> GetCoreStats(Empty _, ServerCallContext context)
        {
            return Task.FromResult(new GetCoreStatsResponse()
            {
                BaseDirectory = AppDomain.CurrentDomain.BaseDirectory,
                AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
            });
        }

        public override Task<Empty> CloseAllWallpapers(Empty request, ServerCallContext context)
        {
            _wpControl.CloseAllWallpapers();
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> CloseWallpaperMonitor(CloseWallpaperMonitorRequest request, ServerCallContext context)
        {
            var monitor = _monitorManager.Monitors.FirstOrDefault(x => x.DeviceId == request.MonitorId);
            if (monitor != null)
            {
                _wpControl.CloseWallpaper(monitor);
            }
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> PreviewWallpaper(PreviewWallpaperRequest request, ServerCallContext context)
        {
            try
            {
                var wpData = request.WpMetaData;
                bool isLibraryPreview = request.IsLibraryPreview;
                IMetaData metaData = new MetaData()
                {
                    Type = (Common.WallpaperType)wpData.Type,
                    FolderPath = wpData.FolderPath,
                    FilePath = wpData.FilePath,
                    WpCustomizePathTmp = wpData.WpCustomizePathTmp
                };
                _wpControl.PreviewWallpaper(metaData, isLibraryPreview);
            }
            catch (Exception e)
            {
                _logger.Error(e.ToString());
            }

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> ModifyPreview(ModifyPreviewRequest request, ServerCallContext context)
        {
            try
            {
                _wpControl.ModifyPreview(request.ControlName, request.PropertyName, request.Value);
            }
            catch (Exception e)
            {
                _logger.Error(e.ToString());
            }

            return Task.FromResult(new Empty());
        }

        public override async Task<UpdateWpResponse> UpdateWp(UpdateWpRequest request, ServerCallContext context)
        {
            UpdateWpResponse response = new();

            try
            {
                WpMetaData wpData = request.WpMetaData;
                IMetaData metaData = new MetaData()
                {
                    Type = (Common.WallpaperType)wpData.Type,
                    FolderPath = wpData.FolderPath,
                    FilePath = wpData.FilePath,
                    WpCustomizePathUsing = wpData.WpCustomizePathUsing,
                };

                response = _wpControl.UpdateWp(
                    request.MonitorId, metaData, context.CancellationToken);
            }
            catch (Exception e)
            {
                _logger.Error(e.ToString());
            }
            
            return await Task.FromResult(response);
        }

        public override Task<RestartWallpaperResponse> RestartAllWallpaper(Empty request, ServerCallContext context)
        {
            RestartWallpaperResponse response = new();

            try
            {
                response = _wpControl.RestoreWallpaper();
            }
            catch (Exception e)
            {
                _logger.Error(e.ToString());
            }

            return Task.FromResult(response);
        }

        public override async Task<SetWallpaperResponse> SetWallpaper(SetWallpaperRequest request, ServerCallContext context)
        {
            SetWallpaperResponse response = new();

            try
            {
                var metaData = WallpaperUtil.ScanWallpaperFolder(request.FolderPath);
                metaData.State = (MetaData.RunningState)(int)request.RunningState;
                var monitor = _monitorManager.Monitors.FirstOrDefault(x => x.DeviceId == request.MonitorId);

                response = await _wpControl.SetWallpaperAsync(
                    metaData,
                    monitor ?? _monitorManager.PrimaryMonitor,
                    context.CancellationToken);
            }
            catch (Exception e)
            {
                _logger.Error(e.ToString());
            }

            return await Task.FromResult(response);
        }

        public override Task<Empty> SendMessageWallpaper(WallpaperMessageRequest request, ServerCallContext context)
        {
            var ipcMsg = JsonConvert.DeserializeObject<IpcMessage>(request.Msg, new JsonSerializerSettings() { Converters = { new IpcMessageConverter() } });

            var monitor = _monitorManager.Monitors.FirstOrDefault(x => x.DeviceId == request.MonitorId);
            _wpControl.SendMessageWallpaper(monitor, request.FolderPath, ipcMsg);

            return Task.FromResult(new Empty());
        }

        public override async Task<Empty> TakeScreenshot(WallpaperScreenshotRequest request, ServerCallContext context)
        {
            try
            {
                switch (_userSetting.Settings.WallpaperArrangement)
                {
                    case WallpaperArrangement.Per:
                        {
                            var wallpaper = _wpControl.Wallpapers.FirstOrDefault(x => request.MonitorId == x.Monitor.DeviceId);
                            if (wallpaper is not null)
                            {
                                await wallpaper.ScreenCapture(request.SavePath);
                            }
                        }
                        break;
                    case WallpaperArrangement.Expand:
                    case WallpaperArrangement.Duplicate:
                        if (_wpControl.Wallpapers.Any())
                        {
                            await _wpControl.Wallpapers[0].ScreenCapture(request.SavePath);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
            return await Task.FromResult(new Empty());
        }

        public override async Task GetWallpapers(Empty request, IServerStreamWriter<GetWallpapersResponse> responseStream, ServerCallContext context)
        {
            try
            {
                foreach (var wallpaper in _wpControl.Wallpapers)
                {
                    var item = new GetWallpapersResponse()
                    {
                        VirtualPaperUid = wallpaper.MetaData.VirtualPaperUid,
                        FolderPath = wallpaper.MetaData.FolderPath,
                        Monitor = new MonitorData()
                        {
                            DeviceId = wallpaper.Monitor.DeviceId,
                            DeviceName = wallpaper.Monitor.DeviceName,
                            DisplayName = wallpaper.Monitor.MonitorName,
                            HMonitor = wallpaper.Monitor.HMonitor.ToInt32(),
                            IsPrimary = wallpaper.Monitor.IsPrimary,
                            Content = wallpaper.Monitor.Content,
                            WorkingArea = new Grpc.Service.WallpaperControl.Rectangle()
                            {
                                X = wallpaper.Monitor.WorkingArea.X,
                                Y = wallpaper.Monitor.WorkingArea.Y,
                                Width = wallpaper.Monitor.WorkingArea.Width,
                                Height = wallpaper.Monitor.WorkingArea.Height
                            },
                            Bounds = new Grpc.Service.WallpaperControl.Rectangle()
                            {
                                X = wallpaper.Monitor.Bounds.X,
                                Y = wallpaper.Monitor.Bounds.Y,
                                Width = wallpaper.Monitor.Bounds.Width,
                                Height = wallpaper.Monitor.Bounds.Height
                            }
                        },
                        ThumbnailPath = wallpaper.MetaData.ThumbnailPath ?? string.Empty,
                        WpCustomizePathUsing = wallpaper.MetaData.WpCustomizePathUsing ?? string.Empty,
                        Type = (Grpc.Service.WallpaperControl.WallpaperType)(int)wallpaper.MetaData.Type
                    };
                    await responseStream.WriteAsync(item);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        public override async Task<WpMetaData> GetWallpaper(GetWallpaperRequest request, ServerCallContext context)
        {
            var resp = new WpMetaData();
            try
            {
                IMetaData metaData = _wpControl.GetWallpaper(request.FolderPath);

                resp.VirtualPaperUid = metaData.VirtualPaperUid;
                resp.AppInfo = new()
                {
                    AppName = metaData.AppInfo.AppName,
                    AppVersion = metaData.AppInfo.AppVersion,
                };
                resp.Title = metaData.Title;
                resp.Desc = metaData.Desc;
                resp.Authors = metaData.Authors;
                resp.PublishDate = metaData.PublishDate;
                resp.Rating = metaData.Rating;
                resp.Type = (Grpc.Service.WallpaperControl.WallpaperType)metaData.Type;
                resp.Partition = metaData.Partition;
                resp.Tags = metaData.Tags;
                resp.FolderPath = metaData.FolderPath;
                resp.FilePath = metaData.FilePath;
                resp.ThumbnailPath = metaData.ThumbnailPath;
                resp.WpCustomizePath = metaData.WpCustomizePath;
                resp.WpCustomizePathTmp = metaData.WpCustomizePathTmp;
                resp.WpCustomizePathUsing = metaData.WpCustomizePathUsing;

                resp.Resolution = metaData.Resolution;
                resp.AspectRatio = metaData.AspectRatio;
                resp.FileExtension = metaData.FileExtension;
                resp.FileSize = metaData.FileSize;
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
            return await Task.FromResult(resp);
        }

        public override async Task<WpMetaData> CreateWallpaper(CreateWallpaperRequest request, ServerCallContext context)
        {
            string folderPath = request.FolderPath;
            string filePath = request.FilePath;
            Common.WallpaperType type = (Common.WallpaperType)request.Type;

            var resp = new WpMetaData();
            try
            {
                var token = context.CancellationToken;

                var metaData = _wpControl.CreateWallpaper(folderPath, filePath, type, token);
                token.ThrowIfCancellationRequested(); // 在等待结果前检查取消请求

                resp.Type = (Grpc.Service.WallpaperControl.WallpaperType)metaData.Type;
                resp.FolderPath = metaData.FolderPath;
                resp.FilePath = metaData.FilePath;
                resp.ThumbnailPath = metaData.ThumbnailPath;
                resp.WpCustomizePath = metaData.WpCustomizePath;

                var wpProperty = _wpControl.TryGetProeprtyInfo(filePath, type);
                token.ThrowIfCancellationRequested(); // 在等待结果前检查取消请求

                resp.Resolution = wpProperty.Resolution;
                resp.AspectRatio = wpProperty.AspectRatio;
                resp.FileExtension = wpProperty.FileExtension;
                resp.FileSize = wpProperty.FileSize;
            }
            catch (Exception) { }

            return await Task.FromResult(resp);
        }

        public override Task<Empty> ResetWpCustomize(ResetWpCustomizeRequest request, ServerCallContext context)
        {
            _wpControl.ResetWpCustomize(
                request.WpCustomizePath, 
                (Common.WallpaperType)request.Type);

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> ChangeWallpaperLayoutFolrderPath(ChangePathRequest request, ServerCallContext context)
        {
            _wpControl.ChangeWallpaperLayoutFolrderPath(request.PreviousDir, request.NewDir);

            return Task.FromResult(new Empty());
        }

        public override async Task SubscribeWallpaperChanged(Empty _, IServerStreamWriter<Empty> responseStream, ServerCallContext context)
        {
            try
            {
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    _wpControl.WallpaperChanged += WallpaperChanged;
                    void WallpaperChanged(object? s, EventArgs e)
                    {
                        _wpControl.WallpaperChanged -= WallpaperChanged;
                        tcs.TrySetResult(true);
                    }
                    using var item = context.CancellationToken.Register(() => { tcs.TrySetResult(false); });
                    await tcs.Task;

                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        _wpControl.WallpaperChanged -= WallpaperChanged;
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

        public override async Task SubscribeWallpaperError(Empty _, IServerStreamWriter<WallpaperErrorResponse> responseStream, ServerCallContext context)
        {
            try
            {
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    var resp = new WallpaperErrorResponse();
                    var tcs = new TaskCompletionSource<bool>();
                    _wpControl.WallpaperError += WallpaperError;
                    void WallpaperError(object? s, Exception e)
                    {
                        _wpControl.WallpaperError -= WallpaperError;

                        resp.ErrorMsg = e.Message ?? string.Empty;
                        resp.Error = e switch
                        {
                            WorkerWException _ => ErrorType.Workerw,
                            WallpaperNotAllowedException _ => ErrorType.WallpaperNotAllowed,
                            WallpaperNotFoundException _ => ErrorType.WallpaperNotFound,
                            WallpaperPluginException _ => ErrorType.WallpaperPluginFail,
                            WallpaperPluginNotFoundException _ => ErrorType.WallpaperPluginNotFound,
                            WallpaperPluginMediaCodecException _ => ErrorType.WallpaperPluginMediaCodecMissing,
                            ScreenNotFoundException _ => ErrorType.ScreenNotFound,
                            _ => ErrorType.General,
                        };
                        tcs.TrySetResult(true);
                    }
                    using var item = context.CancellationToken.Register(() => { tcs.TrySetResult(false); });
                    await tcs.Task;

                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        _wpControl.WallpaperError -= WallpaperError;
                        break;
                    }

                    await responseStream.WriteAsync(resp);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IWallpaperControl _wpControl = desktopWpControl;
        private readonly IMonitorManager _monitorManager = monitorManager;
        private readonly IUserSettingsService _userSetting = userSetting;
    }
}
