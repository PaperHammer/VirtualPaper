using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using CommandLine;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.ScreenSaver.Effects;
using VirtualPaper.ScreenSaver.Utils;

namespace VirtualPaper.ScreenSaver {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow(string[] args) {
            Mouse.OverrideCursor = Cursors.None;

            InitializeComponent();

            _taskManagerListener = new TaskManagerListener();

            Parser.Default.ParseArguments<StartArgs>(args)
                .WithParsed((x) => _startArgs = x)
                .WithNotParsed(HandleParseError);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            App.ShutDown();
        }

        protected override async void OnContentRendered(EventArgs e) {
            base.OnContentRendered(e);

            try {
                await InitializeWebView();
            }
            catch {
                App.ShutDown();
            }
            finally {
                _taskManagerListener.StartListening();
                _ = StdInListener().ConfigureAwait(false);
            }
        }

        public async Task StdInListener() {
            try {
                await Task.Run(async () => {
                    while (true) {
                        var msg = await Console.In.ReadLineAsync();
                        if (string.IsNullOrEmpty(msg)) {
#if !DEBUG
                            break;
#endif
                        }
                        else {
                            try {
                                var close = false;
                                var obj = JsonSerializer.Deserialize<IpcMessage>(msg);

                                this.Dispatcher.Invoke(() => {
                                    switch (obj.Type) {
                                        case MessageType.cmd_close:
                                            close = true;
                                            break;
                                    }
                                });

                                if (close) break;
                            }
                            catch (Exception ie) {
                                App.WriteToParent(new VirtualPaperMessageConsole() {
                                    MsgType = ConsoleMessageType.Error,
                                    Message = $"Ipc action Error: {ie.Message}"
                                });
                            }
                        }
                    }
                });
            }
            catch (Exception e) {
                App.WriteToParent(new VirtualPaperMessageConsole() {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Ipc stdin Error: {e.Message}",
                });
            }
            finally {
                App.ShutDown();
            }
        }

        private async Task InitializeWebView() {
            var env = await CoreWebView2Environment.CreateAsync(null, Constants.CommonPaths.TempScrWebView2Dir, _environmentOptions);
            await Webview2.EnsureCoreWebView2Async(env);

            Webview2.CoreWebView2.ProcessFailed += (s, e) => {
                App.ShutDown();
            };

            //Webview.CoreWebView2.OpenDevToolsWindow();

            Webview2.NavigationCompleted += Webview2_NavigationCompleted;

            Webview2.CoreWebView2.Navigate(
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Players",
                    Constants.PlayingFile.PlayerWeb));
        }

        private async void Webview2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) {
            await LoadSourceAsync();

            App.WriteToParent(new VirtualPaperMessageWallpaperLoaded() { Success = true });

            InitEffect();
        }

        private void InitEffect() {
            string effect = _startArgs.DynamicEffect;
            switch (effect) {
                case "Bubble":
                    Bubble bubble = new(this, canvas);
                    bubble.Start();
                    App.WriteToParent(new VirtualPaperMessageConsole() {
                        MsgType = ConsoleMessageType.Log,
                        Message = "Scr-Effect was started: Bubble",
                    });
                    break;
                default:
                    break;
            }
        }

        private async Task LoadSourceAsync() {
            try {
                await ExecuteScriptFunctionAsync("resourceLoad",
                    _startArgs.WallpaperType,
                    _startArgs.FilePath);
                await ExecuteScriptFunctionAsync("play");
            }
            catch {
                App.ShutDown();
            }
        }

        private void HandleParseError(IEnumerable<Error> errs) {
            App.WriteToParent(new VirtualPaperMessageConsole() {
                MsgType = ConsoleMessageType.Error,
                Message = $"Error parsing cmdline args: {errs.First()}",
            });
            App.ShutDown();
        }

        private async Task<string> ExecuteScriptFunctionAsync(string functionName, params object[] parameters) {
            StringBuilder sb_script = new();
            sb_script.Append(functionName);
            sb_script.Append('(');
            for (int i = 0; i < parameters.Length; i++) {
                sb_script.Append(JsonSerializer.Serialize(parameters[i]));
                if (i < parameters.Length - 1) {
                    sb_script.Append(", ");
                }
            }
            sb_script.Append(");");

            string cmd = sb_script.ToString();
            string script = string.Empty;
            await Application.Current.Dispatcher.Invoke(async () => {
                script = await Webview2.ExecuteScriptAsync(cmd);
            });

            App.WriteToParent(new VirtualPaperMessageConsole() {
                MsgType = ConsoleMessageType.Log,
                Message = $"{cmd} {script}"
            });

            return script;
        }

        private readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1"
        };
        private StartArgs _startArgs;
        private readonly TaskManagerListener _taskManagerListener;
    }
}