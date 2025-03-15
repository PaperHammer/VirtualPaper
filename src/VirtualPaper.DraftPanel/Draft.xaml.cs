using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.Views;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Draft : Page, IDraftPanelBridge {
        internal static IDraftPanelBridge DraftPanelBridge { get; private set; }

        public Draft() {
            this.InitializeComponent();

            this._currentState = DraftPanelState.ConfigSpace;
            DraftPanelBridge = this;
        }

        #region bridge
        public nint GetWindowHandle() {
            return _windowBridge.GetWindowHandle();
        }

        public object GetParam() {
            return _param;
        }

        public void ChangePanelState(DraftPanelState nextState, object param = null) {
            _param = param;
            NavigetBasedState(nextState);
        }

        public void Log(LogType type, object message) {
            _windowBridge.Log(type, message);
        }

        public INoifyBridge GetNotify() {
            return _windowBridge.GetNotify();
        }

        public uint GetDpi() {
            return _windowBridge.GetDpi();
        }
        #endregion

        #region navigate
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            this._windowBridge ??= e.Parameter as IWindowBridge;
        }

        private void FrameCardComp_Loaded(object sender, RoutedEventArgs e) {
            NavigetBasedState(_currentState);
        }

        private void NavigetBasedState(DraftPanelState nextState) {
            CrossThreadInvoker.InvokeOnUiThread(() => {
                _currentState = nextState;

                Type targetPageType;
                switch (nextState) {
                    case DraftPanelState.ConfigSpace:
                        targetPageType = typeof(ConfigSpace);
                        break;
                    case DraftPanelState.WorkSpace:
                        targetPageType = typeof(WorkSpace);
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
                        FrameCardComp.Navigate(targetPageType);
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

        private object _param;
        private IWindowBridge _windowBridge;
        private DraftPanelState _currentState;
    }
}
