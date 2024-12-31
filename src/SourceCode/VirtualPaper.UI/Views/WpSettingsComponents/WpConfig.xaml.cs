//using System;
//using System.IO;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.UI.Input;
//using Microsoft.UI.Xaml;
//using Microsoft.UI.Xaml.Controls;
//using Microsoft.Web.WebView2.Core;
//using VirtualPaper.Common;
//using VirtualPaper.Common.Models.EffectValue;
//using VirtualPaper.Common.Models.EffectValue.Base;
//using VirtualPaper.Common.Utils.Files;
//using VirtualPaper.Common.Utils.ObserverMode;
//using VirtualPaper.Common.Utils.Storage;
//using VirtualPaper.UI.Utils;
//using VirtualPaper.UI.ViewModels;
//using VirtualPaper.UI.ViewModels.WpSettingsComponents;
//using Windows.ApplicationModel.DataTransfer;
//using Windows.Storage;
//using Windows.Storage.Pickers;
//using Windows.UI.Core;
//using WinRT.Interop;

//// To learn more about WinUI, the WinUI project structure,
//// and more about our project templates, see: http://aka.ms/winui-project-info.

//namespace VirtualPaper.UI.Views.WpSettingsComponents {
//    /// <summary>
//    /// An empty page that can be used on its own or navigated to within a Frame.
//    /// </summary>
//    public sealed partial class WpConfig : Page, ICustomizeValueChangedObserver, IDisposable {
//        public WpConfig() {
//            this.InitializeComponent();

//            _wpSettingsVm = App.Services.GetRequiredService<WpSettingsViewModel>();
//            _wpSettingsVm.WpChanged += WpSettingsVm_WpChanged;
//            _wpSettingsVm.WpClosed += WpSettingsVm_WpClosed;
//            //_wpSettingsVm.WpApplied += WpSettingsVm_WpApplied;
//            _wpSettingsVm.WpPreviewed += WpSettingsVm_WpPreviewed;
//            //_wpSettingsVm.WpRestored += WpSettingsVm_WpRestored;

//            ucWpEffect.DoubleValueChanged += OnEffectValueChanged;
//            ucWpEffect.IntValueChanged += OnEffectValueChanged;
//            ucWpEffect.BoolValueChanged += OnEffectValueChanged;
//            ucWpEffect.StringValueChanged += OnEffectValueChanged;

//            InitFilePicker();
//            InitWebview2();

//            _viewModel = App.Services.GetRequiredService<WpConfigViewModel>();
//            _viewModel.UpdateWebviewContent += OnUpdateWebviewContent;
//            InitWallpaperData();

//            this.DataContext = _viewModel;
//        }

//        private async void InitWallpaperData() {
//            await _viewModel.RestoreWallpaperAsync();
//        }

//        private async void OnUpdateWebviewContent(object sender, EventArgs e) {
//            //if (_viewModel.Wallpaper == null) {
//            //    await UpdateWebviewContentAsync();
//            //}
//            //else {
//            //    await UpdateWebviewContentAsync(
//            //        _viewModel.Wallpaper.RuntimeData.RType,
//            //        _viewModel.Wallpaper.BasicData.FilePath,
//            //        _viewModel.Wallpaper.RuntimeData.WpEffectFilePathTemporary);
//            //}
//        }

//        private async void WpSettingsVm_WpRestored(object sender, EventArgs e) {
//            await _viewModel.RestoreWallpaperAsync();
//        }

//        private async void WpSettingsVm_WpPreviewed(object sender, EventArgs e) {
//            await _viewModel.PreviewAsync();
//        }

//        private void WpSettingsVm_WpApplied(object sender, EventArgs e) {
//            _viewModel.Apply();
//        }

//        private void WpSettingsVm_WpClosed(object sender, EventArgs e) {
//            _viewModel.Close();
//        }

//        private async void WpSettingsVm_WpChanged(object sender, EventArgs e) {
//            await _viewModel.RestoreWallpaperAsync();
//        }

//        private void InitFilePicker() {
//            _picker = new FileOpenPicker {
//                ViewMode = PickerViewMode.Thumbnail,
//                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
//            };

//            //_picker.FileTypeFilter.Add(".exe");
//            //_picker.FileTypeFilter.Add(".html");
//            //_picker.FileTypeFilter.Add(".htm");

//            _picker.FileTypeFilter.Add(".apng");
//            _picker.FileTypeFilter.Add(".gif");

//            _picker.FileTypeFilter.Add(".jpg");
//            _picker.FileTypeFilter.Add(".jpeg");
//            _picker.FileTypeFilter.Add(".png");
//            _picker.FileTypeFilter.Add(".bmp");
//            _picker.FileTypeFilter.Add(".svg");
//            _picker.FileTypeFilter.Add(".webp");
//            _picker.FileTypeFilter.Add(".wmf");

