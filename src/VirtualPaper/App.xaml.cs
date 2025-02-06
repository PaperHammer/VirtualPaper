using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using GrpcDotNetNamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using NLog;
using VirtualPaper.Common;
using VirtualPaper.Common.Models;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.PlaybackControl;
using VirtualPaper.Cores.ScreenSaver;
using VirtualPaper.Cores.TrayControl;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Factories;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Grpc.Service.Commands;
using VirtualPaper.Grpc.Service.MonitorManager;
using VirtualPaper.Grpc.Service.ScrCommands;
using VirtualPaper.Grpc.Service.Update;
using VirtualPaper.Grpc.Service.UserSettings;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.GrpcServers;
using VirtualPaper.lang;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services;
using VirtualPaper.Services.Download;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils.Theme;
using VirtualPaper.Views;
using VirtualPaper.Views.WindowsMsg;
using Wpf.Ui.Appearance;
using Application = System.Windows.Application;
using AppTheme = VirtualPaper.Common.AppTheme;
using MessageBox = System.Windows.MessageBox;

namespace VirtualPaper {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        internal static Logger Log => _log;
        internal static JobService Jobs => Services.GetRequiredService<JobService>();
        internal static IUserSettingsService IUserSettgins => Services.GetRequiredService<IUserSettingsService>();

        public static IServiceProvider Services {
            get {
                IServiceProvider serviceProvider = ((App)Current)._serviceProvider;
                return serviceProvider ?? throw new InvalidOperationException("The service provider is not initialized");
            }
        }

        public App() {
            #region 唯一实例检查
            try {
                // 保证全局只有一个实例
                if (!_mutex.WaitOne(TimeSpan.FromSeconds(1), false)) {
                    MessageBox.Show("已存在正在运行的程序，请检查托盘或任务管理器\nThere are already running programs, check the tray or Task Manager", "Virtual Paper", MessageBoxButton.OK, MessageBoxImage.Information);
                    ShutDown();
                    return;
                }
            }
            catch (AbandonedMutexException e) {
                //unexpected app termination.
                Debug.WriteLine(e.Message);
            }
            #endregion

            SetupUnhandledExceptionLogging(); // 初始化异常处理机制
            Log.Info(LogUtil.GetHardwareInfo()); // 记录硬件信息

            #region 必要路径处理
            try {
                // 清空缓存
                FileUtil.EmptyDirectory(Constants.CommonPaths.TempDir);
            }
            catch { }

            try {
                // 创建必要目录, eg: C:\Users\<User>\AppData\Local
                Directory.CreateDirectory(Constants.CommonPaths.LibraryDir);
                //Directory.CreateDirectory(Constants.CommonPaths.ScrSaverDir);
                Directory.CreateDirectory(Constants.CommonPaths.AppDataDir);
                Directory.CreateDirectory(Constants.CommonPaths.LogDir);
                Directory.CreateDirectory(Constants.CommonPaths.TempDir);
                Directory.CreateDirectory(Constants.CommonPaths.ExeIconDir);
                //Directory.CreateDirectory(Path.Combine(Constants.CommonPaths.TempDir, Constants.FolderName.WpStoreFolderName));
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "AppData directory creation failed, exiting..", MessageBoxButton.OK, MessageBoxImage.Error);
                ShutDown();
                return;
            }
            #endregion

            #region 初始化核心组件
            // 依赖注入
            _serviceProvider = ConfigureServices();
            // 将方法绑定到 Grpc 服务上
            _grpcServer = ConfigureGrpcServer();
            #endregion

            // 用户配置
            var userSettings = Services.GetRequiredService<IUserSettingsService>();
            if (userSettings.Settings.WallpaperDir == string.Empty
                || !Directory.Exists(userSettings.Settings.WallpaperDir)) {
                userSettings.Settings.WallpaperDir = Path.Combine(Constants.CommonPaths.LibraryDir, Constants.FolderName.WpStoreFolderName);
                Directory.CreateDirectory(userSettings.Settings.WallpaperDir);
            }
            // 初始化语言包
            ChangeLanguage(userSettings.Settings.Language);

            userSettings.Save<ISettings>();

