using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using VirtualPaper.Common.Utils.Files.Models;
using VirtualPaper.Common.Utils.IPC;

namespace VirtualPaper.Webviewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            this.SessionEnding += (s, a) =>
            {
                if (a.ReasonSessionEnding == ReasonSessionEnding.Shutdown || a.ReasonSessionEnding == ReasonSessionEnding.Logoff)
                {
                    //Wallpaper core will handle the shutdown.
                    a.Cancel = true;
                }
            };

            //string filePath = "C:\\Users\\PaperHammer\\AppData\\Local\\VirtualPaper\\Library\\wallpapers\\n1j43ben.crv\\02inb0tr.oue.jpg";
            //string customizePath = "C:\\Users\\PaperHammer\\AppData\\Local\\VirtualPaper\\Library\\wallpapers\\n1j43ben.crv\\WpCustomize.json";

            //DirectoryInfo path = new(AppDomain.CurrentDomain.BaseDirectory);
            //string workingDir = path.Parent.Parent.FullName;
            //workingDir = Path.Combine(workingDir, "Plugins", "Webviewer");

            //StringBuilder cmdArgs = new();
            //cmdArgs.Append($" --working-dir {workingDir}");
            ////cmdArgs.Append(" --working-dir " + "\"" + workingDir + "\"");
            //cmdArgs.Append($" --file-path {filePath}");
            //cmdArgs.Append(" --wallpaper-type " + "picture");
            //cmdArgs.Append($" --customize-file-path {customizePath}");

            //string[] pars = cmdArgs.ToString().Split(" ");

            SetupUnhandledExceptionLogging();

            MainWindow wnd = new(e.Args);
            //MainWindow wnd = new(pars);
            wnd.Show();
        }

        //protected override void OnStartup(StartupEventArgs e)
        //{
        //    base.OnStartup(e);

        //    if (e.Args.Length > 1)
        //    {
        //        MainWindow mainWindow = new(e.Args);
        //        mainWindow.Show();
        //    }
        //}

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
