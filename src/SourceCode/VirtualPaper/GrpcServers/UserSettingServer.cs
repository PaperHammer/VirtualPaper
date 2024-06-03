using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using NLog;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Grpc.Service.UserSetting;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils;
using ProcInfoData = VirtualPaper.Grpc.Service.UserSetting.ProcInfoData;
using Rectangle = VirtualPaper.Grpc.Service.UserSetting.Rectangle;

namespace VirtualPaper.GrpcServers
{
    internal class UserSettingServer(
        IMonitorManager monitorManager,
        IUserSettingsService userSetting,
        IUIRunnerService uiRunner) : UserSettingService.UserSettingServiceBase
    {
        public override Task<WallpaperLayoutsSettings> GetWallpaperLayouts(Empty request, ServerCallContext context)
        {
            var resp = new WallpaperLayoutsSettings();
            foreach (var wl in _userSetting.WallpaperLayouts)
            {
                resp.WallpaperLayouts.Add(new WallpaperLayoutData
                {
                    Monitor = new()
                    {
                        DeviceId = wl.Monitor.DeviceId,
                        DeviceName = wl.Monitor.DeviceName,
                        DisplayName = wl.Monitor.MonitorName,
                        HMonitor = (int)wl.Monitor.HMonitor,
                        WorkingArea = new()
                        {
                            X = wl.Monitor.WorkingArea.X,
                            Y = wl.Monitor.WorkingArea.Y,
                            Width = wl.Monitor.WorkingArea.Width,
                            Height = wl.Monitor.WorkingArea.Height,                            
                        },
                        Bounds = new()
                        {
                            X = wl.Monitor.Bounds.X,
                            Y = wl.Monitor.Bounds.Y,
                            Width = wl.Monitor.Bounds.Width,
                            Height = wl.Monitor.Bounds.Height,
                        },
                        Content = wl.Monitor.Content,
                    },
                    FolderPath = wl.FolderPath,
                });
            }

            return Task.FromResult(resp);
        }

        public override Task<AppRulesSettings> GetAppRulesSettings(Empty request, ServerCallContext context)
        {
            var resp = new AppRulesSettings();
            foreach (var app in _userSetting.AppRules)
            {
                resp.AppRules.Add(new AppRulesData
                {
                    AppName = app.AppName,
                    Rule = (Grpc.Service.UserSetting.AppRulesEnum)((int)app.Rule)
                });
            }

            return Task.FromResult(resp);
        }

        public override Task<Empty> SetAppRulesSettings(AppRulesSettings request, ServerCallContext context)
        {
            _userSetting.AppRules.Clear();
            foreach (var item in request.AppRules)
            {
                _userSetting.AppRules.Add(new ApplicationRules(item.AppName, (Common.AppWpRunRulesEnum)(int)item.Rule));
            }

            try
            {
                return Task.FromResult(new Empty());
            }
            finally
            {
                lock (appRulesWriteLock)
                {
                    _userSetting.Save<List<IApplicationRules>>();
                }
            }
        }

        public override Task<SettingsData> GetSettings(Empty request, ServerCallContext context)
        {
            var settings = _userSetting.Settings;
            var resp = new SettingsData()
            {
                SelectedMonitor = new MonitorData()
                {
                    DeviceId = settings.SelectedMonitor.DeviceId ?? string.Empty,
                    DeviceName = settings.SelectedMonitor.DeviceName ?? string.Empty,
                    DisplayName = settings.SelectedMonitor.MonitorName ?? string.Empty,
                    HMonitor = settings.SelectedMonitor.HMonitor.ToInt32(),
                    IsPrimary = settings.SelectedMonitor.IsPrimary,
                    WorkingArea = new Rectangle()
                    {
                        X = settings.SelectedMonitor.WorkingArea.X,
                        Y = settings.SelectedMonitor.WorkingArea.Y,
                        Width = settings.SelectedMonitor.WorkingArea.Width,
                        Height = settings.SelectedMonitor.WorkingArea.Height
                    },
                    Bounds = new Rectangle()
                    {
                        X = settings.SelectedMonitor.Bounds.X,
                        Y = settings.SelectedMonitor.Bounds.Y,
                        Width = settings.SelectedMonitor.Bounds.Width,
                        Height = settings.SelectedMonitor.Bounds.Height
                    }, 
                    Content = settings.SelectedMonitor.Content
                },
                WallpaperArrangement = (Grpc.Service.UserSetting.WallpaperArrangement)_userSetting.Settings.WallpaperArrangement,
                AppVersion = _userSetting.Settings.AppVersion,
                IsFirstRun = _userSetting.Settings.IsFirstRun,
                AppFocusPause = (Grpc.Service.UserSetting.AppRulesEnum)_userSetting.Settings.AppFocus,
                AppFullscreenPause = (Grpc.Service.UserSetting.AppRulesEnum)_userSetting.Settings.AppFullscreen,
                BatteryPause = (Grpc.Service.UserSetting.AppRulesEnum)_userSetting.Settings.BatteryPoweredn,
                WallpaperWaitTime = _userSetting.Settings.WallpaperWaitTime,
                ProcessTimerInterval = _userSetting.Settings.ProcessTimerInterval,
                WallpaperScaling = (Grpc.Service.UserSetting.WallpaperScaler)_userSetting.Settings.WallpaperScaling,
                InputForward = (Grpc.Service.UserSetting.InputForwardMode)_userSetting.Settings.InputForward,
                MouseInputMovAlways = _userSetting.Settings.MouseInputMovAlways,
                WallpaperDir = _userSetting.Settings.WallpaperDir,
                IsAudioOnlyOnDesktop = _userSetting.Settings.IsAudioOnlyOnDesktop,
                IsAutoStart = _userSetting.Settings.IsAutoStart,
                ApplicationTheme = (Grpc.Service.UserSetting.AppTheme)_userSetting.Settings.ApplicationTheme,
                RemoteDesktopPause = (Grpc.Service.UserSetting.AppRulesEnum)_userSetting.Settings.RemoteDesktop,
                PowerSaveModePause = (Grpc.Service.UserSetting.AppRulesEnum)_userSetting.Settings.PowerSaving,
                Language = _userSetting.Settings.Language,
                StatuMechanism = (Grpc.Service.UserSetting.StatuMechanismEnum)_userSetting.Settings.StatuMechanism,
                IsUpdated = _userSetting.Settings.IsUpdated,
                SystemBackdrop = (Grpc.Service.UserSetting.AppSystemBackdrop)_userSetting.Settings.SystemBackdrop,
                IsScreenSaverOn = _userSetting.Settings.IsScreenSaverOn,
                IsRunningLock = _userSetting.Settings.IsRunningLock,
                WaitingTime = _userSetting.Settings.WaitingTime,
                ScreenSaverEffect = (ScrEffectEnum)_userSetting.Settings.ScreenSaverEffect,
            };
            foreach (var proc in settings.WhiteListScr)
            {
                resp.WhiteListScr.Add(new ProcInfoData()
                {
                    ProcName = proc.ProcName,
                    IconPath = proc.IconPath,
                    IsRunning = proc.IsRunning,
                });
            }

            return Task.FromResult(resp);
        }

        public override Task<Empty> SetSettings(SettingsData request, ServerCallContext context)
        {
            bool restartRequired = 
                (Common.AppTheme)request.ApplicationTheme != _userSetting.Settings.ApplicationTheme 
                || request.Language != _userSetting.Settings.Language
                || (Common.AppSystemBackdrop)request.SystemBackdrop != _userSetting.Settings.SystemBackdrop;

            if (request.IsAutoStart != _userSetting.Settings.IsAutoStart)
            {
                _userSetting.Settings.IsAutoStart = request.IsAutoStart;
                try
                {
                    _ = WindowsAutoStart.SetAutoStart(_userSetting.Settings.IsAutoStart);
                }
                catch (Exception e)
                {
                    _logger.Error(e);
                }
            }

            if ((Common.AppTheme)request.ApplicationTheme != _userSetting.Settings.ApplicationTheme)
            {
                App.ChangeTheme((Common.AppTheme)request.ApplicationTheme);
            }

            if (request.Language != _userSetting.Settings.Language)
            {
                App.ChangeLanguage(request.Language);
            }

            _userSetting.Settings.SelectedMonitor = _monitorManager.Monitors.FirstOrDefault(x => request.SelectedMonitor.DeviceId == x.DeviceId) ?? _monitorManager.PrimaryMonitor;
            _userSetting.Settings.WallpaperArrangement = (Common.WallpaperArrangement)((int)request.WallpaperArrangement);
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
            foreach (var proc in request.WhiteListScr)
            {
                _userSetting.Settings.WhiteListScr.Add(new Models.ProcInfo()
                {
                    ProcName = proc.ProcName,
                    IconPath = proc.IconPath,
                    IsRunning = proc.IsRunning,
                });
            }

            try
            {
                return Task.FromResult(new Empty());
            }
            finally
            {
                lock (settingsWriteLock)
                {
                    _userSetting.Save<ISettings>();
                    if (restartRequired)
                    {
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
