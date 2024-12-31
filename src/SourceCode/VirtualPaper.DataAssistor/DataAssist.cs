using VirtualPaper.Common;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Models;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using Monitor = VirtualPaper.Models.Cores.Monitor;

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
                MonitorContent = source.MonitorContent,
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

        public static Grpc_WpPlayerData MetadataToGrpcPlayingData(IWpBasicData data, RuntimeType rtype) {
            Grpc_WpPlayerData grpc_data = new() {
                WallpaperUid = data.WallpaperUid,
                RType = (Grpc_RuntimeType)rtype,
                FilePath = data.FilePath,
                FolderPath = data.FolderPath,
                ThumbnailPath = data.ThumbnailPath,
            };

            return grpc_data;
        }

        public static WpRuntimeData GrpcToRuntimeData(Grpc_WpRuntimeData source) {
            WpRuntimeData runtimeData = new() {
                MonitorContent = source.MonitorContent,
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

        public static WpPlayerData GrpcToPlayerData(Grpc_WpPlayerData grpc_data) {
            WpPlayerData playerData = new() {
                WallpaperUid = grpc_data.WallpaperUid,
                RType = (RuntimeType)grpc_data.RType,
                FilePath = grpc_data.FilePath,
                FolderPath = grpc_data.FolderPath,
                ThumbnailPath = grpc_data.ThumbnailPath,
            };

            return playerData;
        }

        public static Monitor GrpToMonitorData(Grpc_MonitorData grpc_monitor) {
            Monitor monitor = new() {
                DeviceId = grpc_monitor.DeviceId,
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

        public static Grpc_SettingsData SettingsToGrpc(ISettings settings) {
            var data = new Grpc_SettingsData() {
                AppFocusPause = (Grpc_AppRulesEnum)settings.AppFocus,
                AppFullscreenPause = (Grpc_AppRulesEnum)settings.AppFullscreen,
                ApplicationTheme = (Grpc_AppTheme)settings.ApplicationTheme,
                BatteryPause = (Grpc_AppRulesEnum)settings.BatteryPoweredn,
                PowerSaveModePause = (Grpc_AppRulesEnum)settings.PowerSaving,
                RemoteDesktopPause = (Grpc_AppRulesEnum)settings.RemoteDesktop,
                SystemBackdrop = (Grpc_AppSystemBackdrop)settings.SystemBackdrop,
                AppName = settings.AppName,
                AppVersion = settings.AppVersion,
                Language = settings.Language,
                IsUpdated = settings.IsUpdated,
                IsAutoStart = settings.IsAutoStart,
                IsFirstRun = settings.IsFirstRun,

                WallpaperDir = settings.WallpaperDir,

                SelectedMonitor = new Grpc_MonitorData() {
                    DeviceId = settings.SelectedMonitor.DeviceId,
                    IsPrimary = settings.SelectedMonitor.IsPrimary,
                    WorkingArea = new Grpc_Rectangle() {
                        X = settings.SelectedMonitor.WorkingArea.X,
                        Y = settings.SelectedMonitor.WorkingArea.Y,
                        Width = settings.SelectedMonitor.WorkingArea.Width,
                        Height = settings.SelectedMonitor.WorkingArea.Height
                    },
                    Bounds = new Grpc_Rectangle() {
                        X = settings.SelectedMonitor.Bounds.X,
                        Y = settings.SelectedMonitor.Bounds.Y,
                        Width = settings.SelectedMonitor.Bounds.Width,
                        Height = settings.SelectedMonitor.Bounds.Height
                    }
                },
                IsAudioOnlyOnDesktop = settings.IsAudioOnlyOnDesktop,
                StatuMechanism = (Grpc_StatuMechanismEnum)settings.StatuMechanism,
                WallpaperScaling = (Grpc_WallpaperScaler)settings.WallpaperScaling,
                WallpaperWaitTime = settings.WallpaperWaitTime,

                InputForward = (Grpc_InputForwardMode)settings.InputForward,
                MouseInputMovAlways = settings.MouseInputMovAlways,

                WallpaperArrangement = (Grpc_WallpaperArrangement)settings.WallpaperArrangement,

                //IsScreensaverLockOnResume = settings.IsScreensaverLockOnResume,
                //IsScreensaverEmptyScreenShowBlack = settings.IsScreensaverEmptyScreenShowBlack,

                ProcessTimerInterval = settings.ProcessTimerInterval,

                IsScreenSaverOn = settings.IsScreenSaverOn,
                IsRunningLock = settings.IsRunningLock,
                WaitingTime = settings.WaitingTime,
                ScreenSaverEffect = (Grpc_ScrEffectEnum)settings.ScreenSaverEffect,
            };
            foreach (var proc in settings.WhiteListScr) {
                data.WhiteListScr.Add(new Grpc_ProcInfoData() {
                    ProcName = proc.ProcName,
                    IconPath = proc.IconPath,
                    IsRunning = proc.IsRunning,
                });
            }

            return data;
        }

        public static Settings GrpcToSettings(Grpc_SettingsData settings) {
            var data = new Settings() {
                AppFocus = (AppWpRunRulesEnum)settings.AppFocusPause,
                AppFullscreen = (AppWpRunRulesEnum)settings.AppFullscreenPause,
                ApplicationTheme = (AppTheme)settings.ApplicationTheme,
                BatteryPoweredn = (AppWpRunRulesEnum)settings.BatteryPause,
                PowerSaving = (AppWpRunRulesEnum)settings.PowerSaveModePause,
                RemoteDesktop = (AppWpRunRulesEnum)settings.RemoteDesktopPause,
                SystemBackdrop = (AppSystemBackdrop)settings.SystemBackdrop,
                AppName = settings.AppName,
                AppVersion = settings.AppVersion,
                Language = settings.Language,
                IsUpdated = settings.IsUpdated,
                IsAutoStart = settings.IsAutoStart,
                IsFirstRun = settings.IsFirstRun,

                WallpaperDir = settings.WallpaperDir,

                SelectedMonitor = new Monitor() {
                    DeviceId = settings.SelectedMonitor.DeviceId,
                    IsPrimary = settings.SelectedMonitor.IsPrimary,
                    WorkingArea = new System.Drawing.Rectangle() {
                        X = settings.SelectedMonitor.WorkingArea.X,
                        Y = settings.SelectedMonitor.WorkingArea.Y,
                        Width = settings.SelectedMonitor.WorkingArea.Width,
                        Height = settings.SelectedMonitor.WorkingArea.Height
                    },
                    Bounds = new System.Drawing.Rectangle() {
                        X = settings.SelectedMonitor.Bounds.X,
                        Y = settings.SelectedMonitor.Bounds.Y,
                        Width = settings.SelectedMonitor.Bounds.Width,
                        Height = settings.SelectedMonitor.Bounds.Height
                    },
                    Content = settings.SelectedMonitor.Content,
                },

                IsAudioOnlyOnDesktop = settings.IsAudioOnlyOnDesktop,
                StatuMechanism = (StatuMechanismEnum)settings.StatuMechanism,
                WallpaperScaling = (WallpaperScaler)settings.WallpaperScaling,
                WallpaperWaitTime = settings.WallpaperWaitTime,

                InputForward = (InputForwardMode)settings.InputForward,
                MouseInputMovAlways = settings.MouseInputMovAlways,

                WallpaperArrangement = (WallpaperArrangement)settings.WallpaperArrangement,

                //IsScreensaverLockOnResume = settings.IsScreensaverLockOnResume,
                //IsScreensaverEmptyScreenShowBlack = settings.IsScreensaverEmptyScreenShowBlack,

                ProcessTimerInterval = settings.ProcessTimerInterval,

                IsScreenSaverOn = settings.IsScreenSaverOn,
                IsRunningLock = settings.IsRunningLock,
                WaitingTime = settings.WaitingTime,
                ScreenSaverEffect = (ScrEffect)settings.ScreenSaverEffect,
            };
            foreach (var proc in settings.WhiteListScr) {
                data.WhiteListScr.Add(new ProcInfo() {
                    ProcName = proc.ProcName,
                    IconPath = proc.IconPath,
                    IsRunning = proc.IsRunning,
                });
            }

            return data;
        }
    }
}
