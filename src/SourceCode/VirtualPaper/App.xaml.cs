using GrpcDotNetNamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NLog;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using VirtualPaper.Common;
using VirtualPaper.Common.Models;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Cores.Desktop;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.PlaybackControl;
using VirtualPaper.Cores.Tray;
using VirtualPaper.Factories;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Grpc.Service.Commands;
using VirtualPaper.Grpc.Service.MonitorManager;
using VirtualPaper.Grpc.Service.Update;
using VirtualPaper.Grpc.Service.UserSetting;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.GrpcServers;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services;
using VirtualPaper.Services.Download;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils.Theme;
using VirtualPaper.Views;
using VirtualPaper.Views.WindowsMsg;
using Application = System.Windows.Application;
using AppTheme = VirtualPaper.Common.AppTheme;
using MessageBox = System.Windows.MessageBox;

namespace VirtualPaper
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider Services
        {
            get
            {
                IServiceProvider serviceProvider = ((App)Current)._serviceProvider;
                return serviceProvider ?? throw new InvalidOperationException("The service provider is not initialized");
            }
        }

        public App()
        {
            #region 唯一实例检查
            try
            {
                // 保证全局只有一个实例
                if (!_mutex.WaitOne(TimeSpan.FromSeconds(1), false))
                {
                    MessageBox.Show("已存在正在运行的程序，请检查托盘或任务管理器\nThere are already running programs, check the tray or Task Manager", "Virtual Paper", MessageBoxButton.OK, MessageBoxImage.Information);
                    ShutDown();
                    return;
                }
            }
            catch (AbandonedMutexException e)
            {
                //unexpected app termination.
                Debug.WriteLine(e.Message);
            }
            #endregion

            SetupUnhandledExceptionLogging(); // 初始化异常处理机制
            _logger.Info(LogUtil.GetHardwareInfo()); // 记录硬件信息

            #region 必要路径处理
            try
            {
                // 清空缓存
                FileUtil.EmptyDirectory(Constants.CommonPaths.TempDir);
            }
            catch { }

            try
            {
                // 创建必要目录, eg: C:\Users\<User>\AppData\Local
                Directory.CreateDirectory(Constants.CommonPaths.AppDataDir);
                Directory.CreateDirectory(Constants.CommonPaths.LogDir);
                Directory.CreateDirectory(Constants.CommonPaths.TempDir);
                Directory.CreateDirectory(Path.Combine(Constants.CommonPaths.TempDir, Constants.CommonPartialPaths.WallpaperInstallDir));
            }
            catch (Exception ex)
            {
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
            var userSetting = Services.GetRequiredService<IUserSettingsService>();
            if (!FileUtil.IsValidFolderPath(userSetting.Settings.WallpaperDir))
                userSetting.Settings.WallpaperDir = Constants.CommonPaths.WpStoreDir;
            CreateWallpaperDir(userSetting.Settings.WallpaperDir);
            // 初始化语言包
            ChangeLanguage(userSetting.Settings.Language);

            userSetting.Save<ISettings>();

            #region 启动相关后台服务
            // 启动针对从 Windows 发出的到该窗口的消息监听服务
            Services.GetRequiredService<WndProcMsgWindow>().Show();
            // 启动针对从外部设备发出的到该窗口的消息监听服务
            Services.GetRequiredService<RawInputMsgWindow>().Show();
            // 启动壁纸行为/状态监听服务
            Services.GetRequiredService<IPlayback>().Start();
            // 启动托盘（后台）服务
            Services.GetRequiredService<ISystray>();
            #endregion

            if (userSetting.Settings.IsUpdated || userSetting.Settings.IsFirstRun)
            {
                SplashWindow? spl = userSetting.Settings.IsFirstRun ? new(0, 500) : null; spl?.Show();
                spl?.Close();
            }

            //restore wallpaper(s) from previous run.
            Services.GetRequiredService<IWallpaperControl>().RestoreWallpaper();

            //first run Setup-Wizard show..
            if (userSetting.Settings.IsFirstRun)
            {
                Services.GetRequiredService<IUIRunnerService>().ShowUI();
            }

            #region 事件绑定
            //need to load theme later stage of startu to update..
            this.Startup += (s, e) =>
            {
                ChangeTheme(userSetting.Settings.ApplicationTheme);                
            };

            //Ref: https://github.com/Kinnara/ModernWpf/blob/master/ModernWpf/Helpers/ColorsHelper.cs
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General)
                {
                    if (userSetting.Settings.ApplicationTheme == AppTheme.Auto)
                    {
                        ChangeTheme(AppTheme.Auto);
                    }
                }
            };

            this.SessionEnding += (s, e) =>
            {
                if (e.ReasonSessionEnding == ReasonSessionEnding.Shutdown || e.ReasonSessionEnding == ReasonSessionEnding.Logoff)
                {
                    e.Cancel = true;
                    ShutDown();
                }
            };
            #endregion
        }

        private ServiceProvider ConfigureServices()
        {
            var provider = new ServiceCollection()
                .AddSingleton<IWallpaperControl, WallpaperControl>()
                .AddSingleton<IMonitorManager, MonitorManager>()
                .AddSingleton<IPlayback, Playback>()
                .AddSingleton<ISystray, Systray>()

                .AddSingleton<IWallpaperFactory, WallpaperFactory>()
                .AddSingleton<IWallpaperConfigFolderFactory, WallpaperConfigFolderFactory>()

                .AddSingleton<IUIRunnerService, UIRunnerService>()
                .AddSingleton<IUserSettingsService, UserSettingsService>()
                .AddSingleton<IWatchdogService, WatchdogService>()
                .AddSingleton<IAppUpdaterService, GithubUpdaterService>()
                .AddSingleton<IDownloadService, MultiDownloadService>()

                .AddSingleton<WallpaperControlServer>()
                .AddSingleton<MonitorManagerServer>()
                .AddSingleton<UserSettingServer>()
                .AddSingleton<AppUpdateServer>()
                .AddSingleton<CommandsServer>()

                .AddSingleton<WndProcMsgWindow>()
                .AddSingleton<RawInputMsgWindow>()

                .BuildServiceProvider();

            return provider;
        }

        private NamedPipeServer ConfigureGrpcServer()
        {
            var server = new NamedPipeServer(Constants.SingleInstance.GrpcPipeServerName);

            WallpaperControlService.BindService(server.ServiceBinder, _serviceProvider.GetRequiredService<WallpaperControlServer>());
            MonitorManagerService.BindService(server.ServiceBinder, _serviceProvider.GetRequiredService<MonitorManagerServer>());
            UserSettingService.BindService(server.ServiceBinder, _serviceProvider.GetRequiredService<UserSettingServer>());
            UpdateService.BindService(server.ServiceBinder, _serviceProvider.GetRequiredService<AppUpdateServer>());
            CommandsService.BindService(server.ServiceBinder, _serviceProvider.GetRequiredService<CommandsServer>());
            server.Start();

            return server;
        }

        private void CreateWallpaperDir(string baseDir)
        {
            Directory.CreateDirectory(Path.Combine(baseDir, Constants.CommonPartialPaths.WallpaperInstallDir));
        }

        private void LogUnhandledException(Exception exception, string source) => _logger.Error(exception);
        private void SetupUnhandledExceptionLogging()
        {
            // 当.NET应用程序域中的任何线程抛出了未捕获的异常时，会触发此事件。
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            // 对于WPF应用程序，如果UI线程（Dispatcher线程）上发生的未捕获异常，会触发此事件。
            Dispatcher.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");

            // 在异步编程中，如果一个Task（任务）完成了但其结果（无论是成功还是失败）未被观察（即没有使用await关键字等待或订阅Result属性），那么UnobservedTaskException事件会在垃圾回收时触发。
            //ref: https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception?redirectedfrom=MSDN&view=net-6.0
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
            };
        }

        private static AppTheme _currentTheme = AppTheme.Dark;
        public static void ChangeTheme(AppTheme theme)
        {
            theme = theme == AppTheme.Auto ? ThemeUtil.GetWindowsTheme() : theme;
            if (_currentTheme == theme)
                return;

            _currentTheme = theme;
            Uri uri = theme switch
            {
                AppTheme.Light => new Uri("Themes/Light.xaml", UriKind.Relative),
                AppTheme.Dark => new Uri("Themes/Dark.xaml", UriKind.Relative),
                _ => new Uri("Themes/Dark.xaml", UriKind.Relative)
            };

            try
            {
                ResourceDictionary resourceDict = Application.LoadComponent(uri) as ResourceDictionary;
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(resourceDict);
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
            _logger.Info($"Theme changed: {theme}");
        }

        private static string _lang = string.Empty;
        public static void ChangeLanguage(string lang)
        {
            if (lang == string.Empty || _lang == lang) return;

            ResourceDictionary? langRd = null;
            try
            {
                //根据名字载入语言文件
                langRd = Application.LoadComponent(new Uri(@"lang\" + lang + ".xaml", UriKind.Relative)) as ResourceDictionary;
            }
            catch (Exception e2)
            {
                MessageBox.Show(e2.Message);
            }

            if (langRd != null)
            {
                _resourceDic = Application.Current.Resources;
                //如果已使用其他语言,先清空
                if (_resourceDic.MergedDictionaries.Count > 0)
                {
                    _resourceDic.MergedDictionaries.Clear();
                }
                _resourceDic.MergedDictionaries.Add(langRd);
                _lang = lang;
            }
        }

        private static AppUpdater updateWindow;
        public static void AppUpdateDialog(Uri uri, string changelog)
        {
            updateNotify = false;
            if (updateWindow == null)
            {
                updateWindow = new AppUpdater(uri, changelog)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                updateWindow.Closed += (s, e) => { updateWindow = null; };
                updateWindow.Show();
            }
        }

        private static int updateNotifyAmt = 1;
        private static bool updateNotify = false;
        private void AppUpdateChecked(object sender, AppUpdaterEventArgs e)
        {
            var sysTray = Services.GetRequiredService<ISystray>();
            _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                if (e.UpdateStatus == AppUpdateStatus.available)
                {
                    if (updateNotifyAmt > 0)
                    {
                        updateNotifyAmt--;
                        updateNotify = true;
                        sysTray?.ShowBalloonNotification(4000,
                            "Virtual Paper",
                            GetResourceDicString("Find_New_Verison"));
                    }

                    //If UI program already running then notification is displayed withing the it.
                    if (!Services.GetRequiredService<IUIRunnerService>().IsVisibleUI && updateNotify)
                    {
                        AppUpdateDialog(e.UpdateUri, e.ChangeLog);
                    }
                }
                _logger.Info($"AppUpdate status: {e.UpdateStatus}");
            }));
        }

        public static string GetResourceDicString(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? "";
        }

        public static void ShutDown()
        {
            try
            {
                ((ServiceProvider)Services)?.Dispose();
                ((App)Current)._grpcServer?.Dispose();
            }
            catch (InvalidOperationException) { /* not initialised */ }

            Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
        }

        private readonly IServiceProvider _serviceProvider;
        private readonly Mutex _mutex = new(false, Constants.SingleInstance.UniqueAppUid);
        private readonly NamedPipeServer _grpcServer;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private static ResourceDictionary _resourceDic = [];
    }
}
