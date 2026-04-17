using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Utils;
using Windows.ApplicationModel.Core;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.Launcher {
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application {
        public static ISettings UserSettings => App.Current is App app ? app._userSettings : new Settings();

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App() {
            #region 唯一实例检查
            try {
                if (!_mutex.WaitOne(TimeSpan.FromSeconds(1), false)) {
                    _m_window?.Activate();
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

            ArcLog.GetLogger<App>().Info("Starting Updater...");

            #region 初始化核心组件
            AppServiceLocator.Services = ConfigureServices();
            #endregion            

            InitializeComponent();

            SetupUnhandledExceptionLogging();

            #region init data
            try {
                _userSettings = JsonSaver.Load<Settings>(Constants.CommonPaths.UserSettingsPath, SettingsContext.Default);
            }
            catch (Exception e) {
                ArcLog.GetLogger<App>().Error(e);
                _userSettings = new Settings();
            }
            #endregion
        }

        private ServiceProvider ConfigureServices() {
            var provider = new ServiceCollection()
                .AddSingleton<MainWindow>()

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
                await LanguageUtil.InitializeLocalizerForPackaged(_userSettings.Language);
            }
            else {
                await LanguageUtil.InitializeLocalizerForUnpackaged(_userSettings.Language);
            }

            _m_window = AppServiceLocator.Services.GetRequiredService<MainWindow>();
            _m_window?.Show();
        }

        private static void LogUnhandledException(Exception exception) => ArcLog.GetLogger<App>().Error(exception);

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
            ((ServiceProvider)AppServiceLocator.Services)?.Dispose();
            ArcLog.GetLogger<App>().Info("Updater was closed");
            Application.Current.Exit();
        }

        private readonly Settings _userSettings = null!;
        private readonly Mutex _mutex = new(false, Constants.CoreField.UniqueAppUpdateUid);
        private MainWindow? _m_window;
    }
}
