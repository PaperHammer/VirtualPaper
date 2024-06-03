using Newtonsoft.Json;
using System.IO;
using System.Windows;
using VirtualPaper.Common.Utils.IPC;

namespace VirtualPaper.ScreenSaver
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            try
            {
                string _tempWebView2Dir = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualPaper"), "ScrWebView2");
                if (!Directory.Exists(_tempWebView2Dir))
                {
                    Directory.CreateDirectory(_tempWebView2Dir);
                }
            }
            catch (Exception)
            {
                Environment.Exit(0);
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            this.SessionEnding += (s, a) =>
            {
                if (a.ReasonSessionEnding == ReasonSessionEnding.Shutdown || a.ReasonSessionEnding == ReasonSessionEnding.Logoff)
                {
                    a.Cancel = true;
                }
            };

            SetupUnhandledExceptionLogging();

            MainWindow wnd = new(e.Args);
            wnd.Show();
        }

        private void SetupUnhandledExceptionLogging()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            Dispatcher.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
        }

        private void LogUnhandledException(Exception exception, string source)
        {
            WriteToParent(new VirtualPaperMessageConsole()
            {
                MsgType = ConsoleMessageType.Error,
                Message = $"Unhandled Error: {exception.Message}",
            });
        }

        public static void WriteToParent(IpcMessage obj)
        {
            Console.WriteLine(JsonConvert.SerializeObject(obj));
        }
    }
}
