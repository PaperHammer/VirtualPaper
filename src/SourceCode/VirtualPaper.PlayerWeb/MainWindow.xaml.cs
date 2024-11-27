using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.PlayerWeb.Utils;
using VirtualPaper.PlayerWeb.ViewModel;
using WinRT.Interop;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx {
        private bool _isFocusOn;
        public bool IsFocusOn {
            get { return _isFocusOn; }
            set { _isFocusOn = value; ParallaxSet(); }
        }

        public MainWindow(StartArgs startArgs) {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? DispatcherQueueController.CreateOnCurrentThread().DispatcherQueue;
            _startArgs = startArgs;
            SetPositionAndSize();

            this.InitializeComponent();

            _viewModel = new MainWindowViewModel();
            this.ContentGrid.DataContext = _viewModel;

            if (_startArgs.IsPreview) {
                SetWindowStyle();
                SetWindowTitleBar();
            }
            else {
                AppTitleBar.Visibility = Visibility.Collapsed;
            }

            _filePath = _startArgs.FilePath;
        }

        private void WindowEx_SizeChanged(object sender, WindowSizeChangedEventArgs args) {
            SetPositionAndSize();
        }

        private async void WindowEx_Activated(object sender, WindowActivatedEventArgs args) {
            if (args.WindowActivationState == WindowActivationState.Deactivated) {
                TitleTextBlock.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];

                IsFocusOn = false;
            }
            else {
                TitleTextBlock.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];

                IsFocusOn = true;
            }

            if (_isFirstRun) {
                _isFirstRun = false;
                _viewModel.Loading(false, false, []);

                await InitializeWebViewAsync();

                if (_startArgs.IsPreview) {
                    WindowUtil.OpenEffectConfigWindow(_startArgs);
                }
                else {
                    WindowUtil.SetWindowAsBackground();
                }

                App.WriteToParent(new VirtualPaperMessageHwnd() {
                    Hwnd = WindowUtil.GetWindowHwnd(this),
                });
                _ = StdInListener();
            }
        }

        private void WindowEx_Closed(object sender, WindowEventArgs args) {
            WindowUtil.CloseToolWindow();

            App.WriteToParent(new VirtualPaperPreviewOffCmd());
        }

        private async Task StdInListener() {
            try {
                await Task.Run(async () => {
                    while (true) {
                        var msg = await Console.In.ReadLineAsync();
                        if (string.IsNullOrEmpty(msg)) {
                            //When the redirected stream is closed, a null line is sent to the event handler. 
#if !DEBUG
                            _ctsParallax?.Cancel();
                            break;
#endif
                        }
                        else {
                            try {
                                var close = false;
                                var obj = JsonSerializer.Deserialize<IpcMessage>(msg);

                                _dispatcherQueue.TryEnqueue(async () => {
                                    switch (obj.Type) {
                                        //case MessageType.msg_rect:
                                        //    var rc = (VirtualPaperMessageRECT)obj;
                                        //    _windowRc.X = rc.X;
                                        //    _windowRc.Y = rc.Y;
                                        //    _windowRc.Width = rc.Width;
                                        //    _windowRc.Height = rc.Height;
                                        //    break;
                                        case MessageType.cmd_close:
                                            close = true;
                                            break;
                                        case MessageType.cmd_apply:
                                            await ExecuteScriptFunctionAsync(Fileds.ApplyFilter);
                                            await ExecuteScriptFunctionAsync(Fileds.Play);
                                            break;
                                        case MessageType.cmd_preview_on:
                                            this.BringToFront();
                                            //WindowUtil.OpenEffectConfigWindow(_startArgs);
                                            break;
                                        case MessageType.cmd_reload:
                                            Webview2?.Reload();
                                            break;
                                        case MessageType.cmd_suspend:
                                            if (!_isPaused) {
                                                await ExecuteScriptFunctionAsync(Fileds.PlaybackChanged, true);
                                            }
                                            _isPaused = true;
                                            break;
                                        case MessageType.cmd_resume:
                                            if (_isPaused) {
                                                await ExecuteScriptFunctionAsync(Fileds.PlaybackChanged, false);
                                            }
                                            _isPaused = false;
                                            break;
                                        case MessageType.cmd_muted:
                                            var muted = (VirtualPaperMutedCmd)obj;
                                            await ExecuteScriptFunctionAsync(Fileds.AudioMuteChanged, muted.IsMuted);
                                            break;
                                        case MessageType.cmd_update:
                                            var update = (VirtualPaperUpdateCmd)obj;
                                            await LoadSourceAsync(update.FilePath, update.WpType);
                                            LoadWpEffect(update.WpEffectFilePathUsing);
                                            break;
                                        case MessageType.cmd_suspend_parallax:
                                            _isFocusOnDesk = false;
                                            ParallaxSet();
                                            break;
                                        case MessageType.cmd_resume_parallax:
                                            _isFocusOnDesk = true;
                                            ParallaxSet();
                                            break;

                                        case MessageType.vp_slider:
                                            var sl = (VirtualPaperSlider)obj;
                                            await ExecuteScriptFunctionAsync(Fileds.PropertyListener, sl.Name, sl.Value);
                                            break;
                                        case MessageType.vp_textbox:
                                            var tb = (VirtualPaperTextBox)obj;
                                            await ExecuteScriptFunctionAsync(Fileds.PropertyListener, tb.Name, tb.Value);
                                            break;
                                        case MessageType.vp_dropdown:
                                            var dd = (VirtualPaperDropdown)obj;
                                            await ExecuteScriptFunctionAsync(Fileds.PropertyListener, dd.Name, dd.Value);
                                            break;
                                        case MessageType.vp_cpicker:
                                            var cp = (VirtualPaperColorPicker)obj;
                                            await ExecuteScriptFunctionAsync(Fileds.PropertyListener, cp.Name, cp.Value);
                                            break;
                                        case MessageType.vp_chekbox:
                                            var cb = (VirtualPaperCheckbox)obj;
                                            ExecuteCheckBoxSet(cb.Name, cb.Value);
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
                _dispatcherQueue.TryEnqueue(() => {
                    Webview2?.Close();
                    App.AppInstance.Exit();
                });
            }
        }

        private async void ParallaxSet() {
            try {
                if (_isParallaxOn &&
                    (_isFocusOnDesk || _startArgs.IsPreview && IsFocusOn)) {
                    if (Interlocked.CompareExchange(ref _isParallaxRunning, 1, -1) == 1) return;

                    App.WriteToParent(new VirtualPaperMessageConsole() {
                        MsgType = ConsoleMessageType.Log,
                        Message = $"Parallax is On.",
                    });

                    _ctsParallax = null;
                    _ctsParallax = new CancellationTokenSource();
                    await Task.Run(async () => {
                        while (!_ctsParallax.IsCancellationRequested) {
                            var pos = RawInput.GetMousePos();
                            int mouseX = pos.X, mouseY = pos.Y;

                            if (_windowRc.Left <= mouseX && mouseX <= _windowRc.Right &&
                                _windowRc.Top <= mouseY && mouseY <= _windowRc.Bottom) {
                                await ExecuteScriptFunctionAsync(
                                    Fileds.MouseMove, mouseX, mouseY);
                            }
                            else {
                                await ExecuteScriptFunctionAsync(Fileds.MouseOut);
                            }

                            await Task.Delay(100, _ctsParallax.Token);
                        }
                    }, _ctsParallax.Token);
                }
                else {
                    if (Interlocked.CompareExchange(ref _isParallaxRunning, -1, 1) == -1) return;

                    if (_ctsParallax != null && !_ctsParallax.IsCancellationRequested) {
                        _ctsParallax?.Cancel();
                    }
                    await ExecuteScriptFunctionAsync(Fileds.MouseOut);

                    App.WriteToParent(new VirtualPaperMessageConsole() {
                        MsgType = ConsoleMessageType.Log,
                        Message = $"Parallax is Off.",
                    });
                }
            }
            catch (Exception e) {
                App.WriteToParent(new VirtualPaperMessageConsole() {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Function ['ParallaxSet'] an error occured: {e.Message}",
                });
            }
        }

        private async Task InitializeWebViewAsync() {
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(null, Constants.CommonPaths.TempWebView2Dir, _environmentOptions);
            await Webview2.EnsureCoreWebView2Async(env);

            Webview2.CoreWebView2.OpenDevToolsWindow();

            Webview2.CoreWebView2.ProcessFailed += (s, e) => {
                App.WriteToParent(new VirtualPaperMessageConsole() {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Process fail: {e.Reason}",
                });
                _dispatcherQueue.TryEnqueue(App.AppInstance.Exit);
            };

            Webview2.NavigationCompleted += Webview2_NavigationCompleted;

            string playingFile = GetPlayingFile();
            Webview2.CoreWebView2.Navigate(
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    playingFile));
        }

        private string GetPlayingFile() {
            return _startArgs.RuntimeType switch {
                "RImage" => Constants.PlayingFile.PlayerWeb,
                "RImage3D" => Constants.PlayingFile.PlayerWeb3D,
                "RVideo" => Constants.PlayingFile.PlayerWeb,
                _ => throw new ArgumentException(nameof(_startArgs.RuntimeType)),
            };
        }

        private async void Webview2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e) {
            switch (_startArgs.RuntimeType) {
                case "RImage":
                case "RVideo":
                    await ExecuteScriptFunctionAsync(Fileds.Init, _windowRc.Right - _windowRc.Left, _windowRc.Bottom - _windowRc.Top);
                    await LoadSourceAsync(_startArgs.FilePath, _startArgs.RuntimeType);
                    break;
                case "RImage3D":
                    await ExecuteScriptFunctionAsync(Fileds.Init, _startArgs.FilePath, _startArgs.DepthFilePath);
                    break;
                default:
                    break;
            }
            LoadWpEffect(_startArgs.WpEffectFilePathUsing);
            await ExecuteScriptFunctionAsync(Fileds.Play);

            App.WriteToParent(new VirtualPaperMessageWallpaperLoaded() {
                Success = true
            });

            _viewModel.Loaded([]);
        }

        private async Task LoadSourceAsync(string filePath, string wpType) {
            try {
                if (filePath == null) return;

                await ExecuteScriptFunctionAsync(Fileds.ResourceLoad, wpType, filePath);
            }
            catch (Exception ex) {
                App.WriteToParent(new VirtualPaperMessageConsole() {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Process fail: {ex}",
                });
            }
        }

        private async void LoadWpEffect(string wpEffectFilePath) {
            try {
                if (wpEffectFilePath == null) return;

                foreach (var item in JsonUtil.GetReadonlyJson(wpEffectFilePath).EnumerateObject()) {
                    string uiElementType = item.Value.GetProperty("Type").ToString();
                    if (!uiElementType.Equals("Button", StringComparison.OrdinalIgnoreCase) && !uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase)) {
                        if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase) ||
                            uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase)) {
                            await ExecuteScriptFunctionAsync(Fileds.PropertyListener, item.Name, item.Value.GetProperty("Value").ToString());
                        }
                        else if (uiElementType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase)) {
                            ExecuteCheckBoxSet(item.Name, bool.Parse(item.Value.GetProperty("Value").ToString()));
                        }
                        else if (uiElementType.Equals("Color", StringComparison.OrdinalIgnoreCase) || uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase)) {
                            await ExecuteScriptFunctionAsync(Fileds.PropertyListener, item.Name, item.Value.GetProperty("Value").ToString());
                        }
                    }
                }
            }
            catch (Exception ex) {
                App.WriteToParent(new VirtualPaperMessageConsole() {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Process fail: {ex}",
                });
            }
        }

        internal void ExecuteCheckBoxSet(string propertyName, bool val) {
            switch (propertyName) {
                case "Parallax":
                    _isParallaxOn = val;
                    ParallaxSet();
                    break;
                default:
                    break;
            }
        }

        //credit: https://stackoverflow.com/questions/62835549/equivalent-of-webbrowser-invokescriptstring-object-in-webview2
        internal async Task<string> ExecuteScriptFunctionAsync(string functionName, params object[] parameters) {
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

#if DEBUG
            string cmd = sb_script.ToString();
#endif

            string script = string.Empty;

            script = await Webview2.ExecuteScriptAsync(sb_script.ToString());

            return script;
        }

        // ×èÖ¹£¨Êó±êµÈ£©Ö¸Õë²Ù×÷
        private void Webview2_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) {
            e.Handled = true;
        }

        // ×èÖ¹¼üÅÌ²Ù×÷
        private void Webview2_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) {
            e.Handled = true;
        }

        private void SetPositionAndSize() {
            _windowRc = RawInput.GetWindowRECT(this);
        }

        #region window title bar
        private void SetWindowStyle() {
            string type = _startArgs.WindowStyleType;
            this.SystemBackdrop = type switch {
                "Mica" => new MicaBackdrop(),
                "Acrylic" => new DesktopAcrylicBackdrop(),
                _ => default,
            };
        }

        private void SetWindowTitleBar() {
            //ref: https://learn.microsoft.com/en-us/windows/apps/develop/title-bar?tabs=wasdk
            if (AppWindowTitleBar.IsCustomizationSupported()) {
                var titleBar = this.AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonForegroundColor = ((SolidColorBrush)App.Current.Resources["WindowCaptionForeground"]).Color;

                AppTitleBar.Loaded += AppTitleBar_Loaded;
                AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
            }
            else {
                AppTitleBar.Visibility = Visibility.Collapsed;
                this.UseImmersiveDarkModeEx(_startArgs.ApplicationTheme == AppTheme.Dark);
            }
        }

        private void AppTitleBar_Loaded(object sender, RoutedEventArgs e) {
            if (AppWindowTitleBar.IsCustomizationSupported()) {
                SetDragRegionForCustomTitleBar(this.AppWindow);
            }
        }

        private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && this.AppWindow.TitleBar.ExtendsContentIntoTitleBar) {
                // Update drag region if the size of the title bar changes.
                SetDragRegionForCustomTitleBar(this.AppWindow);
            }
        }

        private void SetDragRegionForCustomTitleBar(AppWindow appWindow) {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && appWindow.TitleBar.ExtendsContentIntoTitleBar) {
                double scaleAdjustment = GetScaleAdjustment();

                RightPaddingColumn.Width = new GridLength(appWindow.TitleBar.RightInset / scaleAdjustment);
                LeftPaddingColumn.Width = new GridLength(appWindow.TitleBar.LeftInset / scaleAdjustment);

                List<Windows.Graphics.RectInt32> dragRectsList = [];

                Windows.Graphics.RectInt32 dragRectL;
                dragRectL.X = (int)((LeftPaddingColumn.ActualWidth) * scaleAdjustment);
                dragRectL.Y = 0;
                dragRectL.Height = (int)(AppTitleBar.ActualHeight * scaleAdjustment);
                dragRectL.Width = (int)((IconColumn.ActualWidth
                                        + TitleColumn.ActualWidth
                                        + LeftDragColumn.ActualWidth) * scaleAdjustment);
                dragRectsList.Add(dragRectL);

                Windows.Graphics.RectInt32 dragRectR;
                dragRectR.X = (int)((LeftPaddingColumn.ActualWidth
                                    + IconColumn.ActualWidth
                                    + TitleTextBlock.ActualWidth
                                    + LeftDragColumn.ActualWidth) * scaleAdjustment);
                dragRectR.Y = 0;
                dragRectR.Height = (int)(AppTitleBar.ActualHeight * scaleAdjustment);
                dragRectR.Width = (int)(RightDragColumn.ActualWidth * scaleAdjustment);
                dragRectsList.Add(dragRectR);

                Windows.Graphics.RectInt32[] dragRects = dragRectsList.ToArray();

                appWindow.TitleBar.SetDragRectangles(dragRects);
            }
        }

        private double GetScaleAdjustment() {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            DisplayArea displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

            // Get DPI.
            int result = Native.GetDpiForMonitor(hMonitor, Native.Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _);
            if (result != 0) {
                throw new Exception("Could not get DPI for monitor.");
            }

            uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
            return scaleFactorPercent / 100.0;
        }
        #endregion

        private readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1"
        }; // workaround: avoid cache
        private readonly StartArgs _startArgs;
        private bool _isPaused = false;
        private CancellationTokenSource _ctsParallax;
        private bool _isParallaxOn = false;
        private bool _isFocusOnDesk = false;
        private int _isParallaxRunning = -1;
        private Native.RECT _windowRc;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly string _filePath = string.Empty;
        private bool _isFirstRun = true;
        private readonly MainWindowViewModel _viewModel;
    }
}
