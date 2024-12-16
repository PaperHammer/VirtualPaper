using VirtualPaper.Common;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.DataAssistor {
    public static class DataAssist {
        public static Grpc_WpBasicData BasicDataToGrpcData(IWpBasicData source) {
            Grpc_WpBasicData grpc_WpBasicData = new() {
                WallpaperUid = source.WallpaperUid,
                AppInfo = new() {
                    AppName = source.AppInfo.AppName,
                    AppVersion = source.AppInfo.AppVersion,
                    FileVersion = source.AppInfo.FileVersion,
                },
                Title = source.Title,
                Desc = source.Desc,
                Authors = source.Authors,
                PublishDate = source.PublishDate,
                Rating = source.Rating,
                FType = (Grpc_FileType)source.FType,
                IsSingleRType = source.IsSingleRType,
                Partition = source.Partition,
                Tags = source.Tags,

                FolderName = source.FolderName,
                FolderPath = source.FolderPath,
                FilePath = source.FilePath,
                ThumbnailPath = source.ThumbnailPath,

                Resolution = source.Resolution,
                AspectRatio = source.AspectRatio,
                FileSize = source.FileSize,
                FileExtension = source.FileExtension
            };

            return grpc_WpBasicData;
        }

        public static Grpc_WpRuntimeData RuntimeDataToGrpcData(IWpRuntimeData source) {
            Grpc_WpRuntimeData grpc_WpRuntimeData = new() {
                FolderPath = source.FolderPath,
                DepthFilePath = source.DepthFilePath,
                AppInfo = new() {
                    AppName = source.AppInfo.AppName,
                    AppVersion = source.AppInfo.AppVersion,
                    FileVersion = source.AppInfo.FileVersion,
                },
                WpEffectFilePathTemplate = source.WpEffectFilePathTemplate,
                WpEffectFilePathTemporary = source.WpEffectFilePathTemporary,
                WpEffectFilePathUsing = source.WpEffectFilePathUsing,
                RType = (Grpc_RuntimeType)source.RType,
            };

            return grpc_WpRuntimeData;
        }

        public static Grpc_WpPlayerData MetadataToGrpcPlayerData(IWpBasicData data, RuntimeType rtype) {
            Grpc_WpPlayerData grpc_WpPlayerData = new() {
                WallpaperUid = data.WallpaperUid,
                RType = (Grpc_RuntimeType)rtype,
                FilePath = data.FilePath,
                FolderPath = data.FolderPath,
                //ThumbnailPath = data.BasicData.ThumbnailPath,
                //WpEffectFilePathTemplate = data.RuntimeData.WpEffectFilePathTemplate,
                //WpEffectFilePathTemporary = data.RuntimeData.WpEffectFilePathTemporary, // control
                //WpEffectFilePathUsing = data.RuntimeData.WpEffectFilePathUsing,
                //DepthFilePath = data.RuntimeData.DepthFilePath,
            };

            return grpc_WpPlayerData;
        }

        public static WpRuntimeData GrpcToRuntimeData(Grpc_WpRuntimeData source) {
            WpRuntimeData runtimeData = new() {
                FolderPath = source.FolderPath,
                DepthFilePath = source.DepthFilePath,
                AppInfo = new() {
                    AppName = source.AppInfo.AppName,
                    AppVersion = source.AppInfo.AppVersion,
                    FileVersion = source.AppInfo.FileVersion,
                },
                WpEffectFilePathTemplate = source.WpEffectFilePathTemplate,
                WpEffectFilePathTemporary = source.WpEffectFilePathTemporary,
                WpEffectFilePathUsing = source.WpEffectFilePathUsing,
                RType = (RuntimeType)source.RType,
            };

            return runtimeData;
        }

        public static WpBasicData GrpcToBasicData(Grpc_WpBasicData source) {
            WpBasicData basicData = new() {
                WallpaperUid = source.WallpaperUid,
                AppInfo = new() {
                    AppName = source.AppInfo.AppName,
                    AppVersion = source.AppInfo.AppVersion,
                    FileVersion = source.AppInfo.FileVersion
                },
                Title = source.Title,
                Desc = source.Desc,
                Authors = source.Authors,
                PublishDate = source.PublishDate,
                Rating = source.Rating,
                FType = (FileType)source.FType,
                IsSingleRType = source.IsSingleRType,
                Partition = source.Partition,
                Tags = source.Tags,

                FolderName = source.FolderName,
                FolderPath = source.FolderPath,
                FilePath = source.FilePath,
                ThumbnailPath = source.ThumbnailPath,

                Resolution = source.Resolution,
                AspectRatio = source.AspectRatio,
                FileSize = source.FileSize,
                FileExtension = source.FileExtension
            };

            return basicData;
        }

        public static WpPlayerData GrpcToPlayerData(Grpc_WpPlayerData source) {
            WpPlayerData playerData = new() {
                WallpaperUid = source.WallpaperUid,
                RType = (RuntimeType)source.RType,
                FilePath = source.FilePath,
                FolderPath = source.FolderPath,
                //ThumbnailPath = source.ThumbnailPath,
                //WpEffectFilePathTemplate = source.WpEffectFilePathTemplate,
                //WpEffectFilePathTemporary = source.WpEffectFilePathTemporary,
                //WpEffectFilePathUsing = source.WpEffectFilePathUsing,
                //DepthFilePath = source.DepthFilePath,
            };

            return playerData;
        }

        public static Models.Cores.Monitor GrpToMonitorData(Grpc_MonitorData grpc_monitor) {
            Models.Cores.Monitor monitor = new() {
                DeviceId = grpc_monitor.DeviceId,
                //DeviceName = grpc_monitor.DeviceName,
                //MonitorName = grpc_monitor.MonitorName,
                //HMonitor = grpc_monitor.HMonitor,
                Content = grpc_monitor.Content,
                IsPrimary = grpc_monitor.IsPrimary,
                Bounds = new() {
                    X = grpc_monitor.Bounds.X,
                    Y = grpc_monitor.Bounds.Y,
                    Width = grpc_monitor.Bounds.Width,
                    Height = grpc_monitor.Bounds.Height,
                },
                WorkingArea = new() {
                    X = grpc_monitor.WorkingArea.X,
                    Y = grpc_monitor.WorkingArea.Y,
                    Width = grpc_monitor.WorkingArea.Width,
                    Height = grpc_monitor.WorkingArea.Height
                },
                ThumbnailPath = grpc_monitor.ThumbnailPath
            };

            return monitor;
        }

        public static Grpc_MonitorData MonitorDataToGrpc(IMonitor monitor) {
            Grpc_MonitorData grpc_data = new() {
                DeviceId = monitor.DeviceId,
                //DeviceName = monitor.DeviceName,
                //MonitorName = monitor.MonitorName,
                //HMonitor = (int)monitor.HMonitor,
                Content = monitor.Content,
                IsPrimary = monitor.IsPrimary,
                Bounds = new() {
                    X = monitor.Bounds.X,
                    Y = monitor.Bounds.Y,
                    Width = monitor.Bounds.Width,
                    Height = monitor.Bounds.Height,
                },
                WorkingArea = new() {
                    X = monitor.Bounds.X,
                    Y = monitor.Bounds.Y,
                    Width = monitor.Bounds.Width,
                    Height = monitor.Bounds.Height,
                },
                ThumbnailPath = monitor.ThumbnailPath
            };

            return grpc_data;
        }

        public static void FromRuntimeDataGetPlayerData(IWpPlayerData data, IWpRuntimeData wpRuntimeData) {
            data.WpEffectFilePathTemporary = wpRuntimeData.WpEffectFilePathTemporary;
            data.WpEffectFilePathTemplate = wpRuntimeData.WpEffectFilePathTemplate;
            data.WpEffectFilePathUsing = wpRuntimeData.WpEffectFilePathUsing;
            data.DepthFilePath = wpRuntimeData.DepthFilePath;
        }
    }
}
