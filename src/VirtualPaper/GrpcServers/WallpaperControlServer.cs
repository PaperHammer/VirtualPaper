using System.Reflection;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using NLog;
using VirtualPaper.Common;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using static VirtualPaper.Common.Errors;

namespace VirtualPaper.GrpcServers {
    internal class WallpaperControlServer(
        IWallpaperControl desktopWpControl,
        IMonitorManager monitorManager,
        IUserSettingsService userSetting) : Grpc_WallpaperControlService.Grpc_WallpaperControlServiceBase {
        #region wallpaper actions
        public override Task<Empty> CloseAllWallpapers(Empty request, ServerCallContext context) {
            _wpControl.CloseAllWallpapers();

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> CloseWallpaperByMonitor(Grpc_CloseWallpaperByMonitorRequest request, ServerCallContext context) {
            var monitor = _monitorManager.Monitors.FirstOrDefault(x => x.DeviceId == request.MonitorId);
            if (monitor != null) {
                _wpControl.CloseWallpaper(monitor);
            }

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> CloseAllPreview(Empty request, ServerCallContext context) {
            _wpControl.CloseAllPreview();

            return Task.FromResult(new Empty());
        }

        public override async Task<Grpc_WpMetaData> GetWallpaper(Grpc_GetWallpaperRequest request, ServerCallContext context) {
            Grpc_WpMetaData resp = new();
            IWpMetadata data = _wpControl.GetWallpaperByFolderPath(request.FolderPath, request.MonitorContent, request.RType);
            resp.WpBasicData = DataAssist.BasicDataToGrpcData(data.BasicData);
            resp.WpRuntimeData = DataAssist.RuntimeDataToGrpcData(data.RuntimeData);

            return await Task.FromResult(resp);
        }

        public override async Task<Grpc_AdjustWallpaperResponse> AdjustWallpaper(Grpc_AdjustWallpaperRequest request, ServerCallContext context) {
            string monitorDeviceId = request.MonitorDeviceId;
            bool isOk = _wpControl.AdjustWallpaper(monitorDeviceId, context.CancellationToken);

            Grpc_AdjustWallpaperResponse response = new() {
                IsOk = isOk,
            };

            return await Task.FromResult(response);
        }

        public override async Task<Grpc_PreviewWallpaperResponse> PreviewWallpaper(Grpc_PreviewWallpaperRequest request, ServerCallContext context) {
            var playingData = DataAssist.GrpcToPlayerData(request.WpPlayerData);
            bool isOk = await _wpControl.PreviewWallpaperAsync(request.MonitorDeviceId, playingData, context.CancellationToken);
            Grpc_PreviewWallpaperResponse response = new() {
                IsOk = isOk,
            };

            return await Task.FromResult(response);
        }

        public override Task<Grpc_RestartWallpaperResponse> RestartAllWallpapers(Empty request, ServerCallContext context) {
            Grpc_RestartWallpaperResponse response = _wpControl.RestoreWallpaper();

            return Task.FromResult(response);
        }

        public override async Task<Grpc_SetWallpaperResponse> SetWallpaper(Grpc_SetWallpaperRequest request, ServerCallContext context) {
            WpPlayerData wpPlayerData = DataAssist.GrpcToPlayerData(request.WpPlayerData);
            var monitor = _monitorManager.Monitors.FirstOrDefault(x => x.DeviceId == request.MonitorId);

            Grpc_SetWallpaperResponse response = await _wpControl.SetWallpaperAsync(
                wpPlayerData,
                monitor ?? _monitorManager.PrimaryMonitor,
                context.CancellationToken);

            return await Task.FromResult(response);
        }
        #endregion

        #region data
        public override async Task<Grpc_WpBasicData> CreateMetadataBasic(Grpc_CreateMetadataBasicRequest request, ServerCallContext context) {
            var token = context.CancellationToken;
            var data = _wpControl.CreateBasicData(request.FilePath, (FileType)request.FType, token);

            Grpc_WpBasicData grpc_data = DataAssist.BasicDataToGrpcData(data);

            return await Task.FromResult(grpc_data);
        }

        public override async Task<Grpc_WpBasicData> CreateMetadataBasicInMem(Grpc_CreateMetadataBasicRequest request, ServerCallContext context) {
            var token = context.CancellationToken;
            var data = _wpControl.CreateBasicDataInMem(request.FilePath, (FileType)request.FType, token);

            Grpc_WpBasicData grpc_data = DataAssist.BasicDataToGrpcData(data);

            return await Task.FromResult(grpc_data);
        }
        #endregion

        #region utils
        public override Task<Empty> ChangeWallpaperLayoutFolrderPath(Grpc_ChangePathRequest request, ServerCallContext context) {
            _wpControl.ChangeWallpaperLayoutFolrderPath(request.PreviousDir, request.NewDir);

            return Task.FromResult(new Empty());
        }

        public override Task<Grpc_MonitorData> GetRunMonitorByWallpaper(Grpc_GetMonitorRequest request, ServerCallContext context) {
            Grpc_MonitorData grpc_data = _wpControl.GetRunMonitorByWallpaper(request.WpUid);

            return Task.FromResult(grpc_data);
        }

        public override Task<Empty> SendMessageWallpaper(Grpc_WallpaperMessageRequest request, ServerCallContext context) {
            var monitor = _monitorManager.Monitors.FirstOrDefault(x => x.DeviceId == request.MonitorId) ?? _monitorManager.PrimaryMonitor;
            _wpControl.SendMessageWallpaper(monitor, request.FolderPath, request.Msg);

            return Task.FromResult(new Empty());
        }

        public override async Task<Empty> TakeScreenshot(Grpc_WallpaperScreenshotRequest request, ServerCallContext context) {
            switch (_userSetting.Settings.WallpaperArrangement) {
                case WallpaperArrangement.Per: {
                        var wallpaper = _wpControl.Wallpapers.FirstOrDefault(x => request.MonitorId == x.Monitor.DeviceId);
                        if (wallpaper is not null) {
                            await wallpaper.ScreenCapture(request.SavePath);
                        }
                    }
                    break;
                case WallpaperArrangement.Expand:
                case WallpaperArrangement.Duplicate:
                    if (_wpControl.Wallpapers.Count > 0) {
                        await _wpControl.Wallpapers[0].ScreenCapture(request.SavePath);
                    }
                    break;
            }

            return await Task.FromResult(new Empty());
        }

        public override async Task<Grpc_WpBasicData> UpdateBasicData(Grpc_UpdateBasicDataRequest request, ServerCallContext context) {
            var token = context.CancellationToken;
            var data = await _wpControl.UpdateBasicDataAsync(
                request.FolderPath,
                request.FolderName,
                request.FilePath,
                (FileType)request.FType);
            Grpc_WpBasicData grpc_data = DataAssist.BasicDataToGrpcData(data);

            return await Task.FromResult(grpc_data);
        }
        #endregion

        #region private utils
        public override Task<Grpc_GetCoreStatsResponse> GetCoreStats(Empty _, ServerCallContext context) {
            return Task.FromResult(new Grpc_GetCoreStatsResponse() {
                BaseDirectory = AppDomain.CurrentDomain.BaseDirectory,
                AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
            });
        }

        public override async Task SubscribeWallpaperChanged(Empty _, IServerStreamWriter<Empty> responseStream, ServerCallContext context) {
            try {
                while (!context.CancellationToken.IsCancellationRequested) {
                    var tcs = new TaskCompletionSource<bool>();
                    _wpControl.WallpaperChanged += WallpaperChanged;
                    void WallpaperChanged(object? s, EventArgs e) {
                        _wpControl.WallpaperChanged -= WallpaperChanged;
                        tcs.TrySetResult(true);
                    }
                    using var item = context.CancellationToken.Register(() => { tcs.TrySetResult(false); });
                    await tcs.Task;

                    if (context.CancellationToken.IsCancellationRequested) {
                        _wpControl.WallpaperChanged -= WallpaperChanged;
                        break;
                    }

                    await responseStream.WriteAsync(new Empty());
                }
            }
            catch (Exception e) {
                App.Log.Error(e);
            }
        }

        public override async Task SubscribeWallpaperError(Empty _, IServerStreamWriter<Grpc_WallpaperErrorResponse> responseStream, ServerCallContext context) {
            try {
                while (!context.CancellationToken.IsCancellationRequested) {
                    var resp = new Grpc_WallpaperErrorResponse();
                    var tcs = new TaskCompletionSource<bool>();
                    _wpControl.WallpaperError += WallpaperError;
                    void WallpaperError(object? s, Exception e) {
                        _wpControl.WallpaperError -= WallpaperError;

                        resp.ErrorMsg = e.Message ?? string.Empty;
                        resp.Error = e switch {
                            WorkerWException _ => Grpc_ErrorType.Workerw,
                            WallpaperNotAllowedException _ => Grpc_ErrorType.WallpaperNotAllowed,
                            WallpaperNotFoundException _ => Grpc_ErrorType.WallpaperNotFound,
                            WallpaperPluginException _ => Grpc_ErrorType.WallpaperPluginFail,
                            WallpaperPluginNotFoundException _ => Grpc_ErrorType.WallpaperPluginNotFound,
                            WallpaperPluginMediaCodecException _ => Grpc_ErrorType.WallpaperPluginMediaCodecMissing,
                            ScreenNotFoundException _ => Grpc_ErrorType.ScreenNotFound,
                            _ => Grpc_ErrorType.General,
                        };
                        tcs.TrySetResult(true);
                    }
                    using var item = context.CancellationToken.Register(() => { tcs.TrySetResult(false); });
                    await tcs.Task;

                    if (context.CancellationToken.IsCancellationRequested) {
                        _wpControl.WallpaperError -= WallpaperError;
                        break;
                    }

                    await responseStream.WriteAsync(resp);
                }
            }
            catch (Exception e) {
                App.Log.Error(e);
            }
        }
        #endregion

        private readonly IWallpaperControl _wpControl = desktopWpControl;
        private readonly IMonitorManager _monitorManager = monitorManager;
        private readonly IUserSettingsService _userSetting = userSetting;
    }
}
