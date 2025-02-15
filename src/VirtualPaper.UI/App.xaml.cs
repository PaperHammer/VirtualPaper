using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using NLog;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Grpc.Client;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels;
using VirtualPaper.UIComponent.Utils;
using WinUI3Localizer;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI {
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application {
        internal static DispatcherQueue UITaskInvokeQueue => _dispatcherQueue;
        internal static Logger Log => LogManager.GetCurrentClassLogger();

        //public static IServiceProvider Services {
        //    get {
        //        IServiceProvider serviceProvider = ((App)Current)._serviceProvider;
        //        return serviceProvider ?? throw new InvalidOperationException("The service provider is not initialized");
        //    }
        //}

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
            //_serviceProvider = ConfigureServices();
            _userSettings = ObjectProvider.GetRequiredService<IUserSettingsClient>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
            //_userSettingsClient = Services.GetRequiredService<IUserSettingsClient>();

            SetAppTheme(_userSettings.Settings.ApplicationTheme);
        }

        private static void ConfigureServices() {
            ObjectProvider.RegisterRelation<IWallpaperControlClient, WallpaperControlClient>();
            ObjectProvider.RegisterRelation<IMonitorManagerClient, MonitorManagerClient>();
            ObjectProvider.RegisterRelation<IUserSettingsClient, UserSettingsClient>();
            ObjectProvider.RegisterRelation<IAppUpdaterClient, AppUpdaterClient>();
            ObjectProvider.RegisterRelation<ICommandsClient, CommandsClient>();
            ObjectProvider.RegisterRelation<IScrCommandsClient, ScrCommandsClient>();
        }

        //private static ServiceProvider ConfigureServices() {
        //    var provider = new ServiceCollection()
        //        .AddSingleton<MainWindow>()

        //        .AddSingleton<IWallpaperControlClient, WallpaperControlClient>()
        //        .AddSingleton<IMonitorManagerClient, MonitorManagerClient>()
        //        .AddSingleton<IUserSettingsClient, UserSettingsClient>()
        //        .AddSingleton<IAppUpdaterClient, AppUpdaterClient>()
        //        .AddSingleton<ICommandsClient, CommandsClient>()
        //        .AddSingleton<IScrCommandsClient, ScrCommandsClient>()

        //        .AddSingleton<MainWindowViewModel>()
        //        .AddSingleton<WpSettingsViewModel>()
        //        .AddSingleton<LibraryContentsViewModel>()
        //        .AddSingleton<ScreenSaverViewModel>()
        //        .AddTransient<GeneralSettingViewModel>()
        //        .AddTransient<PerformanceSettingViewModel>()
        //        .AddTransient<SystemSettingViewModel>()
        //        .AddTransient<OtherSettingViewModel>()

        //        .AddSingleton<TrayCommand>()

        //        .AddSingleton<IDialogService, DialogUtil>()

        //        .AddHttpClient()

        //        .BuildServiceProvider();

        //    return provider;
        //}

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
            //Services.GetRequiredService<MainWindow>().Show();
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

        private void LogUnhandledException<T>(T exception) => Log.Error(exception);

        //Not working ugh..
        //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/5221
        private void SetupUnhandledExceptionLogging() {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject);

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception);

            this.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception);

            Windows.ApplicationModel.Core.CoreApplication.UnhandledErrorDetected += (s, e) =>
                LogUnhandledException(e.UnhandledError);
        }

        public static void ShutDown() {
            Task.Run(async () => {
                //((ServiceProvider)App.Services)?.Dispose();
                await ObjectProvider.GetRequiredService<IWallpaperControlClient>(ObjectLifetime.Singleton, ObjectLifetime.Singleton).CloseAllPreviewAsync();

                ObjectProvider.Clean();
                Log.Info("UI was closed");
            });
        }

        public static string GetI18n(string key) {
            return _i18n.GetLocalizedString(key);
        }

        //private readonly IServiceProvider _serviceProvider;
        private readonly IUserSettingsClient _userSettings;
        private static readonly ILocalizer _i18n = LanguageUtil.LocalizerInstacne;
        private static readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? DispatcherQueueController.CreateOnCurrentThread().DispatcherQueue;
    }
}
