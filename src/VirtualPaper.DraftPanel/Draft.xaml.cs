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
using Windows.Graphics;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Draft : Page, IDraftPanelBridge {
        //public DraftPanelState CurrentState {
        //    get { return _currentState; }
        //    set { _currentState = value; }
        //}

        public Draft() {
            this.InitializeComponent();
        }

        #region bridge
        //public T GetRequiredService<T>(
        //        ObjectLifetime lifetime = ObjectLifetime.Transient,
        //        ObjectLifetime lifetimeForParams = ObjectLifetime.Transient,
        //        object scope = null) {
        //    return _windowBridge.GetRequiredService<T>(lifetime, lifetimeForParams, scope);
        //}

        public nint GetWindowHandle() {
            return _windowBridge.GetWindowHandle();
        }

        public object GetParam() {
            return _param;
        }

        public void ChangeProjectPanelState(DraftPanelState nextState, object param = null) {
            _param = param;
            NavigetBasedState(nextState);
        }

        public PointInt32 GetWindowLocation() {
            return (_windowBridge.GetMainWindow() as WindowEx).AppWindow.Position;
        }
        #endregion

        #region navigate
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (this._windowBridge == null) {
                //FrameCardComp.CacheSize = 4;
                this._windowBridge = e.Parameter as IWindowBridge;
            }
        }

        private void FrameCardComp_Loaded(object sender, RoutedEventArgs e) {
            NavigetBasedState(_currentState);
        }

        private void FrameCardComp_Unloaded(object sender, RoutedEventArgs e) {
            var frame = sender as Frame;
            frame.Content = null;
            frame.ForwardStack.Clear();
            frame.BackStack.Clear();
        }

        internal void NavigetBasedState(DraftPanelState nextState) {
            CrossThreadInvoker.InvokeOnUiThread(() => {
                _currentState = nextState;

                Type targetPageType;
                switch (nextState) {
                    case DraftPanelState.Startup:
                        targetPageType = typeof(GetStart);
                        break;
                    case DraftPanelState.ProjectConfig:
                        targetPageType = typeof(ProjectConfig);
                        break;
                    case DraftPanelState.DraftConfig:
                        targetPageType = typeof(DraftConfig);
                        break;
                    case DraftPanelState.WorkSpace:
                        targetPageType = typeof(WorkSpace);
                        FrameCardComp.ForwardStack.Clear();
                        FrameCardComp.BackStack.Clear();
                        break;
                    //FrameCardComp.Content = null;
                    //FrameCardComp.ForwardStack.Clear();
                    //FrameCardComp.BackStack.Clear();
                    //return;
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
        private object _param;
        private DraftPanelState _currentState;
    }
}