//            _picker.FileTypeFilter.Add(".mp4");
//            _picker.FileTypeFilter.Add(".webm");
//        }

//        private async void InitWebview2() {
//            var env = await CoreWebView2Environment.CreateWithOptionsAsync(null, Constants.CommonPaths.TempWebView2Dir, _environmentOptions);
//            await Webview2.EnsureCoreWebView2Async(env);

//            //Webview2.CoreWebView2.OpenDevToolsWindow();

//            Webview2.NavigationCompleted += Webview2_NavigationCompleted;

//            Webview2.CoreWebView2.Navigate(
//                Path.Combine(
//                    AppDomain.CurrentDomain.BaseDirectory,
//                    "Players",
//                    Constants.PlayingFile.PlayerWeb));
//        }

//        //private async Task UpdateWebviewContentAsync(WallpaperType type = WallpaperType.Unknown, string filePath = null, string wpEffectFilePathTemporary = null) {
//        //    try {
//        //        await _semaphoreSlimWebview2Loaded.WaitAsync();

//        //        await LoadSourceAsync(type, filePath);
//        //        await LoadWpEffectAsync(wpEffectFilePathTemporary);
//        //    }
//        //    catch { }
//        //    finally {
//        //        _semaphoreSlimWebview2Loaded.Release();
//        //    }
//        //}

//        private void Webview2_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args) {
//            _semaphoreSlimWebview2Loaded.Release();
//        }

//        private async void ImportButton_Click(object sender, RoutedEventArgs e) {
//            Window mainWindow = App.Services.GetRequiredService<MainWindow>();
//            var hWnd = WindowNative.GetWindowHandle(mainWindow);
//            InitializeWithWindow.Initialize(_picker, hWnd);
//            StorageFile file = await _picker.PickSingleFileAsync();
//            if (file != null) {
//                ImportValue importValue = new(
//                    file.Path,
//                    FileFilter.GetFileType(file.FileType));
//                await _viewModel.ImportFromLocalAsync(importValue);
//            }
//        }

//        private async void DetailedInfoButton_Click(object sender, RoutedEventArgs e) {
//            await _viewModel.ShowDetailedInfoAsync();
//        }

//        private async void Button_Drop(object sender, DragEventArgs e) {
//            if (e.DataView.Contains(StandardDataFormats.StorageItems)) {
//                var items = await e.DataView.GetStorageItemsAsync();
//                await _viewModel.DropFileAsync(items);
//            }
//            e.Handled = true;
//        }

//        private void Button_DragOver(object sender, DragEventArgs e) {
//            e.AcceptedOperation = DataPackageOperation.Copy;
//        }

//        //private async Task LoadSourceAsync(WallpaperType type, string filePath) {
//        //    await _semaphoreSlimWallpaperLoadingLock.WaitAsync();

//        //    try {
//        //        if (filePath == null) {
//        //            Webview2.Visibility = Visibility.Collapsed;
//        //            return;
//        //        }

//        //        await ExecuteScriptFunctionAsync("virtualPaperSourceReload", type.ToString(), filePath);
//        //        Webview2.Visibility = Visibility.Visible;
//        //    }
//        //    catch { Webview2.Visibility = Visibility.Collapsed; }
//        //    finally {
//        //        _semaphoreSlimWallpaperLoadingLock.Release();
//        //    }
//        //}

//        private async Task LoadWpEffectAsync(string wpEffectFilePath) {
//            try {
//                if (wpEffectFilePath == null) return;

//                foreach (var item in JsonUtil.GetReadonlyJson(wpEffectFilePath).EnumerateObject()) {
//                    string uiElementType = item.Value.GetProperty("Type").ToString();
//                    if (!uiElementType.Equals("Button", StringComparison.OrdinalIgnoreCase) && !uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase)) {
//                        if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase) ||
//                            uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase)) {
//                            await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", item.Name, item.Value.GetProperty("Value").ToString());
//                        }
//                        else if (uiElementType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase)) {
//                            await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", item.Name, item.Value.GetProperty("Value").ToString());
//                        }
//                        else if (uiElementType.Equals("Color", StringComparison.OrdinalIgnoreCase) || uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase)) {
//                            await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", item.Name, item.Value.GetProperty("Value").ToString());
//                        }
//                    }
//                }

//                await ExecuteScriptFunctionAsync("applyFilter");
//                await ExecuteScriptFunctionAsync("play");
//            }
//            catch { }
//        }

