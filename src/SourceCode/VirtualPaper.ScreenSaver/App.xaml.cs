using System.Text.Json;
using System.Windows;
using VirtualPaper.Common.Utils.IPC;

namespace VirtualPaper.ScreenSaver {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        private void Application_Startup(object sender, StartupEventArgs e) {
            this.SessionEnding += (s, a) => {
                if (a.ReasonSessionEnding == ReasonSessionEnding.Shutdown || a.ReasonSessionEnding == ReasonSessionEnding.Logoff) {
                    a.Cancel = true;
                }
            };

            SetupUnhandledExceptionLogging();

            MainWindow wnd = new(e.Args);
            wnd.Show();
        }

        private void SetupUnhandledExceptionLogging() {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            Dispatcher.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
        }

        private void LogUnhandledException(Exception exception, string source) {
            WriteToParent(new VirtualPaperMessageConsole() {
                MsgType = ConsoleMessageType.Error,
                Message = $"Unhandled Error: {exception.Message}",
            });
        }

        public static void WriteToParent(IpcMessage obj) {
            Console.WriteLine(JsonSerializer.Serialize(obj));
        }

        public static void ShutDown() {
            Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
        }
    }
}
