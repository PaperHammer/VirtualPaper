using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.AccountPanel.Views.PassportComponents;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.Common.Utils.ThreadContext;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AccountPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Passport : Page, IAccountPanelBridge {
        public Passport() {
            this.InitializeComponent();
            _currentPanel = AccountPanelState.Login;
        }

        #region
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
        }

        private void FrameComp_Loaded(object sender, RoutedEventArgs e) {
            NavigetBasedState(_currentPanel);
        }

        internal void NavigetBasedState(AccountPanelState nextState) {
            CrossThreadInvoker.InvokeOnUiThread(() => {
                _currentPanel = nextState;

                Type targetPageType;
                switch (_currentPanel) {
                    case AccountPanelState.Login:
                        targetPageType = typeof(Login);
                        break;
                    case AccountPanelState.Register:
                        targetPageType = typeof(Register);
                        break;
                    default:
                        Account.Instance.ChangePanelState(nextState, _sharedData);
                        return;
                }

                if (targetPageType != null) {
                    if (IsNextPageTarget(targetPageType)) {
                        FrameComp.GoForward();
                    }
                    else if (IsPreviousPageTarget(targetPageType)) {
                        FrameComp.GoBack();
                    }
                    else {
                        FrameComp.Navigate(targetPageType, this);
                    }
                }
            });
        }

        private bool IsNextPageTarget(Type targetPageType) {
            if (FrameComp.ForwardStack.Count > 0) {
                var nextPage = FrameComp.ForwardStack.First();
                return nextPage.SourcePageType == targetPageType;
            }
            return false;
        }

        private bool IsPreviousPageTarget(Type targetPageType) {
            if (FrameComp.BackStack.Count > 0) {
                var previousPage = FrameComp.BackStack.Last();
                return previousPage.SourcePageType == targetPageType;
            }
            return false;
        }
        #endregion

        #region bridge
        public void ChangePanelState(AccountPanelState nextPanel, object data) {
            _sharedData = data;
            NavigetBasedState(nextPanel);
        }

        public object GetSharedData() => _sharedData;

        public IDialogService GetDialog() => Account.Instance.GetDialog();

        public INoifyBridge GetNotify() => Account.Instance.GetNotify();

        public nint GetWindowHandle() => Account.Instance.GetWindowHandle();

        public void Log(LogType type, object message) => Account.Instance.Log(type, message);
        #endregion

        private AccountPanelState _currentPanel;
        private object _sharedData;
    }
}
