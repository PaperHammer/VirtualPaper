using CommandLine;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.ScreenSaver.Effects;

namespace VirtualPaper.ScreenSaver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(string[] args)
        {
            Mouse.OverrideCursor = Cursors.None;

            InitializeComponent();

            Parser.Default.ParseArguments<StartArgs>(args)
                .WithParsed((x) => _startArgs = x)
                .WithNotParsed(HandleParseError);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Webview?.Dispose();
            Application.Current.Shutdown();
        }

        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            try
            {
                await InitializeWebView();
            }
            catch
            {
                Application.Current.Shutdown();
            }
            finally
            {
                _ = StdInListener();
            }
        }

        public async Task StdInListener()
        {
            try
            {
                await Task.Run(async () =>
                {
                    while (true)
                    {
                        var msg = await Console.In.ReadLineAsync();
                        if (string.IsNullOrEmpty(msg))
                        {
                            break;
                        }
                        else
                        {
                            try
                            {
                                var close = false;
                                var obj = JsonConvert.DeserializeObject<IpcMessage>(msg, new JsonSerializerSettings() { Converters = { new IpcMessageConverter() } });

                                this.Dispatcher.Invoke(() =>
                                {
                                    switch (obj.Type)
                                    {
                                        case MessageType.cmd_close:
                                            close = true;
                                            break;
                                    }
                                });

                                if (close) break;
                            }
                            catch (Exception ie)
                            {
                                App.WriteToParent(new VirtualPaperMessageConsole()
                                {
                                    MsgType = ConsoleMessageType.Error,
                                    Message = $"Ipc action Error: {ie.Message}"
                                });
                            }
                        }
                    }
                });
            }
            catch (Exception e)
            {
                App.WriteToParent(new VirtualPaperMessageConsole()
                {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Ipc stdin Error: {e.Message}",
                });
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
            }
        }

        private async Task InitializeWebView()
        {
            var env = await CoreWebView2Environment.CreateAsync(null, _tempWebView2Dir, _environmentOptions);
            await Webview.EnsureCoreWebView2Async(env);

            Webview.CoreWebView2.ProcessFailed += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
            };

            //Webview.CoreWebView2.OpenDevToolsWindow();

            Webview.NavigationCompleted += Webview2_NavigationCompleted;

            Webview.CoreWebView2.Navigate(_workingFile);
        }

        private async void Webview2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            await LoadSourceAsync();

            App.WriteToParent(new VirtualPaperMessageWallpaperLoaded() { Success = true });

            InitEffect();
        }

        private void InitEffect()
        {
            string effect = _startArgs.DynamicEffect;
            switch (effect)
            {
                case "Bubble":
                    Bubble bubble = new(this, canvas);
                    bubble.Start();
                    App.WriteToParent(new VirtualPaperMessageConsole()
                    {
                        MsgType = ConsoleMessageType.Log,
                        Message = "Scr-Effect was started: Bubble",
                    });
                    break;
                default:
                    break;
            }
        }

        private async Task LoadSourceAsync()
        {
            try
            {
                await ExecuteScriptFunctionAsync("virtualPaperSourceReload",
                    _startArgs.WallpaperType,
                    _startArgs.FilePath);
                await ExecuteScriptFunctionAsync("play");
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
            }
        }

        private void HandleParseError(IEnumerable<Error> errs)
        {
            App.WriteToParent(new VirtualPaperMessageConsole()
            {
                MsgType = ConsoleMessageType.Error,
                Message = $"Error parsing cmdline args: {errs.First()}",
            });
            Application.Current.Shutdown();
        }

        private async Task<string> ExecuteScriptFunctionAsync(string functionName, params object[] parameters)
        {
            var script = new StringBuilder();
            script.Append(functionName);
            script.Append('(');
            for (int i = 0; i < parameters.Length; i++)
            {
                script.Append(JsonConvert.SerializeObject(parameters[i]));
                if (i < parameters.Length - 1)
                {
                    script.Append(", ");
                }
            }
            script.Append(");");

            string res = await Webview.ExecuteScriptAsync(script.ToString());

            return res;
        }

        private readonly CoreWebView2EnvironmentOptions _environmentOptions = new()
        {
            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1"
        };
        public static string AppDataDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualPaper");
        private readonly string _tempWebView2Dir = Path.Combine(AppDataDir, "ScrWebView2");
        private readonly string _workingFile = Path.Combine(AppDataDir, "ScrSaver", "scr.html");
        private StartArgs _startArgs;
    }
}