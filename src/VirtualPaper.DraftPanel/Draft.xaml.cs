using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.DraftPanel.Views;
using VirtualPaper.Common.Utils.Bridge.Base;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Draft : Page, IDraftPanelBridge {
        public DraftPanelState CurrentState {
            get { return (DraftPanelState)GetValue(CurrentStatProperty); }
            set { SetValue(CurrentStatProperty, value); }
        }
        private static readonly DependencyProperty CurrentStatProperty =
            DependencyProperty.Register(nameof(CurrentState), typeof(DraftPanelState), typeof(Draft), new PropertyMetadata(0));

        public Draft() {
            this.InitializeComponent();
        }

        #region bridge
        public T GetRequiredService<T>(
                ObjectLifetime lifetime = ObjectLifetime.Transient,
                ObjectLifetime lifetimeForParams = ObjectLifetime.Transient,
                object scope = null) {
            return _windowBridge.GetRequiredService<T>(lifetime, lifetimeForParams, scope);
        }

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
            NavigetBasedState(CurrentState);
        }

        internal void NavigetBasedState(DraftPanelState nextState) {
            CrossThreadInvoker.InvokeOnUiThread(() => {
                CurrentState = nextState;

                Type targetPageType;
                switch (nextState) {
                    case DraftPanelState.Startup:
                        targetPageType = typeof(GetStart);
                        //FrameCardComp.Content = _windowBridge.GetRequiredService<GetStart>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
                        break;
                    case DraftPanelState.ProjectConfig:
                        targetPageType = typeof(ProjectConfig);
                        break;
                    case DraftPanelState.DraftConfig:
                        targetPageType = typeof(DraftConfig);
                        break;
                    case DraftPanelState.WorkSpace:
                        FrameCardComp.Content = null;
                        FrameCardComp.ForwardStack.Clear();
                        FrameCardComp.BackStack.Clear();
                        return;
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
    }
}
