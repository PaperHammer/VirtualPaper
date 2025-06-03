using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.PlayerWeb.Utils;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb {
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application {
        public static App AppInstance { get; private set; }
        public static MainWindow MainWindowInstance { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App() {
            AppInstance = this;
            this.InitializeComponent();
            SetupUnhandledExceptionLogging();

            //string s = "a " +
            //    "--is-preview " +
            //    "--left 0 " +
            //    "--top 0 " +
            //    "--right 2560 " +
            //    "--bottom 1600 " +
            //    "-f C:\\Users\\PaperHammer\\AppData\\Local\\VirtualPaper\\temp\\4dfj1bgy.zl5\\4dfj1bgy.zl5.JPG " +
            //    "-b C:\\Users\\PaperHammer\\AppData\\Local\\VirtualPaper\\temp\\4dfj1bgy.zl5\\wp_metadata_basic.json " +
            //    "-e C:\\Users\\PaperHammer\\AppData\\Local\\VirtualPaper\\temp\\4dfj1bgy.zl5\\1\\RImage\\wpEffectFilePathUsing.json " +
            //    "--effect-file-path-temporary C:\\Users\\PaperHammer\\AppData\\Local\\VirtualPaper\\temp\\4dfj1bgy.zl5\\1\\RImage\\wpEffectFilePathTemporary.json " +
            //    "--effect-file-path-template C:\\Users\\PaperHammer\\AppData\\Local\\VirtualPaper\\temp\\4dfj1bgy.zl5\\wpEffectFilePathTemplate.json " +
            //    "-r RImage " +
            //    "--system-backdrop Default " +
            //    "-T Light " +
            //    "-l zh-CN";
            //string[] startArgs = s.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1..];

            string[] startArgs = Environment.GetCommandLineArgs()[1..];
            Parser.Default.ParseArguments<StartArgs>(startArgs)
                .WithParsed((x) => _startArgs = x)
                .WithNotParsed(HandleParseError);
            if (_startArgs == null) {
                throw new NoNullAllowedException(nameof(StartArgs));
            }

            SetAppTheme(_startArgs.ApplicationTheme);
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs args) {
            // ref: https://github.com/AndrewKeepCoding/WinUI3Localizer
            if (Constants.ApplicationType.IsMSIX) {
                await LanguageUtil.InitializeLocalizerForPackaged(_startArgs.Language);
            }
            else {
                await LanguageUtil.InitializeLocalizerForUnpackaged(_startArgs.Language);
            }

            // 避免文字无法初始化
            MainWindowInstance = new MainWindow(_startArgs);
            if (!_startArgs.IsPreview) {
                WindowUtil.InitWindowAsBackground();
            }
            MainWindowInstance.Activate();
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
            string msg = JsonSerializer.Serialize(obj, IpcMessageContext.Default.IpcMessage);
            Console.WriteLine(msg);
        }

        private StartArgs _startArgs;
    }
}
