using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using NLog;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Grpc.Client;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UI.Utils;
using VirtualPaper.UIComponent.Utils;
using Windows.ApplicationModel.Core;
using WinRT.Interop;
using WinRT;
//using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI {
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application {
        internal static Logger Log => LogManager.GetCurrentClassLogger();

        // 来自 Application-IApplicationFactoryMethods
        // 升级 WinUI 3 SDK 后请确认此 GUID 未更改
        //public static ref readonly Guid IID {
        //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    get {
        //        return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference((ReadOnlySpan<byte>)
        //            [
        //                87, 102, 217, 159, 148, 82, 101, 90, 161, 219,
        //                79, 234, 20, 53, 151, 218
        //            ]));
        //    }
        //}
        private static readonly Guid IApplicationIID =
            new(0x9FD96657, 0x5294, 0x5A65, 0xA1, 0xDB, 0x4F, 0xEA, 0x14, 0x35, 0x97, 0xDA);

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

            #region 唯一实例检查
            try {
                // 保证全局只有一个实例
                if (!_mutex.WaitOne(TimeSpan.FromSeconds(1), false)) {
                    ShutDown();
                    return;
                }
            }
            catch (AbandonedMutexException e) {
                //unexpected app termination.
                Debug.WriteLine(e.Message);
            }
            #endregion

            Log.Info("Starting...");

            this.InitializeComponent();
            // ref: https://github.com/microsoft/microsoft-ui-xaml/issues/1146 
            //this.FocusVisualKind = FocusVisualKind.HighVisibility;

            SetupUnhandledExceptionLogging();

            ConfigureServices();
            _userSettings = ObjectProvider.GetRequiredService<IUserSettingsClient>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
            //SetAppTheme(_userSettings.Settings.ApplicationTheme);
        }

        private static void ConfigureServices() {
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
            //ApplicationLanguages.PrimaryLanguageOverride = _userSettings.Settings.Language;

            // ref: https://github.com/AndrewKeepCoding/WinUI3Localizer
            if (Constants.ApplicationType.IsMSIX) {
                await LanguageUtil.InitializeLocalizerForPackaged(_userSettings.Settings.Language);
            }
            else {
                await LanguageUtil.InitializeLocalizerForUnpackaged(_userSettings.Settings.Language);
            }

            var m_window = ObjectProvider.GetRequiredService<MainWindow>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
            m_window.Activate();
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
        private readonly Mutex _mutex = new(false, Constants.CoreField.UniqueAppUIUid);
    }
}
