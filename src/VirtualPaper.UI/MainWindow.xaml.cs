using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using VirtualPaper.AppSettingsPanel;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UI.ViewModels;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;
using VirtualPaper.WpSettingsPanel;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;

namespace VirtualPaper.UI {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx, IWindowBridge {
        public NavigationView NavigationView {
            get { return NavigationViewControl; }
        }

        public MainWindow(
            MainWindowViewModel viewModel,
            ICommandsClient commandsClient) {
            this.InitializeComponent();

            _commandsClient = commandsClient;
            _commandsClient.UIRecieveCmd += CommandsClient_UIRecieveCmd;
            _viewModel = viewModel;
            this.MainGrid.DataContext = _viewModel;

            SetWindowStartupPosition();
            SetWindowStyle();
            SetWindowTitleBar();
        }

        private void CommandsClient_UIRecieveCmd(object? sender, int e) {
            HandleIpcMessage(e);
        }

        #region bridge
        public nint GetWindowHandle() {
            return _windowHandle = WindowNative.GetWindowHandle(this);
        }

        public async Task<string?> GetStorageFolderAsync() {
            var storageFolder = await WindowsStoragePickers.PickFolderAsync(_windowHandle == -1 ? GetWindowHandle() : _windowHandle);
            if (storageFolder == null) return null;

            return storageFolder.Path;
        }

        public uint GetDpi() {
            return SystemUtil.GetDpi(SystemUtil.GetDisplayArea(this, DisplayAreaFallback.Primary));
        }

