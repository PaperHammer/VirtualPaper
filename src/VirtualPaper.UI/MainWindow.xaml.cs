using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
using VirtualPaper.UIComponent.Windowing;
using VirtualPaper.WpSettingsPanel;
using Windows.System;
using WinRT.Interop;

namespace VirtualPaper.UI {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : ArcWindow, IWindowBridge {
        public override IReadOnlyList<FrameworkElement> TitleBarChildren =>
            [this.TitleBarIcon, this.TitleTextBlock, this.SubTitleTextBlock];
        public override Grid AppRoot => this.MainGrid;
        public override Grid AppTitleBar => this.TitleBar;
        public override NavigationView AppNavView => this.NavigationViewControl;
        public override AppSystemBackdrop CurrentBackdrop => _userSettings.Settings.SystemBackdrop;
        public override Image AppThemeTransitionImage => this.ThemeTransitionImage;

        public NavigationView NavigationView {
            get { return NavigationViewControl; }
        }

        public MainWindow(
            MainWindowViewModel viewModel,
            IUserSettingsClient userSettingsClient,
            ICommandsClient commandsClient) : base() {
            this.InitializeComponent();

            _userSettings = userSettingsClient;
            _commandsClient = commandsClient;
            _commandsClient.UIRecieveCmd += CommandsClient_UIRecieveCmd;
            _viewModel = viewModel;
            this.AppRoot.DataContext = _viewModel;

            SetWindowStartupPosition();
            SetWindowStyle();
            SetWindowTitleBar();
        }

        private async void MainGrid_Loaded(object sender, RoutedEventArgs e) {
            AfterRootLoaded();
            await SetThemeAsync(_userSettings.Settings.ApplicationTheme);
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

#if DEBUG
                SubTitleTextBlock.Visibility = Visibility.Visible;
#endif
                this.ExtendsContentIntoTitleBar = true;
                this.SetTitleBar(AppTitleBar);
                this.AppWindow.SetIcon("Assets/virtualpaper.ico");
                titleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
            }
            else {
                AppTitleBar.Visibility = Visibility.Collapsed;                
                this.UseImmersiveDarkModeEx(_userSettings.Settings.ApplicationTheme == AppTheme.Dark);
            }
        }

        private void SetWindowStyle() {
            this.SystemBackdrop = _userSettings.Settings.SystemBackdrop switch {
                AppSystemBackdrop.Mica => new MicaBackdrop(),
                AppSystemBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
                _ => default,
            };
        }

        private void WindowEx_Closed(object sender, WindowEventArgs args) {
            try {
                _commandsClient.UIRecieveCmd -= CommandsClient_UIRecieveCmd;

                if (_userSettings.Settings.IsFirstRun) {
                    args.Handled = true;
                    _userSettings.Settings.IsFirstRun = false;
                    _userSettings.Save<ISettings>();
                    this.Close();
                }

                if (_userSettings.Settings.IsUpdated) {
                    args.Handled = true;
                    _userSettings.Settings.IsUpdated = false;
                    _userSettings.Save<ISettings>();
                    this.Close();
                }

                App.ShutDown();
            }
            catch (InvalidOperationException ex) {
                App.Log.Error("An error ocurred at UI closing: ", ex);
            }
        }
        #endregion

        #region ipc
        private void CommandsClient_UIRecieveCmd(object? sender, int e) {
            HandleIpcMessage(e);
        }

        private void HandleIpcMessage(int type) {
            try {
                MessageType messageType = (MessageType)type;
                switch (messageType) {
                    case MessageType.cmd_active:
                        CrossThreadInvoker.InvokeOnUIThread(() => {
                            this.Activate();
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
        #endregion

        #region navigation control
        private void OnNavigationViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            try {
                var selectedItem = args.SelectedItemContainer;

                if (args.SelectedItemContainer.Name == Nav_WpSettings.Name) {
                    Navigate(typeof(WpSettings), this);
                }
                else if (args.SelectedItemContainer.Name == Nav_Draft.Name) {
                    Navigate(typeof(Draft), this);
                }
                else if (args.SelectedItemContainer.Name == Nav_AppSettings.Name) {
                    Navigate(typeof(AppSettings), this);
                }
            }
            catch (Exception ex) {
                _viewModel._basicComponentUtil.ShowExp(ex);
                App.Log.Error(ex);
            }
        }

        public void Navigate(Type pageType, object? targetPageArguments = null, NavigationTransitionInfo? navigationTransitionInfo = null) {
            rootFrame.Navigate(pageType, targetPageArguments, navigationTransitionInfo);
        }
        #endregion

        private async void LightAndDarkButton_Click(object sender, RoutedEventArgs e) {
            LightAndDarkButton.IsEnabled = false;
            try {
                await SetThemeAsync();
            }
            finally {
                LightAndDarkButton.IsEnabled = true;
            }
        }

        private readonly IUserSettingsClient _userSettings;
        private readonly ICommandsClient _commandsClient;
        internal readonly MainWindowViewModel _viewModel;
        private nint _windowHandle = -1;
    }
}
