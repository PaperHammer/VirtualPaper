using System.Data;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.IPC;

namespace VirtualPaper.PlayerWeb {
    internal static class Program {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            // ERROR_FILE_NOT_FOUND
            // Ref: <https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499->
            if (!IsWebView2Available())
                Environment.Exit(2);

            SetupUnhandledExceptionLogging();

            //string _args = "{\"isPreview\":false,\"filePath\":\"C:\\\\Users\\\\PaperHammer\\\\AppData\\\\Local\\\\VirtualPaper\\\\Library\\\\wallpapers\\\\onfetinv.r41\\\\onfetinv.r41.jpg\",\"depthFilePath\":null,\"wpBasicDataFilePath\":\"C:\\\\Users\\\\PaperHammer\\\\AppData\\\\Local\\\\VirtualPaper\\\\Library\\\\wallpapers\\\\onfetinv.r41\\\\wp_metadata_basic.json\",\"wpEffectFilePathUsing\":\"C:\\\\Users\\\\PaperHammer\\\\AppData\\\\Local\\\\VirtualPaper\\\\Library\\\\wallpapers\\\\onfetinv.r41\\\\1\\\\RImage\\\\wpEffectFilePathUsing.json\",\"wpEffectFilePathTemporary\":\"C:\\\\Users\\\\PaperHammer\\\\AppData\\\\Local\\\\VirtualPaper\\\\Library\\\\wallpapers\\\\onfetinv.r41\\\\1\\\\RImage\\\\wpEffectFilePathTemporary.json\",\"wpEffectFilePathTemplate\":\"C:\\\\Users\\\\PaperHammer\\\\AppData\\\\Local\\\\VirtualPaper\\\\Library\\\\wallpapers\\\\onfetinv.r41\\\\wpEffectFilePathTemplate.json\",\"runtimeType\":\"RImage\",\"systemBackdrop\":0,\"applicationTheme\":2,\"language\":\"en-US\"}";

            string? msg;
            using (var reader = new StreamReader(Console.OpenStandardInput())) {
                msg = reader.ReadLine();
            }
            if (msg == null) {
                throw new NoNullAllowedException($"The argument for {nameof(msg)} is null. Please check the command msg arguments.");
            }

            _startArgs = JsonSerializer.Deserialize<StartArgsWeb>(msg);
            if (_startArgs == null) {
                throw new NoNullAllowedException($"The argument for {nameof(StartArgsWeb)} is null. Please check the command msg arguments.");
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1(_startArgs));
        }

        private static bool IsWebView2Available() {
            try {
                return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
            }
            catch (Exception) {
                return false;
            }
        }

        private static void LogUnhandledException(Exception exception) {
            WriteToParent(new VirtualPaperMessageConsole() {
                MsgType = ConsoleMessageType.Error,
                Message = $"Unhandled Error: {exception}",
            });
        }

        //Not working..
        //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/5221
        private static void SetupUnhandledExceptionLogging() {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject);

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception);
        }

        public static void WriteToParent(IpcMessage message) {
            try {
                string msg = JsonSerializer.Serialize(message, IpcMessageContext.Default.IpcMessage);
                Console.WriteLine(msg);
                Console.Out.Flush();
            }
            catch (Exception e) {
                ArcLog.GetLogger<Form1>().Error(e);
            }
        }

        private static StartArgsWeb? _startArgs;
    }
}