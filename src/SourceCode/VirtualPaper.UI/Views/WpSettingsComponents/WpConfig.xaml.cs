using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Models;
using VirtualPaper.Common.Utils.ObserverMode;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.UI.ViewModels;
using VirtualPaper.UI.ViewModels.WpSettingsComponents;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views.WpSettingsComponents
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WpConfig : Page, ICustomizeValueChangedObserver, IDisposable
    {
        public WpConfig()
        {
            this.InitializeComponent();

            var wpSettingsVm = App.Services.GetRequiredService<WpSettingsViewModel>();
            _content = wpSettingsVm.Monitors[wpSettingsVm.MonitorSelectedIdx].Content;
                        
            InitCustomizeData();
            InitWebview2();
            
            _viewModel = new(InitWebviewContent);
            _viewModel.DoubleValueChanged += OnCustomizeValueChanged;
            _viewModel.BoolValueChanged += OnCustomizeValueChanged;
            _viewModel.StringValueChanged += OnCustomizeValueChanged;
            this.DataContext = _viewModel;
        }

        public async void InitContent()
        {
            var wpSettingsVm = App.Services.GetRequiredService<WpSettingsViewModel>();
            _content = wpSettingsVm.Monitors[wpSettingsVm.MonitorSelectedIdx].Content;
            await _viewModel.InitWp(_content);
        }

        private void InitCustomizeData()
        {
            _pictureAndGifCostumise = new();
            _videoCostumize = new();
        }

        private async void InitWebview2()
        {
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(null, Constants.CommonPaths.TempWebView2Dir, _environmentOptions);
            await Webview2.EnsureCoreWebView2Async(env);

            //Webview2.CoreWebView2.OpenDevToolsWindow();

            Webview2.NavigationCompleted += Webview2_NavigationCompleted;

            Webview2.CoreWebView2.Navigate(Path.Combine(_workingDir, "Web\\viewer.html"));
        }

        private async void Webview2_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            await _viewModel.InitWp(_content);
        }

        private async Task InitWebviewContent(WallpaperType type = WallpaperType.unknown, string filePath = null, string wpCustomizePathUsing = null)
        {
            await LoadSourceAsync(type, filePath);
            await RestoreWpCustomizeAsync(wpCustomizePathUsing);
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            };

            Window mainWindow = App.Services.GetRequiredService<MainWindow>();
            var hWnd = WindowNative.GetWindowHandle(mainWindow);
            InitializeWithWindow.Initialize(picker, hWnd);

            //picker.FileTypeFilter.Add(".exe");
            //picker.FileTypeFilter.Add(".html");
            //picker.FileTypeFilter.Add(".htm");

            picker.FileTypeFilter.Add(".apng");
            picker.FileTypeFilter.Add(".gif");

            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".svg");
            picker.FileTypeFilter.Add(".webp");
            picker.FileTypeFilter.Add(".wmf");

            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".webm");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await _viewModel.TryImportFromLocalAsync(file.Path, this.XamlRoot);
            }
        }

        private async void DetailedInfoButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ShowDetailedInfoPop(this.XamlRoot);
        }

        private async void Button_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                await _viewModel.TryDropFileAsync(items, this.XamlRoot);
            }
            e.Handled = true;
        }

        private void Button_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async Task LoadSourceAsync(WallpaperType type, string filePath)
        {
            await _semaphoreSlimWallpaperLoadingLock.WaitAsync();

            try
            {
                if (filePath == null)
                {
                    Webview2.Visibility = Visibility.Collapsed;
                    return;
                }

                await ExecuteScriptFunctionAsync("virtualPaperSourceReload", type.ToString(), filePath);
                Webview2.Visibility = Visibility.Visible;
            }
            catch { Webview2.Visibility = Visibility.Collapsed; }
            finally
            {
                _semaphoreSlimWallpaperLoadingLock.Release();
            }
        }

        private async Task RestoreWpCustomizeAsync(string wpCustomizeFilePath)
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

        public async void OnCustomizeValueChanged(object sender, DoubleValueChangedEventArgs args)
        {
            ModifyCustomizeProperty(args.PropertyName, args.Value);
            ModifySource(args.ControlName, args.PropertyName, args.Value.ToString());
            await _viewModel.ModifyPreviewAsync(args.ControlName, args.PropertyName, args.Value.ToString());
        }

        public void OnCustomizeValueChanged(object sender, BoolValueChangedEventArgs args)
        {
            ModifyCustomizeProperty(args.PropertyName, args.Value);
            ModifySource(args.ControlName, args.PropertyName, args.Value.ToString());
        }

        public void OnCustomizeValueChanged(object sender, StringValueChangedEventArgs args)
        {
            ModifyCustomizeProperty(args.PropertyName, args.Value);
            ModifySource(args.ControlName, args.PropertyName, args.Value.ToString());
        }

        private void ModifyCustomizeProperty<T>(string propertyName, T val)
        {
            if (_viewModel.Wallpaper.Type == WallpaperType.picture || _viewModel.Wallpaper.Type == WallpaperType.gif)
                _pictureAndGifCostumise.ModifyPropertyValue(propertyName, val);
            else if (_viewModel.Wallpaper.Type == WallpaperType.video)
                _videoCostumize.ModifyPropertyValue(propertyName, val);
        }

        #region Dispose
        private bool _isDisposed = false;
        private void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Webview2?.Close();
                    _viewModel.DoubleValueChanged -= OnCustomizeValueChanged;
                    _viewModel.BoolValueChanged -= OnCustomizeValueChanged;
                    _viewModel.StringValueChanged -= OnCustomizeValueChanged;
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private string _content;
        private WpConfigViewModel _viewModel;
        private readonly CoreWebView2EnvironmentOptions _environmentOptions = new()
        {
            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1"
        }; // workaround: avoid cache
        private readonly string _workingDir = AppDomain.CurrentDomain.BaseDirectory;
        private PictureCostumise _pictureAndGifCostumise;
        private VideoAndGifCostumize _videoCostumize;
        private readonly SemaphoreSlim _semaphoreSlimWallpaperLoadingLock = new(1, 1);
        private readonly SemaphoreSlim _semaphoreSlimWallpaperModifyLock = new(1, 1);

        //[ComImport]
        //[Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
        //[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        //public interface IInitializeWithWindow
        //{
        //    void Initialize(IntPtr hwnd);
        //}
        //[ComImport]
        //[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        //[Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
        //internal interface IWindowNative
        //{
        //    IntPtr WindowHandle { get; }
        //}
    }
}
