using CommandLine;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.PlayerWebView2;

namespace VirtualPaper.Webviewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(string[] args)
        {
            InitializeComponent();

            Parser.Default.ParseArguments<StartArgs>(args)
                .WithParsed((x) => _startArgs = x)
                .WithNotParsed(HandleParseError);
        }

        protected override async void OnContentRendered(EventArgs e) // 2
        {
            base.OnContentRendered(e);

            try
            {
                await InitializeWebView();
                App.WriteToParent(new VirtualPaperMessageHwnd()
                {
                    Hwnd = Webview2.Handle.ToInt32()
                });
            }
            finally
            {
                _ = StdInListener();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) // 1
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            //ShowInTaskbar = false : causing issue with windows10 Taskview.
            WindowUtil.RemoveWindowFromTaskbar(handle);
            //this hides the window from taskbar and also fixes crash when win10 taskview is launched. 
            this.ShowInTaskbar = false;
            this.ShowInTaskbar = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Webview2?.Dispose();
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
                            //When the redirected stream is closed, a null line is sent to the event handler. 
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
                                        case MessageType.cmd_initFilter:
                                            _ = ExecuteScriptFunctionAsync("virtualPaperInitFilter");
                                            break;
                                        case MessageType.cmd_apply:
                                            _ = ExecuteScriptFunctionAsync("applyFilter");
                                            _ = ExecuteScriptFunctionAsync("play");
                                            break;
                                        case MessageType.cmd_reload:
                                            Webview2?.Reload();
                                            break;
                                        case MessageType.cmd_suspend:
                                            //if (_startArgs.PauseEvent && !_isPaused)
                                            if (!_isPaused)
                                            {
                                                //TODO: check if js context ready
                                                _ = ExecuteScriptFunctionAsync("virtualPaperPlaybackChanged", true);
                                            }
                                            _isPaused = true;
                                            break;
                                        case MessageType.cmd_resume:
                                            if (_isPaused)
                                            {
                                                //if (_startArgs.PauseEvent)
                                                //{
                                                //TODO: check if js context ready
                                                _ = ExecuteScriptFunctionAsync("virtualPaperPlaybackChanged", false);
                                                //}
                                            }
                                            _isPaused = false;
                                            break;
                                        case MessageType.cmd_muted:
                                            var m = (VirtualPaperMuted)obj;
                                            _ = ExecuteScriptFunctionAsync("virtualPaperAudioMuteChanged", m.IsMuted);
                                            break;
                                        case MessageType.vp_slider:
                                            var sl = (VirtualPaperSlider)obj;
                                            _ = ExecuteScriptFunctionAsync("virtualPaperPropertyListener", sl.Name, sl.Value);
                                            break;
                                        case MessageType.vp_textbox:
                                            var tb = (VirtualPaperTextBox)obj;
                                            _ = ExecuteScriptFunctionAsync("virtualPaperPropertyListener", tb.Name, tb.Value);
                                            break;
                                        case MessageType.vp_dropdown:
                                            var dd = (VirtualPaperDropdown)obj;
                                            _ = ExecuteScriptFunctionAsync("virtualPaperPropertyListener", dd.Name, dd.Value);
                                            break;
                                        case MessageType.vp_cpicker:
                                            var cp = (VirtualPaperColorPicker)obj;
                                            _ = ExecuteScriptFunctionAsync("virtualPaperPropertyListener", cp.Name, cp.Value);
                                            break;
                                        case MessageType.vp_chekbox:
                                            var cb = (VirtualPaperCheckbox)obj;
                                            _ = ExecuteScriptFunctionAsync("virtualPaperPropertyListener", cb.Name, cb.Value);
                                            break;
                                        //case MessageType.vp_button:
                                        //    var btn = (VirtualPaperButton)obj;
                                        //    if (btn.IsDefault)
                                        //    {
                                        //        _ = RestoreWpCustomizeAsync(_startArgs.WpCustomizeFilePath);
                                        //    }
                                        //    else
                                        //    {
                                        //        _ = ExecuteScriptFunctionAsync("virtualPaperPropertyListener", btn.Name, true);
                                        //    }
                                        //    break;
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
                Webview2?.Dispose();                
                Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
            }
        }

        private async Task InitializeWebView()
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Constants.CommonPaths.TempWebView2Dir, _environmentOptions);
            await Webview2.EnsureCoreWebView2Async(env);

            Webview2.CoreWebView2.ProcessFailed += (s, e) =>
            {
                App.WriteToParent(new VirtualPaperMessageConsole()
                {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Process fail: {e.Reason}",
                });
            };

            //Webview2.CoreWebView2.OpenDevToolsWindow();

            Webview2.NavigationCompleted += Webview2_NavigationCompleted;

            Webview2.CoreWebView2.Navigate(Path.Combine(_startArgs.WorkingDir, "Web", "viewer.html"));
        }

        private async void Webview2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            await LoadSourceAsync(_startArgs.FilePath);
            await RestoreWpCustomizeAsync(_startArgs.WpCustomizeFilePath);

            App.WriteToParent(new VirtualPaperMessageWallpaperLoaded() { Success = true });
        }

        private async Task LoadSourceAsync(string filePath)
        {
            try
            {
                if (filePath == null) return;

                await ExecuteScriptFunctionAsync("virtualPaperSourceReload", _startArgs.WallpaperType, filePath);
            }
            catch { }
        }

        private async Task RestoreWpCustomizeAsync(string wpCustomizeFilePath)
        {
            try
            {
                if (wpCustomizeFilePath == null) return;

                await ExecuteScriptFunctionAsync("virtualPaperInitFilter");

                foreach (var item in JsonUtil.ReadJObject(wpCustomizeFilePath))
                {
                    string uiElementType = item.Value["Type"].ToString();
                    if (!uiElementType.Equals("Button", StringComparison.OrdinalIgnoreCase) && !uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase))
                    {
                        if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase) ||
                            uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase))
                        {
                            await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", item.Key, (string)item.Value["Value"]);
                        }
                        else if (uiElementType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase))
                        {
                            await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", item.Key, (bool)item.Value["Value"]);
                        }
                        else if (uiElementType.Equals("Color", StringComparison.OrdinalIgnoreCase) || uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase))
                        {
                            await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", item.Key, (string)item.Value["Value"]);
                        }
                    }
                }

                await ExecuteScriptFunctionAsync("applyFilter");
                await ExecuteScriptFunctionAsync("play");
            }
            catch { }
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

        //credit: https://stackoverflow.com/questions/62835549/equivalent-of-webbrowser-invokescriptstring-object-in-webview2
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

            //App.WriteToParent(new VirtualPaperMessageConsole()
            //{
            //    MsgType = ConsoleMessageType.Log,
            //    Message = script.ToString(),
            //});

            string res = await Webview2.ExecuteScriptAsync(script.ToString());

            return res;
        }

        private readonly CoreWebView2EnvironmentOptions _environmentOptions = new()
        {
            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1"
        }; // workaround: avoid cache
        private StartArgs _startArgs;
        private bool _isPaused = false;
    }
}