using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.AccountPanel;
using VirtualPaper.AppSettingsPanel;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel;
using VirtualPaper.GalleryPanel;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UI.ViewModels;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;
using VirtualPaper.WpSettingsPanel;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx, IWindowBridge {
        public MainWindow(
            MainWindowViewModel mainWindowViewModel,
            ICommandsClient commandsClient,
            IUserSettingsClient userSettingsClient) {
            this.InitializeComponent();

            _commandsClient = commandsClient;
            _commandsClient.UIRecieveCmd += CommandsClient_UIRecieveCmd;
            _userSettingsClient = userSettingsClient;
            _viewModel = mainWindowViewModel;
            this.MainGrid.DataContext = _viewModel;

            SetWindowStartupPosition();
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
                this.UseImmersiveDarkModeEx(_userSettingsClient.Settings.ApplicationTheme == AppTheme.Dark);
            }
        }

        private void SetWindowStyle() {
            this.SystemBackdrop = _userSettingsClient.Settings.SystemBackdrop switch {
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
                        CrossThreadInvoker.InvokeOnUiThread(() => {
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

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            try {
                Type pageType = null;
                if (args.SelectedItemContainer.Name == Gallery.Name) {
                    pageType = typeof(Gallery);
                }
                else if (args.SelectedItemContainer.Name == Nav_WpSettings.Name) {
                    pageType = typeof(WpSettings);
                }
                else if (args.SelectedItemContainer.Name == Nav_Draft.Name) {
                    pageType = typeof(Draft);
                }
                else if (args.SelectedItemContainer.Name == Nav_Account.Name) {
                    pageType = typeof(Account);
                }
                else if (args.SelectedItemContainer.Name == Nav_AppSettings.Name) {
                    pageType = typeof(AppSettings);
                }

                if (pageType != null) {
                    ContentFrame.Navigate(pageType, this);
                }
            }
            catch (Exception ex) {
                _viewModel._basicComponentUtil.ShowExp(ex);
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
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly MainWindowViewModel _viewModel;

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
