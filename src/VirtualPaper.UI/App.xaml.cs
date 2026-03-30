using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using VirtualPaper.AppSettingsPanel.ViewModels;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Grpc.Client;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Converters;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;
using Windows.ApplicationModel.Core;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI {
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App() {
            #region 唯一实例检查
            try {
                // 保证全局只有一个实例
                if (!_mutex.WaitOne(TimeSpan.FromSeconds(1), false)) {
                    ArcLog.GetLogger<App>().Warn("UI has been running.");
                    Environment.Exit(0);
                    return;
                }
            }
            catch (AbandonedMutexException e) {
#if DEBUG
                //unexpected app termination.
                Debug.WriteLine(e.Message);
#endif
            }
            #endregion

            #region 初始化核心组件
            // 依赖注入
            AppServiceLocator.Services = ConfigureServices();
            #endregion

#if !DEBUG
            if (!SingleInstanceUtil.IsAppMutexRunning(Constants.CoreField.UniqueAppUid)) {
                _ = Native.MessageBox(IntPtr.Zero, "Wallpaper core is not running, run VirtualPaper.exe first before opening UI.", "Virtual Paper", 16);
                //Sad dev noises.. this.Exit() does not work without Window: https://github.com/microsoft/microsoft-ui-xaml/issues/5931
                Process.GetCurrentProcess().Kill();
            }            
#else
            VisibilityByValueConverter.DebugEnabled = true;
            BoolByValueConverter.DebugEnabled = true;
#endif

            ArcLog.GetLogger<App>().Info("Starting UI...");

            this.InitializeComponent();
            // ref: https://github.com/microsoft/microsoft-ui-xaml/issues/1146 
            //this.FocusVisualKind = FocusVisualKind.HighVisibility;

            SetupUnhandledExceptionLogging();

            _userSettings = AppServiceLocator.Services.GetRequiredService<IUserSettingsClient>();
        }

        private ServiceProvider ConfigureServices() {
            var provider = new ServiceCollection()
                .AddSingleton<MainWindow>()
                
                .AddSingleton<GeneralSettingViewModel>()
                .AddTransient<OtherSettingViewModel>()
                .AddTransient<PerformanceSettingViewModel>()
                .AddTransient<SystemSettingViewModel>()
                .AddSingleton<WpSettingsViewModel>()
                .AddSingleton<ScreenSaverViewModel>()
                .AddSingleton<LibraryContentsViewModel>()
                .AddTransient<ConfigSpaceViewModel>()
                .AddTransient<GetStartViewModel>()
                .AddTransient<DraftConfigViewModel>()
                .AddSingleton<WorkSpaceViewModel>()

                .AddSingleton<WallpaperIndexService>()

                .AddSingleton<IUserSettingsClient, UserSettingsClient>()
                .AddSingleton<IWallpaperControlClient, WallpaperControlClient>()
                .AddSingleton<IMonitorManagerClient, MonitorManagerClient>()
                .AddSingleton<IAppUpdaterClient, AppUpdaterClient>()
                .AddSingleton<ICommandsClient, CommandsClient>()
                .AddSingleton<IScrCommandsClient, ScrCommandsClient>()

                .BuildServiceProvider();

            return provider;
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs args) {
            CrossThreadInvoker.Initialize(new UiSynchronizationContext());

            // ref: https://github.com/microsoft/WindowsAppSDK/issues/1687
            //ApplicationLanguages.PrimaryLanguageOverride = _userSettings.Settings.Language;

            // ref: https://github.com/AndrewKeepCoding/WinUI3Localizer
            if (Constants.ApplicationType.IsMSIX) {
                await LanguageUtil.InitializeLocalizerForPackaged(_userSettings.Settings.Language);
            }
            else {
                await LanguageUtil.InitializeLocalizerForUnpackaged(_userSettings.Settings.Language);
            }

            var m_window = AppServiceLocator.Services.GetRequiredService<MainWindow>();
            m_window.Show();
            m_window.Activate();
        }

        private static void LogUnhandledException(Exception exception) => ArcLog.GetLogger<App>().Error(exception);

        private static void LogUnhandledException(UnhandledError exception) => ArcLog.GetLogger<App>().Error(exception);

        //Not working ugh..
        //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/5221
        private void SetupUnhandledExceptionLogging() {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject);

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception);

            this.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception);

            CoreApplication.UnhandledErrorDetected += (s, e) => {
                try {
                    e.UnhandledError.Propagate();
                }
                catch (Exception ex) {
                    LogUnhandledException(ex);
                }
            };
        }

        public static void ShutDown() {
            Application.Current.Exit();
            _ = Task.Run(() => {
                ((ServiceProvider)AppServiceLocator.Services)?.Dispose();
                ArcLog.GetLogger<App>().Info("UI was closed");
            });
        }

        private readonly IUserSettingsClient _userSettings;
        private readonly Mutex _mutex = new(false, Constants.CoreField.UniqueAppUIUid);
    }
}