            #region 启动相关后台服务
            try {
                // 启动针对从 Windows 发出的到该窗口的消息监听服务
                Services.GetRequiredService<WndProcMsgWindow>().Show();
                // 启动针对从外部设备发出的到该窗口的消息监听服务
                Services.GetRequiredService<RawInputMsgWindow>().Show();
                // 启动壁纸行为/状态监听服务
                Services.GetRequiredService<IPlayback>().Start(_ctsPlayback);
                // 启动托盘（后台）服务
                Services.GetRequiredService<MainWindow>().Show();
            }
            catch (Exception ex) {
                MessageBox.Show("Core runtime Error, please restart or reinstall.\n" + ex.Message);
                return;
            }
            #endregion

            if (userSettings.Settings.IsUpdated || userSettings.Settings.IsFirstRun) {
                SplashWindow? spl = userSettings.Settings.IsFirstRun ? new(0, 500) : null; spl?.Show();
                spl?.Close();
            }

            try {
                //restore wallpaper(s) from previous run.
                var wpControl = Services.GetRequiredService<IWallpaperControl>();
                wpControl.RestoreWallpaper();

                // 启动屏保服务（需要在"还原壁纸"后进行）
                bool isScrOn = userSettings.Settings.IsScreenSaverOn;
                if (isScrOn) {
                    Services.GetRequiredService<IScrControl>().Start();
                }

                //first run Setup-Wizard show..
                if (userSettings.Settings.IsFirstRun) {
                    Services.GetRequiredService<IUIRunnerService>().ShowUI();
                }
            }
            catch (Exception ex) {
                MessageBox.Show("Core runtime Error, please restart or reinstall.\n" + ex.Message);
                return;
            }

            #region 事件绑定
            //need to load theme later stage of startu to update..
            this.Startup += (s, e) => {
                ChangeTheme(userSettings.Settings.ApplicationTheme);
            };

            //Ref: https://github.com/Kinnara/ModernWpf/blob/master/ModernWpf/Helpers/ColorsHelper.cs
            SystemEvents.UserPreferenceChanged += (s, e) => {
                if (e.Category == UserPreferenceCategory.General) {
                    if (userSettings.Settings.ApplicationTheme == AppTheme.Auto) {
                        ChangeTheme(AppTheme.Auto);
                    }
                }
            };

