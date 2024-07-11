using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;

namespace VirtualPaper.Views.Preview
{
    /// <summary>
    /// WebPreviewer.xaml 的交互逻辑
    /// </summary>
    public partial class WebPreviewer : Window
    {
        public Action? Reset { get; internal set; }

        public WebPreviewer(WallpaperType type, string filePath, string wpCustomizePathUsing)
        {
            InitializeComponent();

            _type = type;
            _filePath = filePath;
            _wpCustomizePathUsing = wpCustomizePathUsing;

            InitWebview2();
        }

        private async void InitWebview2()
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Constants.CommonPaths.TempWebView2Dir, _environmentOptions);
            await Webview2.EnsureCoreWebView2Async(env);

            Webview2.NavigationCompleted += Webview2_NavigationCompleted;

            Webview2.CoreWebView2.Navigate(Path.Combine(_workingDir, "Plugins\\UI\\Web\\viewer.html"));
        }

        private void Webview2_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key >= Key.F1 && e.Key <= Key.F12)
            {
                e.Handled = true;
            }
        }

        private async void Webview2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            await Webview2.CoreWebView2.ExecuteScriptAsync(
                @"
                    document.addEventListener('contextmenu', function(event) {
                        event.preventDefault();
                    }, false);
                "
            );
            await Task.Delay(600);

            await LoadSourceAsync(_type, _filePath);
            await LoadWpCustomizeAsync(_wpCustomizePathUsing);
        }

        private async Task LoadSourceAsync(WallpaperType type, string filePath)
        {
            try
            {
                if (filePath == null)
                {
                    Webview2.Visibility = Visibility.Collapsed;
                    return;
                }

                await ExecuteScriptFunctionAsync("virtualPaperSourceReload", type.ToString(), filePath);
                Webview2.Visibility = Visibility.Visible;
                skPanel.Visibility = Visibility.Collapsed;
            }
            catch { Webview2.Visibility = Visibility.Collapsed; }
        }

        private async Task LoadWpCustomizeAsync(string wpCustomizeFilePath)
        {
            try
            {
                if (wpCustomizeFilePath == null) return;

                foreach (var item in JsonUtil.ReadJObject(wpCustomizeFilePath))
                {
                    string uiElementType = item.Value["Type"].ToString();
                    if (!uiElementType.Equals("Button", StringComparison.OrdinalIgnoreCase) && !uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase))
                    {
                        if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase) ||
                            uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase))
                        {
                            await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", item.Key, (double)item.Value["Value"]);
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

        public async void ModifySource(string uiElementType, string propertyName, string value)
        {
            await _semaphoreSlimWallpaperModifyLock.WaitAsync();

            try
            {
                if (!uiElementType.Equals("Button", StringComparison.OrdinalIgnoreCase) && !uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase))
                {
                    if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase) ||
                        uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase))
                    {
                        await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", propertyName, double.Parse(value));
                    }
                    else if (uiElementType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase))
                    {
                        await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", propertyName, bool.Parse(value));
                    }
                    else if (uiElementType.Equals("Color", StringComparison.OrdinalIgnoreCase) || uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase))
                    {
                        await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", propertyName, (string)value);
                    }
                }

                await ExecuteScriptFunctionAsync("applyFilter");
                await ExecuteScriptFunctionAsync("play");
            }
            catch { }
            finally
            {
                _semaphoreSlimWallpaperModifyLock.Release();
            }
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

            string res = await Webview2.ExecuteScriptAsync(script.ToString());

            return res;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Reset?.Invoke();
            Webview2?.Dispose();
            Webview2 = null;
        }

        private readonly CoreWebView2EnvironmentOptions _environmentOptions = new()
        {
            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1"
        }; // workaround: avoid cache
        private string _workingDir = AppDomain.CurrentDomain.BaseDirectory;
        private WallpaperType _type;
        private string _filePath = string.Empty;
        private string _wpCustomizePathUsing = string.Empty;
        private readonly SemaphoreSlim _semaphoreSlimWallpaperModifyLock = new(1, 1);
    }
}
