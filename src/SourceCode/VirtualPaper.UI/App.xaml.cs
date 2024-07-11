using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Grpc.Client;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UI.CommServers;
using VirtualPaper.UI.Services;
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UI.ViewModels;
using VirtualPaper.UI.ViewModels.AppSettings;
using VirtualPaper.UI.ViewModels.WpSettingsComponents;
using Windows.Storage;
using WinUI3Localizer;
using static VirtualPaper.Common.Constants;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static bool IsNeedReslease { get; set; } = false;
        public static SemaphoreSlim SemaphoreSlimForLib { get; } = new(0, 1);

        public static IServiceProvider Services
        {
            get
            {
                IServiceProvider serviceProvider = ((App)Current)._serviceProvider;
                return serviceProvider ?? throw new InvalidOperationException("The service provider is not initialized");
            }
        }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
#if DEBUG != true
            if (!SingleInstanceUtil.IsAppMutexRunning(SingleInstance.UniqueAppUid))
            {
                _ = Native.MessageBox(IntPtr.Zero, "Wallpaper core is not running, run VirtualPaper.exe first before opening UI.", "Virtual Paper", 16);
                //Sad dev noises.. this.Exit() does not work without Window: https://github.com/microsoft/microsoft-ui-xaml/issues/5931
                Process.GetCurrentProcess().Kill();
            }
#endif
            _logger.Info("Starting...");

            this.InitializeComponent();

            _serviceProvider = ConfigureServices();
            _userSettings = Services.GetRequiredService<IUserSettingsClient>();

            SetAppTheme(_userSettings.Settings.ApplicationTheme);
            SetupUnhandledExceptionLogging();
        }

        private ServiceProvider ConfigureServices()
        {
            var provider = new ServiceCollection()
                .AddSingleton<MainWindow>()

                .AddSingleton<IWallpaperControlClient, WallpaperControlClient>()
                .AddSingleton<IMonitorManagerClient, MonitorManagerClient>()
                .AddSingleton<IUserSettingsClient, UserSettingsClient>()
                .AddSingleton<IAppUpdaterClient, AppUpdaterClient>()
                .AddSingleton<ICommandsClient, CommandsClient>()
                .AddSingleton<IScrCommandsClient, ScrCommandsClient>()

                .AddSingleton<WpSettingsViewModel>()
                .AddSingleton<LibraryContentsViewModel>()
                .AddSingleton<WpNavSettingsViewModel>()
                .AddTransient<GeneralSettingViewModel>()
                .AddTransient<PerformanceSettingViewModel>()
                .AddTransient<SystemSettingViewModel>()
                .AddTransient<OtherSettingViewModel>()

                .AddSingleton<TrayCommand>()

                .AddSingleton<IDialogService, DialogService>()

                .AddHttpClient()

                .BuildServiceProvider();

            return provider;
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            // ref: https://github.com/microsoft/WindowsAppSDK/issues/1687
            //ApplicationLanguages.PrimaryLanguageOverride = _userSettings.Settings.Language;

            // ref: https://github.com/AndrewKeepCoding/WinUI3Localizer
            if (Constants.ApplicationType.IsMSIX)
            {
                await InitializeLocalizerForPackaged(_userSettings.Settings.Language);
            }
            else
            {
                await InitializeLocalizerForUnpackaged(_userSettings.Settings.Language);
            }

            Services.GetRequiredService<MainWindow>();
            //var m_window = Services.GetRequiredService<MainWindow>();
            //m_window.Activate();
            //}

            // 避免文字无法初始化
            Services.GetRequiredService<TrayCommand>();
        }

        // ref: https://github.com/AndrewKeepCoding/WinUI3Localizer
        private async Task InitializeLocalizerForUnpackaged(string lang)
        {
            // Initialize a "Strings" folder in the executables folder.
            string stringsFolderPath = Path.Combine(AppContext.BaseDirectory, "Strings");
            StorageFolder stringsFolder = await StorageFolder.GetFolderFromPathAsync(stringsFolderPath);

            ILocalizer localizer = await new LocalizerBuilder()
                .AddStringResourcesFolderForLanguageDictionaries(stringsFolderPath)
                .SetOptions(options =>
                {
                    options.DefaultLanguage = lang;
                })
                .Build();
        }

        private async Task InitializeLocalizerForPackaged(string lang)
        {
            // Initialize a "Strings" folder in the "LocalFolder" for the packaged app.
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFolder stringsFolder = await localFolder.CreateFolderAsync(
              "Strings",
               CreationCollisionOption.OpenIfExists);

            // Create string resources file from app resources if doesn't exists.
            string resourceFileName = "Resources.resw";
            await CreateStringResourceFileIfNotExists(stringsFolder, "zh-CN", resourceFileName);
            await CreateStringResourceFileIfNotExists(stringsFolder, "en-US", resourceFileName);

            ILocalizer localizer = await new LocalizerBuilder()
                .AddStringResourcesFolderForLanguageDictionaries(stringsFolder.Path)
                .SetOptions(options =>
                {
                    options.DefaultLanguage = lang;
                })
                .Build();
        }

        public static async void LanguageChanged(string lang)
        {
            await Localizer.Get().SetLanguage(lang);
        }

        private static async Task CreateStringResourceFileIfNotExists(StorageFolder stringsFolder, string language, string resourceFileName)
        {
            StorageFolder languageFolder = await stringsFolder.CreateFolderAsync(
                language,
                CreationCollisionOption.OpenIfExists);

            if (await languageFolder.TryGetItemAsync(resourceFileName) is null)
            {
                string resourceFilePath = Path.Combine(stringsFolder.Name, language, resourceFileName);
                StorageFile resourceFile = await LoadStringResourcesFileFromAppResource(resourceFilePath);
                _ = await resourceFile.CopyAsync(languageFolder);
            }
        }

        private static async Task<StorageFile> LoadStringResourcesFileFromAppResource(string filePath)
        {
            Uri resourcesFileUri = new($"ms-appx:///{filePath}");
            return await StorageFile.GetFileFromApplicationUriAsync(resourcesFileUri);
        }

        //Cannot change runtime.
        //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/4474
        private void SetAppTheme(AppTheme theme)
        {
            switch (theme)
            {
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

        private void LogUnhandledException<T>(T exception) => _logger.Error(exception);
        //Not working ugh..
        //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/5221
        private void SetupUnhandledExceptionLogging()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject);

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception);

            this.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception);

            Windows.ApplicationModel.Core.CoreApplication.UnhandledErrorDetected += (s, e) =>
                LogUnhandledException(e.UnhandledError);
        }

        public static void ShutDown()
        {
            try
            {
                Task.Run(() =>
                {
                    ((ServiceProvider)App.Services)?.Dispose();
                    _logger.Info("UI was closed");
                });
            }
            catch (InvalidOperationException) { }
        }

        private IServiceProvider _serviceProvider;
        private IUserSettingsClient _userSettings;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
