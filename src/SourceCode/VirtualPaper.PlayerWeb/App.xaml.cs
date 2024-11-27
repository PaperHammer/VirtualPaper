using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.UIComponent.Utils;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb {
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application {
        public static App AppInstance { get; private set; }
        public static MainWindow MainWindowInstance { get; private set; }

        private event SessionEndingEventHandler SessionEnding;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App() {
            AppInstance = this;

            this.InitializeComponent();

            SessionEnding += App_SessionEnding;
            SetupUnhandledExceptionLogging();

            ////string s = "a --file-path C:\\Users\\PaperHammer\\Desktop\\1P.mp4 --effect-file-path-using D:\\_%TEMP2\\wallpapers\\of14a2lw.buk\\1\\wpEffectFilePathUsing.json --effect-file-path-temporary D:\\_%TEMP2\\wallpapers\\of14a2lw.buk\\wpEffectFilePathTemporary.json --effect-file-path-template D:\\_%TEMP2\\wallpapers\\of14a2lw.buk\\wpEffectFilePathTemplate.json --runtime-type RVideo --is-preview True --window-style-type Default --app-theme Light --app-language zh-CN";
            ////string s = "a --file-path D:\\_%TEMP2\\wallpapers\\of14a2lw.buk\\of14a2lw.buk.jpg --effect-file-path-using D:\\_%TEMP2\\wallpapers\\of14a2lw.buk\\1\\wpEffectFilePathUsing.json --effect-file-path-temporary D:\\_%TEMP2\\wallpapers\\of14a2lw.buk\\wpEffectFilePathTemporary.json --effect-file-path-template D:\\_%TEMP2\\wallpapers\\of14a2lw.buk\\wpEffectFilePathTemplate.json --runtime-type RImage --is-preview True --window-style-type Default --app-theme Light --app-language zh-CN";
            //string s = "a  --file-path D:\\ProgramDemos\\VSCodeDemos\\Temp\\WEB\\3d\\Images\\img29.jpg --depth-file-path D:\\ProgramDemos\\VSCodeDemos\\Temp\\WEB\\3d\\Images\\_img29.jpg --effect-file-path-using D:\\_%TEMP2\\wallpapers\\of14a2lw.buk\\1\\wpEffectFilePathUsing.json --effect-file-path-temporary D:\\_%TEMP2\\wallpapers\\of14a2lw.buk\\wpEffectFilePathTemporary.json --effect-file-path-template D:\\_%TEMP2\\wallpapers\\of14a2lw.buk\\wpEffectFilePathTemplate.json --runtime-type RImage3D --is-preview True --window-style-type Default --app-theme Light --app-language zh-CN";
            //string[] startArgs = s.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1..];
            string[] startArgs = Environment.GetCommandLineArgs()[1..];

            Parser.Default.ParseArguments<StartArgs>(startArgs)
                .WithParsed((x) => _startArgs = x)
                .WithNotParsed(HandleParseError);
            foreach (string arg in startArgs) {
                if (arg == null || arg == string.Empty) {
                    throw new NoNullAllowedException(nameof(StartArgs));
                }
            }

            SetAppTheme(_startArgs.ApplicationTheme);
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs args) {
            // ref: https://github.com/microsoft/WindowsAppSDK/issues/1687
            //ApplicationLanguages.PrimaryLanguageOverride = _userSettingsClient.Settings.Language;

            // ref: https://github.com/AndrewKeepCoding/WinUI3Localizer
            if (Constants.ApplicationType.IsMSIX) {
                await LanguageUtil.InitializeLocalizerForPackaged(_startArgs.Language);
            }
            else {
                await LanguageUtil.InitializeLocalizerForUnpackaged(_startArgs.Language);
            }

            // 避免文字无法初始化
            MainWindowInstance = new MainWindow(_startArgs);
            MainWindowInstance.Show();
        }

        /// <summary>
        /// 处理会话结束
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void App_SessionEnding(object sender, SessionEndingEventArgs e) {
            if (e.Reason == SessionEndReasons.Logoff || e.Reason == SessionEndReasons.SystemShutdown) {
                e.Cancel = true;
            }
        }

        private void HandleParseError(IEnumerable<Error> errs) {
            App.WriteToParent(new VirtualPaperMessageConsole() {
                MsgType = ConsoleMessageType.Error,
                Message = $"Error parsing cmdline args: {errs.First()}",
            });
            App.AppInstance.Exit();
        }

        private void SetupUnhandledExceptionLogging() {
            UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception, "Global UnhandledException");

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
        }

        private void LogUnhandledException(Exception ex, string source) {
            WriteToParent(new VirtualPaperMessageConsole() {
                MsgType = ConsoleMessageType.Error,
                Message = $"Unhandled Error: {ex}",
            });
            App.Current.Exit();

#if DEBUG
            Debug.WriteLine(ex);
#endif
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

        public static void WriteToParent(IpcMessage obj) {
            string msg = JsonSerializer.Serialize(obj);
            Console.WriteLine(msg);

#if DEBUG
            Debug.WriteLine(msg);
#endif
        }

        private readonly IServiceProvider _serviceProvider;
        private StartArgs _startArgs;
    }
}
