using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.AppSettingsPanel;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.EditPanel;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.TwoWay;
using VirtualPaper.IntelligentPanel;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;
using VirtualPaper.WpSettingsPanel;
using WinRT.Interop;
using WinUIEx;

namespace VirtualPaper.UI {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : ArcWindow {
        public override ArcWindowHost ContentHost => this.MainHost;
        public override NavigationView AppNavView => this.NavigationViewControl;
        public override bool IsMainWindow => true;
        public override ArcWindowManagerKey Key => _windowKey;

        public MainWindow(IUserSettingsClient userSettings, ICommandsClient commandsClient, ITwoWayClient twoWayClient)
            : base(userSettings.Settings.ApplicationTheme, userSettings.Settings.SystemBackdrop) {
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.Main);
            _userSettings = userSettings;
            this.InitializeComponent();
            this.InitWindowConst();
            base.InitializeWindow();

            _commandsClient = commandsClient;
            _twoWayClient = twoWayClient;
            _commandsClient.UIRecieveCmd += CommandsClient_UIRecieveCmd;
            _twoWayClient.MessageReceived += TwoWayClient_MessageReceived;
            this.AppWindow.Closing += AppWindow_Closing;
            this.Closed += MainWindow_Closed;
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
                    await CloseConfirmedAsync();
                }
            }
            finally {
                Interlocked.Exchange(ref _isCheckingClose, 0);
            }
        }

        private int _isClosing;
        private async Task CloseConfirmedAsync(TwoWayMessage? closeResult = null) {
            bool initiateClose = Interlocked.Exchange(ref _isClosing, 1) == 0;
            if (initiateClose) {
                try {
                    // Arm Core's timeout only after all pages have agreed to close.
                    await _commandsClient.NotifyUIClosingAsync();
                }
                catch (Exception ex) {
                    // Core may already be stopping. A failed notification must not prevent closing.
                    ArcLog.GetLogger<MainWindow>().Warn($"Could not notify Core that UI is closing: {ex.Message}");
                }
            }
            if (closeResult != null) {
                try {
                    // The response stream must not prevent an approved close forever.
                    await _twoWayClient.SendMessageAsync(closeResult).WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex) {
                    ArcLog.GetLogger<MainWindow>().Warn($"Could not send UI close result: {ex.Message}");
                }
            }
            if (!initiateClose) return;
            CrossThreadInvoker.InvokeOnUIThread(() => {
                _isSafeToClose = true;
                this.Hide();
                this.Close();
            });
        }

        private void InitWindowConst() {
            WindowConsts.ArcWindowInstance = this;
            WindowConsts.WindowHandle = WindowNative.GetWindowHandle(this);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args) {
            App.ShutDown();
        }

        #region ipc
        private void CommandsClient_UIRecieveCmd(object? sender, int e) {
            HandleIpcMessage(e);
        }

        private async void TwoWayClient_MessageReceived(object? sender, TwoWayMessage e) {
            try {
                switch (e.Type) {
                    case "REQUEST_CLOSE":
                        var canClose = await NaviContent.CheckAllPagesCanCloseAsync();
                        
                        var closeResult = new TwoWayMessage {
                            Type = "UI_CLOSE_RESULT",
                            RequestId = e.RequestId,
                            Payload = canClose.ToString()
                        };
                        
                        if (canClose) {
                            // Arm the watchdog before sending the result or disposing the stream.
                            await CloseConfirmedAsync(closeResult);
                        }
                        else await _twoWayClient.SendMessageAsync(closeResult);
                        break;
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainWindow>().Error($"TwoWay message handling failed: {ex.Message}");
            }
        }

        private async void HandleIpcMessage(int type) {
            try {
                MessageType messageType = (MessageType)type;
                switch (messageType) {
                    case MessageType.cmd_active:
                        CrossThreadInvoker.InvokeOnUIThread(() => {
                            if (Volatile.Read(ref _isClosing) != 0) return;
                            var hwnd = WindowConsts.WindowHandle;
                            Native.ShowWindow(hwnd, (uint)Native.SHOWWINDOW.SW_RESTORE);
                            _ = Native.SetForegroundWindow(hwnd);
                        });
                        break;
                    case MessageType.cmd_close:
                        var canClose = await NaviContent.CheckAllPagesCanCloseAsync();
                        if (canClose) {
                            await CloseConfirmedAsync();
                        }
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
                    "Nav_Edit" => typeof(Edit),
                    "Nav_AppSettings" => typeof(AppSettings),
                    "Nav_Intelligent" => typeof(Intelligent),
                    _ => throw new NotImplementedException(),
                };

                var options = _userSettings.Settings.NavigationAnimationsEnabled
                    ? new ArcNavigationOptions(ArcNavigationTransition.Fade)
                    : null;
                NaviContent.Navigate(pageType, options: options);
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
        private readonly ITwoWayClient _twoWayClient;
        private readonly ArcWindowManagerKey _windowKey;
    }
}