            this.SessionEnding += (s, e) => {
                if (e.ReasonSessionEnding == ReasonSessionEnding.Shutdown || e.ReasonSessionEnding == ReasonSessionEnding.Logoff) {
                    e.Cancel = true;
                    ShutDown();
                }
            };
            #endregion
        }

        private ServiceProvider ConfigureServices() {
            var provider = new ServiceCollection()
                .AddSingleton<IWallpaperControl, WallpaperControl>()
                .AddSingleton<IMonitorManager, MonitorManager>()
                .AddSingleton<IPlayback, Playback>()
                .AddSingleton<IScrControl, ScrControl>()

                .AddSingleton<IWallpaperFactory, WallpaperFactory>()
                .AddSingleton<IWallpaperConfigFolderFactory, WallpaperConfigFolderFactory>()

                .AddSingleton<JobService>()
                .AddSingleton<IUIRunnerService, UIRunnerService>()
                .AddSingleton<IUserSettingsService, UserSettingsService>()
                .AddSingleton<IAppUpdaterService, GithubUpdaterService>()
                .AddSingleton<IDownloadService, MultiDownloadService>()

                .AddSingleton<WallpaperControlServer>()
                .AddSingleton<MonitorManagerServer>()
                .AddSingleton<UserSettingServer>()
                .AddSingleton<AppUpdateServer>()
                .AddSingleton<CommandsServer>()
                .AddSingleton<ScrCommandsServer>()

                .AddSingleton<WndProcMsgWindow>()
                .AddSingleton<RawInputMsgWindow>()
                .AddSingleton<MainWindow>()
                .AddTransient<DebugLog>()

                .AddTransient<TrayCommand>()

                .BuildServiceProvider();

            return provider;
        }

        private NamedPipeServer ConfigureGrpcServer() {
            var server = new NamedPipeServer(Constants.CoreField.GrpcPipeServerName);

            Grpc_WallpaperControlService.BindService(server.ServiceBinder, _serviceProvider.GetRequiredService<WallpaperControlServer>());
            Grpc_MonitorManagerService.BindService(server.ServiceBinder, _serviceProvider.GetRequiredService<MonitorManagerServer>());
            Grpc_UserSettingsService.BindService(server.ServiceBinder, _serviceProvider.GetRequiredService<UserSettingServer>());
            Grpc_UpdateService.BindService(server.ServiceBinder, _serviceProvider.GetRequiredService<AppUpdateServer>());
            Grpc_CommandsService.BindService(server.ServiceBinder, _serviceProvider.GetRequiredService<CommandsServer>());
            Grpc_ScrCommandsService.BindService(server.ServiceBinder, _serviceProvider.GetRequiredService<ScrCommandsServer>());
            server.Start();

            return server;
        }

        private static void LogUnhandledException(Exception exception, string source)
            => Log.Error(exception, source);

        private void SetupUnhandledExceptionLogging() {
            // 当.NET应用程序域中的任何线程抛出了未捕获的异常时，会触发此事件。
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            // 对于WPF应用程序，如果UI线程（Dispatcher线程）上发生的未捕获异常，会触发此事件。
            Dispatcher.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");

            // 在异步编程中，如果一个Task（任务）完成了但其结果（无论是成功还是失败）未被观察（即没有使用await关键字等待或订阅Result属性），那么UnobservedTaskException事件会在垃圾回收时触发。
            //ref: https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception?redirectedfrom=MSDN&view=net-6.0
            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
        }

        public static void ChangeTheme(AppTheme theme) {
            try {
                theme = theme == AppTheme.Auto ? ThemeUtil.GetWindowsTheme() : theme;
                ApplicationTheme applicationTheme = theme == AppTheme.Light
                    ? ApplicationTheme.Light : ApplicationTheme.Dark;

                Application.Current.Dispatcher.Invoke(() => {
                    ApplicationThemeManager.Apply(applicationTheme, updateAccent: false);
                });
            }
            catch (Exception e) {
                Log.Error(e);
            }
            Log.Info($"Theme changed: {theme}");
        }

        public static void ChangeLanguage(string lang) {
            if (lang == string.Empty) return;

            Application.Current.Dispatcher.Invoke(() => {
                LanguageManager.Instance.ChangeLanguage(new(lang));
            });
        }

        private static AppUpdater? updateWindow;
        public static void AppUpdateDialog(Uri uri, string changelog) {
            updateNotify = false;
            if (updateWindow == null) {
                updateWindow = new AppUpdater(uri, changelog) {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                updateWindow.Closed += (s, e) => { updateWindow = null; };
                updateWindow.Show();
            }
        }

        private static int updateNotifyAmt = 1;
        private static bool updateNotify = false;

        private void AppUpdateChecked(object sender, AppUpdaterEventArgs e) {
            _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(delegate {
                if (e.UpdateStatus == AppUpdateStatus.Available) {
                    if (updateNotifyAmt > 0) {
                        updateNotifyAmt--;
                        updateNotify = true;
                        new ToastContentBuilder()
                            .AddText(LanguageManager.Instance["Find_New_Verison"])
                            .Show();
                    }

                    //If UI program already running then notification is displayed withing the it.
                    if (!Services.GetRequiredService<IUIRunnerService>().IsVisibleUI && updateNotify) {
                        AppUpdateDialog(e.UpdateUri, e.ChangeLog);
                    }
                }
                Log.Info($"AppUpdate status: {e.UpdateStatus}");
            }));
        }

        public static void ShutDown() {
            try {
                _ctsPlayback.Cancel();
                ((ServiceProvider)Services)?.Dispose();
                ((App)Current)._grpcServer?.Dispose();
                ToastNotificationManagerCompat.Uninstall();
            }
            catch (InvalidOperationException) { /* not initialised */ }

            Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
        }

        private readonly IServiceProvider _serviceProvider;
        private readonly Mutex _mutex = new(false, Constants.CoreField.UniqueAppUid);
        private readonly NamedPipeServer _grpcServer;
        private static readonly CancellationTokenSource _ctsPlayback = new();
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    }
}
