using System;
using System.Threading;
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

            this.AppWindow.Closing += AppWindow_Closing;
        }

        private bool _isSafeToClose = false;
        private int _isCheckingClose = 0;
        private async void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args) {
            if (_isSafeToClose) return;

            args.Cancel = true;

            if (Interlocked.CompareExchange(ref _isCheckingClose, 1, 0) != 0) {
                return; // 锁被占用，忽略后续的重复点击，不再进入执行
            }

            try {
                bool canClose = await NaviContent.CheckAllPagesCanCloseAsync();
                if (canClose) {
                    _isSafeToClose = true;
                    this.Close();
                }
            }
            finally {
                Interlocked.Exchange(ref _isCheckingClose, 0);
            }
        }

        private void InitWindowConst() {
            WindowConsts.ArcWindowInstance = this;
            WindowConsts.WindowHandle = WindowNative.GetWindowHandle(this);
        }

        private void WindowEx_Closed(object sender, WindowEventArgs args) {
            App.ShutDown();
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
                GlobalMessageUtil.ShowException(ex, arcWindow: ArcWindowManager.GetArcWindow(Key));
                ArcLog.GetLogger<MainWindow>().Error(ex);
            }
        }
        #endregion

        private void LightAndDarkButton_Click(object sender, RoutedEventArgs e) {
            LightAndDarkButton.IsEnabled = false;
            try {
                var nxTheme = GetNextTheme(ArcThemeUtil.MainWindowAppTheme);
                UpdateThemeFromThemeBtnClick(nxTheme);
                _userSettings.Settings.ApplicationTheme = nxTheme;
                _userSettings.SaveAsync<ISettings>();
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
