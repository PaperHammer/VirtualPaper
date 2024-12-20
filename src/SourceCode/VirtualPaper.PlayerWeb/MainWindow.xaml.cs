using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using VirtualPaper.UIComponent.Utils.Extensions;
using WinRT.Interop;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx {
        public SolidColorBrush WindowCaptionForeground { get; private set; }
        public SolidColorBrush WindowCaptionForegroundDisabled { get; private set; }

        private bool _isFocusOnWindow;
        public bool IsFocusOnWindow {
            get { return _isFocusOnWindow; }
            set {
                _isFocusOnWindow = value;
                ParallaxControl();
            }
        }

        public MainWindow(StartArgs startArgs) {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? DispatcherQueueController.CreateOnCurrentThread().DispatcherQueue;
            _startArgs = startArgs;
            SetPositionAndSize();

            this.InitializeComponent();

            _viewModel = new MainWindowViewModel();
            this.ContentGrid.DataContext = _viewModel;

            if (_startArgs.IsPreview) {
                WindowCaptionForeground = (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
                WindowCaptionForegroundDisabled = (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];

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
                TitleTextBlock.Foreground = WindowCaptionForegroundDisabled;

                IsFocusOnWindow = false;
            }
            else {
                TitleTextBlock.Foreground = WindowCaptionForeground;

                IsFocusOnWindow = true;
            }

            if (_isFirstRun) {
                _isFirstRun = false;
                _viewModel.Loading(false, false, []);

                await InitializeWebViewAsync();

                if (_startArgs.IsPreview) {
                    WindowUtil.OpenToolWindow(_startArgs);
                    WindowUtil.AddEffectConfigPage();
                    WindowUtil.AddDetailsPage();
                }
                else {
                    this.Activated -= WindowEx_Activated;
                    WindowUtil.SetWindowAsBackground();
                }

                _ = StdInListener();
            }
        }

        private void WindowEx_Closed(object sender, WindowEventArgs args) {
            Closing();
        }

        private void Webview2_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) {
            e.Handled = true; // ×èÖ¹£¨Êó±êµÈ£©Ö¸Õë²Ù×÷
        }

        private void Webview2_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) {
            e.Handled = true;  // ×èÖ¹¼üÅÌ²Ù×÷
        }

        private void Closing() {
            this.Hide();

            WindowUtil.CloseToolWindow();
            StopParallaxLoop();

            _dispatcherQueue.TryEnqueue(() => {
                Webview2?.Close();
                App.AppInstance.Exit();
            });

            App.WriteToParent(new VirtualPaperMessageClosed());
        }

        private async Task StdInListener() {
            try {
                await Task.Run(async () => {
                    while (!_isClose) {
                        var msg = await Console.In.ReadLineAsync();
                        if (string.IsNullOrEmpty(msg)) {
                            //When the redirected stream is closed, a null line is sent to the event handler. 
#if !DEBUG
                            _ctsParallax?.Cancel();
                            break;
#endif
                        }
                        else {
                            HandleIpcMessage(msg);
                        }
                    }
                });
            }
            catch (Exception e) {
                App.WriteToParent(new VirtualPaperMessageConsole {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Ipc stdin Error: {e.Message}"
                });
            }
            finally {
                Closing();
            }
        }

        private async void HandleIpcMessage(string message) {
            try {
                var obj = JsonSerializer.Deserialize<IpcMessage>(message);
                switch (obj.Type) {
                    case MessageType.cmd_close:
                        HandleCloseCommand();
                        break;
                    case MessageType.cmd_apply:
                        _ = ExecuteScriptFunctionAsync(Fileds.ApplyFilter);
                        _ = ExecuteScriptFunctionAsync(Fileds.Play);
                        break;
                    case MessageType.cmd_active:
                        this.BringToFront();
                        break;
                    case MessageType.cmd_reload:
                        Webview2?.Reload();
                        break;
                    case MessageType.cmd_suspend:
                        await HandlePlaybackCommandAsync(true);
                        break;
                    case MessageType.cmd_resume:
                        await HandlePlaybackCommandAsync(false);
                        break;
                    case MessageType.cmd_muted:
                        await HandleMuteCommandAsync((VirtualPaperMutedCmd)obj);
                        break;
                    case MessageType.cmd_update:
                        await HandleUpdateCommandAsync((VirtualPaperUpdateCmd)obj);
                        break;
                    case MessageType.cmd_suspend_parallax:
                        _isFocusOnDesk = false;
                        ParallaxControl();
                        break;
                    case MessageType.cmd_resume_parallax:
                        _isFocusOnDesk = true;
                        ParallaxControl();
                        break;

                    case MessageType.vp_slider:
                        var sl = (VirtualPaperSlider)obj;
                        HandleVpMsg(sl.Name, sl.Value);
                        break;
                    case MessageType.vp_textbox:
                        var tb = (VirtualPaperTextBox)obj;
                        HandleVpMsg(tb.Name, tb.Value);
                        break;
                    case MessageType.vp_dropdown:
                        var dd = (VirtualPaperDropdown)obj;
                        HandleVpMsg(dd.Name, dd.Value);
                        break;
                    case MessageType.vp_cpicker:
                        var cp = (VirtualPaperColorPicker)obj;
                        HandleVpMsg(cp.Name, cp.Value);
                        break;
                    case MessageType.vp_chekbox:
                        var cb = (VirtualPaperCheckbox)obj;
                        ExecuteCheckBoxSet(cb.Name, cb.Value);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported message type: {obj.Type}");
                }
            }
            catch (Exception e) {
                App.WriteToParent(new VirtualPaperMessageConsole {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Ipc action Error: {e.Message}"
                });
            }
        }

        #region handel_ipcmessage
        private void HandleVpMsg(string propertyName, object propertyValue) {
            _ = ExecuteScriptFunctionAsync(Fileds.PropertyListener, propertyName, propertyValue);
        }

        private void HandleCloseCommand() {
            _isClose = true;
            StopParallaxLoop();
        }

        private async Task HandlePlaybackCommandAsync(bool pause) {
            if (_isPaused == pause) return;

            await ExecuteScriptFunctionAsync(Fileds.PlaybackChanged, pause);
            _isPaused = pause;
        }

        private async Task HandleMuteCommandAsync(VirtualPaperMutedCmd muted) {
            await ExecuteScriptFunctionAsync(Fileds.AudioMuteChanged, muted.IsMuted);
        }

        private async Task HandleUpdateCommandAsync(VirtualPaperUpdateCmd update) {
            await LoadSource(update.FilePath, update.WpType);
            LoadWpEffect(update.WpEffectFilePathUsing);
        }
        #endregion

        private void StopParallaxLoop() {
            _isParallaxOn = false;
        }

        private void ParallaxControl() {
            try {
                if (_isParallaxOn &&
                    (_isFocusOnDesk || _startArgs.IsPreview && IsFocusOnWindow)) {
                    if (Interlocked.CompareExchange(ref _isParallaxRunning, 1, 0) == 1) return;

                    App.WriteToParent(new VirtualPaperMessageConsole() {
                        MsgType = ConsoleMessageType.Log,
                        Message = $"Parallax is On.",
                    });

                    _ = Task.Run(async () => {
                        try {
                            while (_isParallaxRunning == 1) {
                                var pos = RawInput.GetMousePos();
                                int mouseX = pos.X, mouseY = pos.Y;

                                if (_windowRc.Left <= mouseX && mouseX <= _windowRc.Right &&
                                    _windowRc.Top <= mouseY && mouseY <= _windowRc.Bottom) {
                                    _ = ExecuteScriptFunctionAsync(
                                       Fileds.MouseMove, mouseX, mouseY);
                                }
                                else {
                                    _ = ExecuteScriptFunctionAsync(Fileds.MouseOut);
                                }

                                await Task.Delay(100);
                            }
                        }
                        catch (Exception e) {
                            App.WriteToParent(new VirtualPaperMessageConsole {
                                MsgType = ConsoleMessageType.Error,
                                Message = $"[ParallaxControl] error: {e.Message}"
                            });
                        }
                    });
                }
                else {
                    if (Interlocked.CompareExchange(ref _isParallaxRunning, 0, 1) == 0) return;

                    _ = ExecuteScriptFunctionAsync(Fileds.MouseOut);

                    App.WriteToParent(new VirtualPaperMessageConsole() {
                        MsgType = ConsoleMessageType.Log,
                        Message = $"Parallax is Off.",
                    });
                }
            }
            catch (Exception e) {
                App.WriteToParent(new VirtualPaperMessageConsole() {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Function ['ParallaxControl'] an error occured: {e.Message}",
                });
            }
        }

        private async Task InitializeWebViewAsync() {
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(null, Constants.CommonPaths.TempWebView2Dir, _environmentOptions);
            await Webview2.EnsureCoreWebView2Async(env);

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

        private async void Webview2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e) {
            switch (_startArgs.RuntimeType) {
                case "RImage":
                case "RVideo":
                    _ = ExecuteScriptFunctionAsync(Fileds.Init, _windowRc.Right - _windowRc.Left, _windowRc.Bottom - _windowRc.Top);
                    await LoadSource(_startArgs.FilePath, _startArgs.RuntimeType);
                    break;
                case "RImage3D":
                    await ExecuteScriptFunctionAsync(Fileds.Init, _startArgs.FilePath, _startArgs.DepthFilePath);
                    break;
                default:
                    break;
            }
            LoadWpEffect(_startArgs.WpEffectFilePathUsing);
            _ = ExecuteScriptFunctionAsync(Fileds.Play);

            App.WriteToParent(new VirtualPaperMessageProcId() {
                ProcId = Webview2.CoreWebView2.BrowserProcessId,
            });
            //Webview2.CoreWebView2.OpenDevToolsWindow();

            _viewModel.Loaded([]);
        }

        private async Task LoadSource(string filePath, string wpType) {
            try {
                if (string.IsNullOrEmpty(filePath)) return;

                await ExecuteScriptFunctionAsync(Fileds.ResourceLoad, wpType, filePath);
            }
            catch (Exception ex) {
                App.WriteToParent(new VirtualPaperMessageConsole() {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Process fail: {ex.Message}",
                });
            }
        }

        private void LoadWpEffect(string wpEffectFilePath) {
            try {
                if (wpEffectFilePath == null) return;

                foreach (var item in JsonUtil.GetReadonlyJson(wpEffectFilePath).EnumerateObject()) {
                    string uiElementType = item.Value.GetProperty("Type").ToString();
                    if (!uiElementType.Equals("Button", StringComparison.OrdinalIgnoreCase) && !uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase)) {
                        if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase) ||
                            uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase)) {
                            _ = ExecuteScriptFunctionAsync(Fileds.PropertyListener, item.Name, item.Value.GetProperty("Value").ToString());
                        }
                        else if (uiElementType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase)) {
                            ExecuteCheckBoxSet(item.Name, bool.Parse(item.Value.GetProperty("Value").ToString()));
                        }
                        else if (uiElementType.Equals("Color", StringComparison.OrdinalIgnoreCase) || uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase)) {
                            _ = ExecuteScriptFunctionAsync(Fileds.PropertyListener, item.Name, item.Value.GetProperty("Value").ToString());
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

            string script = string.Empty;
            await _dispatcherQueue.EnqueueOrInvoke(async () => {
                if (Webview2.CoreWebView2 == null) { // ???
                    await Webview2.EnsureCoreWebView2Async();
                }
                await Webview2.ExecuteScriptAsync(sb_script.ToString());
            });

            return script;
        }

        private void SetPositionAndSize() {
            _windowRc = RawInput.GetWindowRECT(this);
        }

        private string GetPlayingFile() {
            return _startArgs.RuntimeType switch {
                "RImage" => Constants.PlayingFile.PlayerWeb,
                "RImage3D" => Constants.PlayingFile.PlayerWeb3D,
                "RVideo" => Constants.PlayingFile.PlayerWeb,
                _ => throw new ArgumentException(nameof(_startArgs.RuntimeType)),
            };
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

        private static readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1"
        }; // workaround: avoid cache
        private readonly StartArgs _startArgs;
        private static bool _isPaused = false;
        private static bool _isParallaxOn = true;
        private static int _isParallaxRunning = 0;
        private static bool _isFocusOnDesk = false;
        private Native.RECT _windowRc;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly string _filePath = string.Empty;
        private static bool _isFirstRun = true;
        private readonly MainWindowViewModel _viewModel;
        private static bool _isClose = false;
    }
}
