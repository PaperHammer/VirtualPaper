using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using NLog;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Grpc.Client;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Utils;
using Windows.ApplicationModel.Core;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI {
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application {
        internal static Logger Log => LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App() {
#if !DEBUG
            if (!SingleInstanceUtil.IsAppMutexRunning(Constants.CoreField.UniqueAppUid))
            {
                _ = Native.MessageBox(IntPtr.Zero, "Wallpaper core is not running, run VirtualPaper.exe first before opening UI.", "Virtual Paper", 16);
                //Sad dev noises.. this.Exit() does not work without Window: https://github.com/microsoft/microsoft-ui-xaml/issues/5931
                Process.GetCurrentProcess().Kill();
            }
#endif
            Log.Info("Starting...");

            this.InitializeComponent();
            // ref: https://github.com/microsoft/microsoft-ui-xaml/issues/1146 
            //this.FocusVisualKind = FocusVisualKind.HighVisibility;

            SetupUnhandledExceptionLogging();

            ConfigureServices();
            _userSettings = ObjectProvider.GetRequiredService<IUserSettingsClient>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
            SetAppTheme(_userSettings.Settings.ApplicationTheme);
        }

        private static void ConfigureServices() {
            ObjectProvider.RegisterRelation<IGalleryClient, GalleryClient>();
            ObjectProvider.RegisterRelation<IAccountClient, AccountClient>();
            ObjectProvider.RegisterRelation<IWallpaperControlClient, WallpaperControlClient>();
            ObjectProvider.RegisterRelation<IMonitorManagerClient, MonitorManagerClient>();
            ObjectProvider.RegisterRelation<IUserSettingsClient, UserSettingsClient>();
            ObjectProvider.RegisterRelation<IAppUpdaterClient, AppUpdaterClient>();
            ObjectProvider.RegisterRelation<ICommandsClient, CommandsClient>();
            ObjectProvider.RegisterRelation<IScrCommandsClient, ScrCommandsClient>();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs args) {
            CrossThreadInvoker.Initialize(new UiSynchronizationContext());

            // ref: https://github.com/microsoft/WindowsAppSDK/issues/1687
            //ApplicationLanguages.PrimaryLanguageOverride = _userSettingsClient.Settings.Language;

            // ref: https://github.com/AndrewKeepCoding/WinUI3Localizer
            if (Constants.ApplicationType.IsMSIX) {
                await LanguageUtil.InitializeLocalizerForPackaged(_userSettings.Settings.Language);
            }
            else {
                await LanguageUtil.InitializeLocalizerForUnpackaged(_userSettings.Settings.Language);
            }

            ObjectProvider.GetRequiredService<MainWindow>(ObjectLifetime.Singleton, ObjectLifetime.Singleton).Show();
            // 避免文字无法初始化
            //ObjectProvider.GetRequiredService<TrayCommand>();
            //Services.GetRequiredService<TrayCommand>();
        }

        //Cannot change runtime.
        //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/4474
        private void SetAppTheme(AppTheme theme) {
            switch (theme) {
                case AppTheme.Auto:
                    //Nothing
                    break;
                case AppTheme.Light:
                    this.RequestedTheme = ApplicationTheme.Light;
                    break;
                case AppTheme.Dark:
                    this.RequestedTheme = ApplicationTheme.Dark;
                    break;
            }
        }

        private static void LogUnhandledException<T>(T exception) => Log.Error(exception);

        //Not working ugh..
        //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/5221
        private void SetupUnhandledExceptionLogging() {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject);

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception);

            this.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception);

            CoreApplication.UnhandledErrorDetected += (s, e) =>
                LogUnhandledException(e.UnhandledError);
        }

        public static void ShutDown() {
            Task.Run(async () => {
                await ObjectProvider.GetRequiredService<IWallpaperControlClient>(ObjectLifetime.Singleton, ObjectLifetime.Singleton).CloseAllPreviewAsync();

                ObjectProvider.Clean();
                Log.Info("UI was closed");
            });
        }

        private readonly IUserSettingsClient _userSettings;
    }
}
