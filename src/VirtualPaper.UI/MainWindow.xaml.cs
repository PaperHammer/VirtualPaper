using System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.AppSettingsPanel;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent;
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
        public override bool IsMainWindow => true;
        public override ArcWindowManagerKey Key => _windowKey;

        public MainWindow(IUserSettingsClient userSettings, ICommandsClient commandsClient) 
            : base(userSettings.Settings.ApplicationTheme, userSettings.Settings.SystemBackdrop) {
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.Main);
            this.InitializeComponent();
            this.InitWindowConst();
            base.InitializeWindow();

            _userSettings = userSettings;
            _commandsClient = commandsClient;
            _commandsClient.UIRecieveCmd += CommandsClient_UIRecieveCmd;
        }

        private void InitWindowConst() {
            WindowConsts.ArcWindowInstance = this;
            //WindowConsts.Dpi = SystemUtil.GetDpi(SystemUtil.GetDisplayArea(this, DisplayAreaFallback.Primary));
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

                NaviContent.Navigate(pageType);
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(Key), ex);
                ArcLog.GetLogger<MainWindow>().Error(ex);
            }
        }
        #endregion

        private void LightAndDarkButton_Click(object sender, RoutedEventArgs e) {
            LightAndDarkButton.IsEnabled = false;
            try {
                var nxTheme = GetNextTheme(ArcThemeUtil.MainWindowAppTheme);
                //await SetThemeAsync(nxTheme);
                UpdateThemeFromThemeBtnClick(nxTheme);
            }
            finally {
                LightAndDarkButton.IsEnabled = true;
            }
        }

        private static AppTheme GetNextTheme(AppTheme current) {
            return current switch {
                AppTheme.Light => AppTheme.Dark,
                AppTheme.Dark => AppTheme.Auto,
                AppTheme.Auto => AppTheme.Light,
                _ => AppTheme.Light
            };
        }

        private readonly IUserSettingsClient _userSettings;
        private readonly ICommandsClient _commandsClient;
        private readonly ArcWindowManagerKey _windowKey;
    }
}