        public INoifyBridge GetNotify() {
            return _viewModel._basicComponentUtil;
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

        public object GetMainWindow() {
            return this;
        }

        public IDialogService GetDialog() {
            return _viewModel._dialog;
        }
        #endregion

        #region window property
        private void SetWindowStartupPosition() {
            DisplayArea displayArea = SystemUtil.GetDisplayArea(this, DisplayAreaFallback.Nearest);
            if (displayArea is not null) {
                var centeredPosition = this.AppWindow.Position;
                centeredPosition.X = (displayArea.WorkArea.Width - this.AppWindow.Size.Width) / 2;
                centeredPosition.Y = (displayArea.WorkArea.Height - this.AppWindow.Size.Height) / 2;
                this.AppWindow.Move(centeredPosition);
            }
        }

        private void SetWindowTitleBar() {
            //ref: https://learn.microsoft.com/en-us/windows/apps/develop/title-bar?tabs=wasdk
            if (AppWindowTitleBar.IsCustomizationSupported()) {
                var titleBar = this.AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonForegroundColor = ResourcesUtil.GetBrush(Constants.ColorKey.WindowCaptionForeground).Color;

                AppTitleBar.Loaded += AppTitleBar_Loaded;
                AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
            }
            else {
                AppTitleBar.Visibility = Visibility.Collapsed;
                this.UseImmersiveDarkModeEx(_viewModel._userSettings.Settings.ApplicationTheme == AppTheme.Dark);
            }
        }

        private void SetWindowStyle() {
            this.SystemBackdrop = _viewModel._userSettings.Settings.SystemBackdrop switch {
                AppSystemBackdrop.Mica => new MicaBackdrop(),
                AppSystemBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
                _ => default,
            };
        }

        private void WindowEx_Activated(object sender, WindowActivatedEventArgs args) {
            if (args.WindowActivationState == WindowActivationState.Deactivated) {
                TitleTextBlock.Foreground = ResourcesUtil.GetBrush(Constants.ColorKey.WindowCaptionForegroundDisabled);
            }
            else {
                TitleTextBlock.Foreground = ResourcesUtil.GetBrush(Constants.ColorKey.WindowCaptionForeground);
            }
        }

        private void WindowEx_Closed(object sender, WindowEventArgs args) {
            try {
                _commandsClient.UIRecieveCmd -= CommandsClient_UIRecieveCmd;

                if (_viewModel._userSettings.Settings.IsFirstRun) {
                    args.Handled = true;
                    _viewModel._userSettings.Settings.IsFirstRun = false;
                    _viewModel._userSettings.Save<ISettings>();
                    this.Close();
                }

                if (_viewModel._userSettings.Settings.IsUpdated) {
                    args.Handled = true;
                    _viewModel._userSettings.Settings.IsUpdated = false;
                    _viewModel._userSettings.Save<ISettings>();
                    this.Close();
                }

                App.ShutDown();
            }
            catch (InvalidOperationException ex) {
                App.Log.Error("An error ocurred at UI closing: ", ex);
            }
        }
        #endregion

        private void HandleIpcMessage(int type) {
            try {
                MessageType messageType = (MessageType)type;
                switch (messageType) {
                    case MessageType.cmd_active:
                        CrossThreadInvoker.InvokeOnUIThread(() => {
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

        #region navigation control
        private void OnNavigationViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            try {
                var selectedItem = args.SelectedItemContainer;

                if (args.SelectedItemContainer.Name == Nav_WpSettings.Name) {
                    Navigate(typeof(WpSettings));
                }
                else if (args.SelectedItemContainer.Name == Nav_Draft.Name) {
                    Navigate(typeof(Draft));
                }
                else if (args.SelectedItemContainer.Name == Nav_AppSettings.Name) {
                    Navigate(typeof(AppSettings));
                }
            }
            catch (Exception ex) {
                _viewModel._basicComponentUtil.ShowExp(ex);
                App.Log.Error(ex);
            }
        }

        // Wraps a call to rootFrame.Navigate to give the Page a way to know which NavigationRootPage is navigating.
        // Please call this function rather than rootFrame.Navigate to navigate the rootFrame.
        public void Navigate(Type pageType, object? targetPageArguments = null, NavigationTransitionInfo? navigationTransitionInfo = null) {
            rootFrame.Navigate(pageType, targetPageArguments, navigationTransitionInfo);
        }
        #endregion

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
                double scaleAdjustment = SystemUtil.GetScaleAdjustment(this);

                RightPaddingColumn.Width = new GridLength(appWindow.TitleBar.RightInset / scaleAdjustment);
                LeftPaddingColumn.Width = new GridLength(appWindow.TitleBar.LeftInset / scaleAdjustment);

                List<RectInt32> dragRectsList = [];

                RectInt32 dragRectL;
                dragRectL.X = (int)((LeftPaddingColumn.ActualWidth) * scaleAdjustment);
                dragRectL.Y = 0;
                dragRectL.Height = (int)(AppTitleBar.ActualHeight * scaleAdjustment);
                dragRectL.Width = (int)((IconColumn.ActualWidth
                                        + TitleColumn.ActualWidth
                                        + LeftDragColumn.ActualWidth) * scaleAdjustment);
                dragRectsList.Add(dragRectL);

                RectInt32 dragRectR;
                dragRectR.X = (int)((LeftPaddingColumn.ActualWidth
                                    + IconColumn.ActualWidth
                                    + TitleTextBlock.ActualWidth
                                    + LeftDragColumn.ActualWidth) * scaleAdjustment);
                dragRectR.Y = 0;
                dragRectR.Height = (int)(AppTitleBar.ActualHeight * scaleAdjustment);
                dragRectR.Width = (int)(RightDragColumn.ActualWidth * scaleAdjustment);
                dragRectsList.Add(dragRectR);

                RectInt32[] dragRects = [.. dragRectsList];

                appWindow.TitleBar.SetDragRectangles(dragRects);
            }
        }
        #endregion

        private readonly ICommandsClient _commandsClient;
        private readonly MainWindowViewModel _viewModel;
        private nint _windowHandle = -1;

        private void LightAndDarkButton_Click(object sender, RoutedEventArgs e) {
            //if (a == 1) {
            //    aaa(ElementTheme.Dark);
            //}
            //else {
            //    aaa(ElementTheme.Light);
            //}
            //a ^= 1;
        }
        private int a = 1;
    }
}
