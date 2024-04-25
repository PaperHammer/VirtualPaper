using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.UserSetting;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using WallpaperArrangement = VirtualPaper.Grpc.Service.UserSetting.WallpaperArrangement;

namespace VirtualPaper.Grpc.Client
{
    public class UserSettingsClient : IUserSettingsClient
    {
        public ISettings Settings { get; private set; }

        public List<IApplicationRules> AppRules { get; private set; } = [];

        public UserSettingsClient()
        {
            _client = new(new NamedPipeChannel(".", Constants.SingleInstance.GrpcPipeServerName));

            Task.Run(async () =>
            {
                await LoadAsync<ISettings>().ConfigureAwait(false);
                await LoadAsync<List<IApplicationRules>>().ConfigureAwait(false);
            }).Wait();
        }

        public void Load<T>()
        {
            if (typeof(T) == typeof(ISettings))
            {
                Settings = GetSettings();
            }
            else if (typeof(T) == typeof(List<IApplicationRules>))
            {
                AppRules = GetAppRulesSettings();
            }
            else
            {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public async Task LoadAsync<T>()
        {
            if (typeof(T) == typeof(ISettings))
            {
                Settings = await GetSettingsAsync().ConfigureAwait(false);
            }
            else if (typeof(T) == typeof(List<IApplicationRules>))
            {
                AppRules = await GetAppRulesSettingsAsync().ConfigureAwait(false);
            }
            else
            {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public void Save<T>()
        {
            if (typeof(T) == typeof(ISettings))
            {
                SetSettings();
            }
            else if (typeof(T) == typeof(List<IApplicationRules>))
            {
                SetAppRulesSettings();
            }
            else
            {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public async Task SaveAsync<T>()
        {
            if (typeof(T) == typeof(ISettings))
            {
                await SetSettingsAsync().ConfigureAwait(false);
            }
            else if (typeof(T) == typeof(List<IApplicationRules>))
            {
                await SetAppRulesSettingsAsync().ConfigureAwait(false);
            }
            else
            {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        private void SetSettings()
        {
            _ = _client.SetSettings(CreateGrpcSettings(Settings));
        }

        private async Task SetSettingsAsync()
        {
            _ = await _client.SetSettingsAsync(CreateGrpcSettings(Settings));
        }

        private ISettings GetSettings()
        {
            var resp = _client.GetSettings(new Empty());
            return CreateSettingsFromGrpc(resp);
        }

        private async Task<ISettings> GetSettingsAsync()
        {
            var resp = await _client.GetSettingsAsync(new Empty());
            return CreateSettingsFromGrpc(resp);
        }

        private List<IApplicationRules> GetAppRulesSettings()
        {
            var appRules = new List<IApplicationRules>();
            var resp = _client.GetAppRulesSettings(new Empty());
            foreach (var item in resp.AppRules)
            {
                appRules.Add(new ApplicationRules(item.AppName, (Common.AppWpRunRulesEnum)((int)item.Rule)));
            }
            return appRules;
        }

        private async Task<List<IApplicationRules>> GetAppRulesSettingsAsync()
        {
            var appRules = new List<IApplicationRules>();
            var resp = await _client.GetAppRulesSettingsAsync(new Empty());
            foreach (var item in resp.AppRules)
            {
                appRules.Add(new ApplicationRules(item.AppName, (Common.AppWpRunRulesEnum)((int)item.Rule)));
            }
            return appRules;
        }

        private void SetAppRulesSettings()
        {
            var tmp = new AppRulesSettings();
            foreach (var item in AppRules)
            {
                tmp.AppRules.Add(new AppRulesData
                {
                    AppName = item.AppName,
                    Rule = (Service.UserSetting.AppRulesEnum)(int)item.Rule
                });
            }
            _ = _client.SetAppRulesSettings(tmp);
        }

        private async Task SetAppRulesSettingsAsync()
        {
            var tmp = new AppRulesSettings();
            foreach (var item in AppRules)
            {
                tmp.AppRules.Add(new AppRulesData
                {
                    AppName = item.AppName,
                    Rule = (Service.UserSetting.AppRulesEnum)(int)item.Rule
                });
            }
            _ = await _client.SetAppRulesSettingsAsync(tmp);
        }

        #region helpers

        private SettingsData CreateGrpcSettings(ISettings settings)
        {
            return new SettingsData()
            {
                AppFocusPause = (Service.UserSetting.AppRulesEnum)settings.AppFocus,
                AppFullscreenPause = (Service.UserSetting.AppRulesEnum)settings.AppFullscreen,
                ApplicationTheme = (Service.UserSetting.AppTheme)settings.ApplicationTheme,
                BatteryPause = (Service.UserSetting.AppRulesEnum)settings.BatteryPoweredn,
                PowerSaveModePause = (Service.UserSetting.AppRulesEnum)settings.PowerSaving,
                RemoteDesktopPause = (Service.UserSetting.AppRulesEnum)settings.RemoteDesktop,
                ApplicationThemeBackground = (Service.UserSetting.AppThemeBackground)settings.ApplicationThemeBackground,
                AppVersion = settings.AppVersion,
                ApplicationThemeBackgroundPath = settings.ApplicationThemeBackgroundPath,
                Language = settings.Language,
                IsUpdated = settings.IsUpdated,
                IsAutoStart = settings.IsAutoStart,
                IsFirstRun = settings.IsFirstRun,
                SysTrayIcon = settings.SysTrayIcon,

                WallpaperDir = settings.WallpaperDir,

                SelectedMonitor = new MonitorData()
                {
                    DeviceId = settings.SelectedMonitor.DeviceId,
                    DeviceName = settings.SelectedMonitor.DeviceName,
                    DisplayName = settings.SelectedMonitor.MonitorName,
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
                    }
                },
                IsAudioOnlyOnDesktop = settings.IsAudioOnlyOnDesktop,
                DisplayPauseSettings = (Service.UserSetting.DisplayPauseEnum)settings.StatuMechanism,
                WallpaperScaling = (Service.UserSetting.WallpaperScaler)settings.WallpaperScaling,
                WallpaperWaitTime = settings.WallpaperWaitTime,

                SystemTaskbarTheme = (Service.UserSetting.TaskbarTheme)settings.SystemTaskbarTheme,

                InputForward = (Service.UserSetting.InputForwardMode)settings.InputForward,
                MouseInputMovAlways = settings.MouseInputMovAlways,

                WallpaperArrangement = (WallpaperArrangement)settings.WallpaperArrangement,

                IsScreensaverLockOnResume = settings.IsScreensaverLockOnResume,
                IsScreensaverEmptyScreenShowBlack = settings.IsScreensaverEmptyScreenShowBlack,

                ProcessTimerInterval = settings.ProcessTimerInterval,

                WebDebugPort = settings.WebDebugPort,
                IsCefDiskCache = settings.IsCefDiskCache,
            };
        }

        private ISettings CreateSettingsFromGrpc(SettingsData settings)
        {
            return new Settings()
            {
                AppFocus = (Common.AppWpRunRulesEnum)settings.AppFocusPause,
                AppFullscreen = (Common.AppWpRunRulesEnum)settings.AppFullscreenPause,
                ApplicationTheme = (Common.AppTheme)settings.ApplicationTheme,
                BatteryPoweredn = (Common.AppWpRunRulesEnum)settings.BatteryPause,
                PowerSaving = (Common.AppWpRunRulesEnum)settings.PowerSaveModePause,
                RemoteDesktop = (Common.AppWpRunRulesEnum)settings.RemoteDesktopPause,
                ApplicationThemeBackground = (Common.AppThemeBackground)settings.ApplicationThemeBackground,
                AppVersion = settings.AppVersion,
                ApplicationThemeBackgroundPath = settings.ApplicationThemeBackgroundPath,
                ThemeBundleVersion = settings.ThemeBundleVersion,
                Language = settings.Language,
                IsUpdated = settings.IsUpdated,
                IsAutoStart = settings.IsAutoStart,
                IsFirstRun = settings.IsFirstRun,

                WallpaperDir = settings.WallpaperDir,

                SelectedMonitor = new Models.Cores.Monitor()
                {
                    DeviceId = settings.SelectedMonitor.DeviceId,
                    DeviceName = settings.SelectedMonitor.DeviceName,
                    MonitorName = settings.SelectedMonitor.DisplayName,
                    HMonitor = settings.SelectedMonitor.HMonitor,
                    IsPrimary = settings.SelectedMonitor.IsPrimary,
                    WorkingArea = new System.Drawing.Rectangle()
                    {
                        X = settings.SelectedMonitor.WorkingArea.X,
                        Y = settings.SelectedMonitor.WorkingArea.Y,
                        Width = settings.SelectedMonitor.WorkingArea.Width,
                        Height = settings.SelectedMonitor.WorkingArea.Height
                    },
                    Bounds = new System.Drawing.Rectangle()
                    {
                        X = settings.SelectedMonitor.Bounds.X,
                        Y = settings.SelectedMonitor.Bounds.Y,
                        Width = settings.SelectedMonitor.Bounds.Width,
                        Height = settings.SelectedMonitor.Bounds.Height
                    },
                    Index = settings.SelectedMonitor.Index,
                },
                
                IsAudioOnlyOnDesktop = settings.IsAudioOnlyOnDesktop,
                StatuMechanism = (Common.StatuMechanismEnum)settings.DisplayPauseSettings,
                WallpaperScaling = (Common.WallpaperScaler)settings.WallpaperScaling,
                WallpaperWaitTime = settings.WallpaperWaitTime,

                SystemTaskbarTheme = (Common.TaskbarTheme)settings.SystemTaskbarTheme,

                InputForward = (Common.InputForwardMode)settings.InputForward,
                MouseInputMovAlways = settings.MouseInputMovAlways,

                WallpaperArrangement = (Common.WallpaperArrangement)settings.WallpaperArrangement,

                IsScreensaverLockOnResume = settings.IsScreensaverLockOnResume,
                IsScreensaverEmptyScreenShowBlack = settings.IsScreensaverEmptyScreenShowBlack,

                ProcessTimerInterval = settings.ProcessTimerInterval,

                WebDebugPort = settings.WebDebugPort,
                IsCefDiskCache = settings.IsCefDiskCache,
            };
        }

        #endregion

        private readonly UserSettingService.UserSettingServiceClient _client;
    }
}
