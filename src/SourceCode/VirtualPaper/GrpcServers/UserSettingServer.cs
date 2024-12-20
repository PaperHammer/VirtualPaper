using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using NLog;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Grpc.Service.UserSettings;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils;

namespace VirtualPaper.GrpcServers {
    internal class UserSettingServer(
        IMonitorManager monitorManager,
        IUserSettingsService userSetting,
        IUIRunnerService uiRunner) : Grpc_UserSettingsService.Grpc_UserSettingsServiceBase {
        public override Task<Grpc_WallpaperLayoutsSettings> GetWallpaperLayouts(Empty request, ServerCallContext context) {
            var resp = new Grpc_WallpaperLayoutsSettings();
            foreach (var layout in _userSetting.WallpaperLayouts) {
                resp.WallpaperLayouts.Add(new Grpc_WallpaperLayout {                    
                    FolderPath = layout.FolderPath,
                    MonitorDeviceId = layout.MonitorDeviceId,
                    MonitorContent = layout.MonitorContent,
                    RType = layout.RType,
                });
            }

            return Task.FromResult(resp);
        }

        public override Task<Grpc_AppRulesSettings> GetAppRulesSettings(Empty request, ServerCallContext context) {
            var resp = new Grpc_AppRulesSettings();
            foreach (var app in _userSetting.AppRules) {
                resp.AppRules.Add(new Grpc_AppRulesData {
                    AppName = app.AppName,
                    Rule = (Grpc_AppRulesEnum)((int)app.Rule)
                });
            }

            return Task.FromResult(resp);
        }

        public override Task<Empty> SetAppRulesSettings(Grpc_AppRulesSettings request, ServerCallContext context) {
            _userSetting.AppRules.Clear();
            foreach (var item in request.AppRules) {
                _userSetting.AppRules.Add(new ApplicationRules(item.AppName, (Common.AppWpRunRulesEnum)(int)item.Rule));
            }

            try {
                return Task.FromResult(new Empty());
            }
            finally {
                lock (appRulesWriteLock) {
                    _userSetting.Save<List<IApplicationRules>>();
                }
            }
        }

        public override Task<Grpc_SettingsData> GetSettings(Empty request, ServerCallContext context) {
            var settings = _userSetting.Settings;
            var resp = new Grpc_SettingsData() {
                SelectedMonitor = new Grpc_MonitorData() {
                    DeviceId = settings.SelectedMonitor.DeviceId ?? string.Empty,
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
                    },
                    Content = settings.SelectedMonitor.Content
                },
                WallpaperArrangement = (Grpc_WallpaperArrangement)_userSetting.Settings.WallpaperArrangement,
                AppName = _userSetting.Settings.AppName,
                AppVersion = _userSetting.Settings.AppVersion,
                IsFirstRun = _userSetting.Settings.IsFirstRun,
                AppFocusPause = (Grpc_AppRulesEnum)_userSetting.Settings.AppFocus,
                AppFullscreenPause = (Grpc_AppRulesEnum)_userSetting.Settings.AppFullscreen,
                BatteryPause = (Grpc_AppRulesEnum)_userSetting.Settings.BatteryPoweredn,
                WallpaperWaitTime = _userSetting.Settings.WallpaperWaitTime,
                ProcessTimerInterval = _userSetting.Settings.ProcessTimerInterval,
                WallpaperScaling = (Grpc_WallpaperScaler)_userSetting.Settings.WallpaperScaling,
                InputForward = (Grpc_InputForwardMode)_userSetting.Settings.InputForward,
                MouseInputMovAlways = _userSetting.Settings.MouseInputMovAlways,
                WallpaperDir = _userSetting.Settings.WallpaperDir,
                IsAudioOnlyOnDesktop = _userSetting.Settings.IsAudioOnlyOnDesktop,
                IsAutoStart = _userSetting.Settings.IsAutoStart,
                ApplicationTheme = (Grpc_AppTheme)_userSetting.Settings.ApplicationTheme,
                RemoteDesktopPause = (Grpc_AppRulesEnum)_userSetting.Settings.RemoteDesktop,
                PowerSaveModePause = (Grpc_AppRulesEnum)_userSetting.Settings.PowerSaving,
                Language = _userSetting.Settings.Language,
                StatuMechanism = (Grpc_StatuMechanismEnum)_userSetting.Settings.StatuMechanism,
                IsUpdated = _userSetting.Settings.IsUpdated,
                SystemBackdrop = (Grpc_AppSystemBackdrop)_userSetting.Settings.SystemBackdrop,
                IsScreenSaverOn = _userSetting.Settings.IsScreenSaverOn,
                IsRunningLock = _userSetting.Settings.IsRunningLock,
                WaitingTime = _userSetting.Settings.WaitingTime,
                ScreenSaverEffect = (Grpc_ScrEffectEnum)_userSetting.Settings.ScreenSaverEffect,
            };
            foreach (var proc in settings.WhiteListScr) {
                resp.WhiteListScr.Add(new Grpc_ProcInfoData() {
                    ProcName = proc.ProcName,
                    IconPath = proc.IconPath,
                    IsRunning = proc.IsRunning,
                });
            }

            return Task.FromResult(resp);
        }

