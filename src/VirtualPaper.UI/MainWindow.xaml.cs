using System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.AppSettingsPanel;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UI.ViewModels;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Logging;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel;
using WinRT.Interop;

namespace VirtualPaper.UI {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : ArcWindow {
        public override ArcWindowHost ContentHost => this.MainHost;
        public override NavigationView AppNavView => this.NavigationViewControl;

        public MainWindow(
            MainWindowViewModel viewModel,
            IUserSettingsClient userSettings,
            ICommandsClient commandsClient) : base(userSettings.Settings.ApplicationTheme, userSettings.Settings.SystemBackdrop) {
            this.InitializeComponent();
            base.InitializeWindow();
            this.InitWindowConst();

            _userSettings = userSettings;
            _commandsClient = commandsClient;
            _commandsClient.UIRecieveCmd += CommandsClient_UIRecieveCmd;
            _viewModel = viewModel;
            this.ContentHost.AppRoot.DataContext = _viewModel;
        }

        private void InitWindowConst() {
            WindowConsts.ArcWindowInstance = this;
            WindowConsts.Dpi = SystemUtil.GetDpi(SystemUtil.GetDisplayArea(this, DisplayAreaFallback.Primary)); ;
            WindowConsts.WindowHandle = WindowNative.GetWindowHandle(this);
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
                ArcLog.GetLogger<MainWindow>().Error("An error ocurred at UI closing: ", ex);
            }
        }

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
                ArcLog.GetLogger<MainWindow>().Error(ex);
            }
        }
        #endregion

        #region navigation control
        private void OnNavigationViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            try {
                Type pageType = args.SelectedItemContainer.Name switch {
                    "Nav_WpSettings" => typeof(WpSettings),
                    "Nav_Draft" => typeof(Draft),
                    "Nav_AppSettings" => typeof(AppSettings),
                    _ => throw new NotImplementedException(),
                };
                
                FrameNavigationOptions navOptions = new() {
                    TransitionInfoOverride = args.RecommendedNavigationTransitionInfo,
                    IsNavigationStackEnabled = false
                };
                ContentFrame.NavigateToType(pageType, this, navOptions);
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowException(ex);
                ArcLog.GetLogger<MainWindow>().Error(ex);
            }
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
    }
}