//        /// <summary>
//        /// 修改“当前壁纸”界面展示的壁纸
//        /// </summary>
//        /// <param name="uiElementType"></param>
//        /// <param name="propertyName"></param>
//        /// <param name="value"></param>
//        public async void ModifySource(string uiElementType, string propertyName, string value) {
//            await _semaphoreSlimWallpaperModifyLock.WaitAsync();

//            try {
//                if (!uiElementType.Equals("Button", StringComparison.OrdinalIgnoreCase) && !uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase)) {
//                    if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase) ||
//                        uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase)) {
//                        await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", propertyName, double.Parse(value));
//                    }
//                    else if (uiElementType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase)) {
//                        await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", propertyName, bool.Parse(value));
//                    }
//                    else if (uiElementType.Equals("Color", StringComparison.OrdinalIgnoreCase) || uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase)) {
//                        await ExecuteScriptFunctionAsync("virtualPaperPropertyListener", propertyName, value);
//                    }
//                }

//                await ExecuteScriptFunctionAsync("applyFilter");
//                await ExecuteScriptFunctionAsync("play");
//            }
//            catch { }
//            finally {
//                _semaphoreSlimWallpaperModifyLock.Release();
//            }
//        }

//        //credit: https://stackoverflow.com/questions/62835549/equivalent-of-webbrowser-invokescriptstring-object-in-webview2
//        private async Task<string> ExecuteScriptFunctionAsync(string functionName, params object[] parameters) {
//            var script = new StringBuilder();
//            script.Append(functionName);
//            script.Append('(');
//            for (int i = 0; i < parameters.Length; i++) {
//                script.Append(JsonSerializer.Serialize(parameters[i]));
//                if (i < parameters.Length - 1) {
//                    script.Append(", ");
//                }
//            }
//            script.Append(");");

//#if DEBUG
//            string cmd = script.ToString();
//#endif

//            string res = await Webview2.ExecuteScriptAsync(script.ToString());

//            return res;
//        }

//        public async void OnEffectValueChanged(object sender, DoubleValueChangedEventArgs args) {
//            await OnEffectValueChanged(args);
//        }

//        public async void OnEffectValueChanged(object sender, IntValueChangedEventArgs args) {
//            await OnEffectValueChanged(args);
//        }

//        public async void OnEffectValueChanged(object sender, BoolValueChangedEventArgs args) {
//            await OnEffectValueChanged(args);
//        }

//        public async void OnEffectValueChanged(object sender, StringValueChangedEventArgs args) {
//            await OnEffectValueChanged(args);
//        }

//        private async Task OnEffectValueChanged<T>(EffectValueChanged<T> args) {
//            ModifySource(args.ControlName, args.PropertyName, args.Value.ToString());
//            await _viewModel.ModifyPreviewAsync(args.ControlName, args.PropertyName, args.Value.ToString());
//        }

//        //ref: https://learn.microsoft.com/en-us/uwp/api/windows.ui.core.corecursortype?view=winrt-26100
//        private void BtnContent_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) {
//            this.ProtectedCursor = InputCursor.CreateFromCoreCursor(new CoreCursor(CoreCursorType.Hand, 0));
//        }

//        private void BtnContent_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) {
//            this.ProtectedCursor = null;
//        }

//        #region Dispose
//        private bool _isDisposed = false;
//        private void Dispose(bool disposing) {
//            if (!_isDisposed) {
//                if (disposing) {
//                    Webview2?.Close();

//                    _wpSettingsVm.WpChanged -= WpSettingsVm_WpChanged;
//                    _wpSettingsVm.WpClosed -= WpSettingsVm_WpClosed;
//                    //_wpSettingsVm.WpApplied -= WpSettingsVm_WpApplied;
//                    _wpSettingsVm.WpPreviewed -= WpSettingsVm_WpPreviewed;
//                    //_wpSettingsVm.WpRestored -= WpSettingsVm_WpRestored;

//                    _viewModel.UpdateWebviewContent -= OnUpdateWebviewContent;
//                }
//                _isDisposed = true;
//            }
//        }

//        public void Dispose() {
//            Dispose(disposing: true);
//            GC.SuppressFinalize(this);
//        }
//        #endregion

//        private WpConfigViewModel _viewModel;
//        private WpSettingsViewModel _wpSettingsVm;
//        private readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
//            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1"
//        }; // workaround: avoid cache
//        //private readonly string _workingDir = AppDomain.CurrentDomain.BaseDirectory;
//        private readonly SemaphoreSlim _semaphoreSlimWallpaperLoadingLock = new(1, 1);
//        private readonly SemaphoreSlim _semaphoreSlimWallpaperModifyLock = new(1, 1);
//        private readonly SemaphoreSlim _semaphoreSlimWebview2Loaded = new(0, 1);
//        private FileOpenPicker _picker;
//    }
//}