        public override Task<Empty> SetSettings(Grpc_SettingsData request, ServerCallContext context) {
            bool restartRequired =
                (Common.AppTheme)request.ApplicationTheme != _userSetting.Settings.ApplicationTheme
                || request.Language != _userSetting.Settings.Language
                || (Common.AppSystemBackdrop)request.SystemBackdrop != _userSetting.Settings.SystemBackdrop;

            if (request.IsAutoStart != _userSetting.Settings.IsAutoStart) {
                _userSetting.Settings.IsAutoStart = request.IsAutoStart;
                try {
                    _ = WindowsAutoStart.SetAutoStart(_userSetting.Settings.IsAutoStart);
                }
                catch (Exception e) {
                    _logger.Error(e);
                }
            }

            if ((Common.AppTheme)request.ApplicationTheme != _userSetting.Settings.ApplicationTheme) {
                App.ChangeTheme((Common.AppTheme)request.ApplicationTheme);
            }

            if (request.Language != _userSetting.Settings.Language) {
                App.ChangeLanguage(request.Language);
            }

            _userSetting.Settings.SelectedMonitor = _monitorManager.Monitors.FirstOrDefault(x => request.SelectedMonitor.DeviceId == x.DeviceId) ?? _monitorManager.PrimaryMonitor;
            _userSetting.Settings.WallpaperArrangement = (Common.WallpaperArrangement)((int)request.WallpaperArrangement);
            _userSetting.Settings.AppName = request.AppName;
            _userSetting.Settings.AppVersion = request.AppVersion;
            _userSetting.Settings.IsFirstRun = request.IsFirstRun;
            _userSetting.Settings.AppFocus = (Common.AppWpRunRulesEnum)((int)request.AppFocusPause);
            _userSetting.Settings.AppFullscreen = (Common.AppWpRunRulesEnum)((int)request.AppFullscreenPause);
            _userSetting.Settings.BatteryPoweredn = (Common.AppWpRunRulesEnum)((int)request.BatteryPause);
            _userSetting.Settings.WallpaperWaitTime = request.WallpaperWaitTime;
            _userSetting.Settings.ProcessTimerInterval = request.ProcessTimerInterval;
            _userSetting.Settings.WallpaperScaling = (Common.WallpaperScaler)((int)request.WallpaperScaling);
            _userSetting.Settings.InputForward = (Common.InputForwardMode)request.InputForward;
            _userSetting.Settings.MouseInputMovAlways = request.MouseInputMovAlways;
            _userSetting.Settings.WallpaperDir = request.WallpaperDir;
            _userSetting.Settings.IsAudioOnlyOnDesktop = request.IsAudioOnlyOnDesktop;
            _userSetting.Settings.ApplicationTheme = (Common.AppTheme)request.ApplicationTheme;
            _userSetting.Settings.RemoteDesktop = (Common.AppWpRunRulesEnum)request.RemoteDesktopPause;
            _userSetting.Settings.PowerSaving = (Common.AppWpRunRulesEnum)request.PowerSaveModePause;
            _userSetting.Settings.Language = request.Language;
            _userSetting.Settings.StatuMechanism = (Common.StatuMechanismEnum)request.StatuMechanism;
            _userSetting.Settings.IsUpdated = request.IsUpdated;
            _userSetting.Settings.SystemBackdrop = (Common.AppSystemBackdrop)request.SystemBackdrop;
            _userSetting.Settings.IsScreenSaverOn = request.IsScreenSaverOn;
            _userSetting.Settings.IsRunningLock = request.IsRunningLock;
            _userSetting.Settings.WaitingTime = request.WaitingTime;
            _userSetting.Settings.ScreenSaverEffect = (Common.ScrEffect)request.ScreenSaverEffect;

            _userSetting.Settings.WhiteListScr = [];
            foreach (var proc in request.WhiteListScr) {
                _userSetting.Settings.WhiteListScr.Add(new Models.ProcInfo() {
                    ProcName = proc.ProcName,
                    IconPath = proc.IconPath,
                    IsRunning = proc.IsRunning,
                });
            }

            try {
                return Task.FromResult(new Empty());
            }
            finally {
                lock (settingsWriteLock) {
                    _userSetting.Save<ISettings>();
                    if (restartRequired) {
                        _uiRunner.RestartUI();
                    }
                }
            }
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IMonitorManager _monitorManager = monitorManager;
        private readonly IUserSettingsService _userSetting = userSetting;
        private readonly IUIRunnerService _uiRunner = uiRunner;
        private readonly object appRulesWriteLock = new();
        private readonly object settingsWriteLock = new();
    }
}
