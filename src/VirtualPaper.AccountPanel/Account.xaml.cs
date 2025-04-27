using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.AccountPanel.Views;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Models.AccountPanel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AccountPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Account : Page, IAccountPanelBridge {
        internal static Account Instance { get; private set; }

        public Account() {
            Instance = this;
            this._currentPanel = AccountPanelState.Passport;
            this.InitializeComponent();
        }

        #region bridge
        public void ChangePanelState(AccountPanelState nextPanel, object data) {
            _sharedData = data;
            NavigetBasedState(nextPanel);
        }

        public object GetSharedData() => _sharedData;

        public IDialogService GetDialog() => _windowBridge.GetDialog();

        public INoifyBridge GetNotify() => _windowBridge.GetNotify();

        public nint GetWindowHandle() => _windowBridge.GetWindowHandle();

        public void Log(LogType type, object message) {
            _windowBridge.Log(type, message);
        }
        #endregion

        #region navigate
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            this._windowBridge ??= e.Parameter as IWindowBridge;
        }

        private void FrameCardComp_Loaded(object sender, RoutedEventArgs e) {
            NavigetBasedState(_currentPanel);
        }

        private void NavigetBasedState(AccountPanelState nextPanel) {
            CrossThreadInvoker.InvokeOnUiThread(() => {
                _currentPanel = nextPanel;

                Type targetPageType;
                switch (_currentPanel) {
                    case AccountPanelState.Passport:
                        targetPageType = typeof(Passport);
                        break;
                    case AccountPanelState.UserCenter:
                        targetPageType = typeof(UserCenter);
                        break;
                    default:
                        return;
                }

                if (targetPageType != null) {
                    if (IsNextPageTarget(targetPageType)) {
                        FrameCardComp.GoForward();
                    }
                    else if (IsPreviousPageTarget(targetPageType)) {
                        FrameCardComp.GoBack();
                    }
                    else {
                        FrameCardComp.Navigate(targetPageType, this);
                    }
                }
            });
        }

        private bool IsNextPageTarget(Type targetPageType) {
            if (FrameCardComp.ForwardStack.Count > 0) {
                var nextPage = FrameCardComp.ForwardStack.First();
                return nextPage.SourcePageType == targetPageType;
            }
            return false;
        }

        private bool IsPreviousPageTarget(Type targetPageType) {
            if (FrameCardComp.BackStack.Count > 0) {
                var previousPage = FrameCardComp.BackStack.Last();
                return previousPage.SourcePageType == targetPageType;
            }
            return false;
        }
        #endregion

        private IWindowBridge _windowBridge;
        private AccountPanelState _currentPanel;
        private object _sharedData;
    }
}
