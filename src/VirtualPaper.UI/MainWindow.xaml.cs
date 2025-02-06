using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.AppSettingsPanel;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.DraftPanel;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels;
using VirtualPaper.UIComponent.Utils.Extensions;
using VirtualPaper.WpSettingsPanel;
using Windows.UI;
using WinRT.Interop;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx, IWindowBridge {
        //public string WindowStyleType { get; private set; }
        //public SolidColorBrush WindowCaptionForeground => (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
        //public SolidColorBrush WindowCaptionForegroundDisabled => (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];

        public MainWindow(
            MainWindowViewModel mainWindowViewModel,
            ICommandsClient commandsClient,
            IWallpaperControlClient wallpaperControlClient,
            IUserSettingsClient userSettingsClient) {
            _commandsClient = commandsClient;
            _wpControl = wallpaperControlClient;
            _userSettingsClient = userSettingsClient;

            this.InitializeComponent();

            _basicUIComponent = new(mainWindowViewModel);
            _dialog = new();

            _viewModel = mainWindowViewModel;
            this.MainGrid.DataContext = _viewModel;

            _commandsClient.UIRecieveCmd += CommandsClient_UIRecieveCmd;
            //_ctsConsoleIn = new();

            SetWindowStyle();
            SetWindowTitleBar();
        }

        private void CommandsClient_UIRecieveCmd(object sender, int e) {
            HandleIpcMessage(e);
        }

        #region bridge
        public nint GetWindowHandle() {
            return WindowNative.GetWindowHandle(this);
        }

        public INoifyBridge GetNotify() {
            return _basicUIComponent;
        }

        public T GetRequiredService<T>(
            ObjectLifetime lifetime = ObjectLifetime.Transient,
            ObjectLifetime lifetimeForParams = ObjectLifetime.Transient,
            object scope = null) {
            return ObjectProvider.GetRequiredService<T>(lifetime, lifetimeForParams, scope);
        }

        public void Log(LogType type, object message) {
            switch (type) {
                case LogType.Info:
                    App.Log.Info(message);
                    break;
                case LogType.Warn:
                    App.Log.Warn(message);
                    break;
                case LogType.Error:
                    App.Log.Error(message);
                    break;
                case LogType.Trace:
                    App.Log.Trace(message);
                    break;
                default:
                    break;
            }
        }

        public object GetCompositor() {
            return this.Compositor;
        }

        public object GetMainWindow() {
            return this;
        }

        public IDialogService GetDialog() {
            return _dialog;
        }

        public Color GetColorByKey(string key) {
            return _colors.GetValueOrDefault(key);
        }
        #endregion

        #region window property
        private void SetWindowTitleBar() {
            //ref: https://learn.microsoft.com/en-us/windows/apps/develop/title-bar?tabs=wasdk
            if (AppWindowTitleBar.IsCustomizationSupported()) {
                var titleBar = this.AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonForegroundColor = _colors[Constants.ColorKey.WindowCaptionForeground];

                AppTitleBar.Loaded += AppTitleBar_Loaded;
                AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
            }
            else {
                AppTitleBar.Visibility = Visibility.Collapsed;
                this.UseImmersiveDarkModeEx(_userSettingsClient.Settings.ApplicationTheme == AppTheme.Dark);
            }
        }

        private void SetWindowStyle() {
            //string type = _userSettingsClient.Settings.SystemBackdrop;
            //WindowStyleType = type;
            this.SystemBackdrop = _userSettingsClient.Settings.SystemBackdrop switch {
                AppSystemBackdrop.Mica => new MicaBackdrop(),
                AppSystemBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
                _ => default,
            };
        }

        private void WindowEx_Activated(object sender, WindowActivatedEventArgs args) {
            if (args.WindowActivationState == WindowActivationState.Deactivated) {
                TitleTextBlock.Foreground = new SolidColorBrush(_colors[Constants.ColorKey.WindowCaptionForegroundDisabled]);
            }
            else {
                TitleTextBlock.Foreground = new SolidColorBrush(_colors[Constants.ColorKey.WindowCaptionForeground]);
            }

            //_ = StdInListener();
        }

        private async void WindowEx_Closed(object sender, WindowEventArgs args) {
            await _wpControl.CloseAllPreviewAsync();

            if (_userSettingsClient.Settings.IsFirstRun) {
                args.Handled = true;
                _userSettingsClient.Settings.IsFirstRun = false;
                _userSettingsClient.Save<ISettings>();
                this.Close();
            }

            if (_userSettingsClient.Settings.IsUpdated) {
                args.Handled = true;
                _userSettingsClient.Settings.IsUpdated = false;
                _userSettingsClient.Save<ISettings>();
                this.Close();
            }

            App.ShutDown();
        }
        #endregion

//        private async Task StdInListener() {
//            try {
//                await Task.Run(async () => {
//                    while (!_ctsConsoleIn.IsCancellationRequested) {
//                        var msg = await Console.In.ReadLineAsync(_ctsConsoleIn.Token);
//                        if (string.IsNullOrEmpty(msg)) {
//                            //When the redirected stream is closed, a null line is sent to the event handler. 
//#if !DEBUG
//                            break;
//#endif
//                        }
//                        else {
//                            HandleIpcMessage(msg);
//                        }
//                    }
//                });
//            }
//            catch (Exception ex) {
//                App.Log.Error(ex);
//            }
//            finally {
//                Closing();
//            }
//        }

        private void HandleIpcMessage(int type) {
            try {
                MessageType messageType = (MessageType)type;
                switch (messageType) {
                    case MessageType.cmd_active:
                        App.UITaskInvokeQueue.TryEnqueue(() => {
                            this.BringToFront();
                        });
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported message type: {messageType}");
                }
            }
            catch (Exception ex) {
                App.Log.Error(ex);
            }
        }

        private void Closing() {
            this.Hide();
            //_ctsConsoleIn?.Cancel();
            _commandsClient.UIRecieveCmd -= CommandsClient_UIRecieveCmd;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            try {
                FrameNavigationOptions navOptions = new() {
                    TransitionInfoOverride = args.RecommendedNavigationTransitionInfo,
                    IsNavigationStackEnabled = false
                };

                Type pageType = null;
                //if (args.SelectedItemContainer.Name == Gallery.Name) {
                //    pageType = typeof(Gallery);
                //}
                //else 
                if (args.SelectedItemContainer.Name == Nav_WpSettings.Name) {
                    pageType = typeof(WpSettings);
                }
                else if (args.SelectedItemContainer.Name == Nav_Draft.Name) {
                    pageType = typeof(Draft);
                }
                //else if (args.SelectedItemContainer.Name == Account.Name) {
                //    pageType = typeof(Account);
                //}
                else if (args.SelectedItemContainer.Name == Nav_AppSettings.Name) {
                    pageType = typeof(AppSettings);
                }

                ContentFrame.NavigateToType(pageType, this, navOptions);
            }
            catch (Exception ex) {
                _basicUIComponent.ShowExp(ex);
                App.Log.Error(ex);
            }
        }

        #region window title bar
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

                Windows.Graphics.RectInt32[] dragRects = [.. dragRectsList];

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

        private readonly ICommandsClient _commandsClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly IWallpaperControlClient _wpControl;
        private readonly MainWindowViewModel _viewModel;
        //private static CancellationTokenSource _ctsConsoleIn;
        private readonly BasicUIComponentUtil _basicUIComponent;
        private readonly DialogUtil _dialog;
        private readonly Dictionary<string, Color> _colors = new() {
            [Constants.ColorKey.WindowCaptionForeground] = ((SolidColorBrush)App.Current.Resources[Constants.ColorKey.WindowCaptionForeground]).Color,
            [Constants.ColorKey.WindowCaptionForegroundDisabled] = ((SolidColorBrush)App.Current.Resources[Constants.ColorKey.WindowCaptionForegroundDisabled]).Color,
        };

        //private void Flyout_BackgreoundTask_Opening(object sender, object e) {

        //}

        //private void Flyout_Closing(Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase sender, Microsoft.UI.Xaml.Controls.Primitives.FlyoutBaseClosingEventArgs args) {
        //    args.Cancel = true;
        //}

        //private void CancelBgTaskBtn_Click(object sender, RoutedEventArgs e) {
        //    BackgroundTask task = sender as BackgroundTask;
        //    task?.Cancel?.Invoke();
        //}
    }
}
