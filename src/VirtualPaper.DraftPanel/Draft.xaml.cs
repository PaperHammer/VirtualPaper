using System;
using System.Linq;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.Views;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Draft : ArcPage, IDraftPanelBridge {
        internal static IDraftPanelBridge Instance { get; private set; }
        public override ArcPageContext Context { get; }
        public override Type PageType => typeof(Draft);

        public Draft() {
            this.InitializeComponent();
            Context = new ArcPageContext(this, this.MainHost.LoadingControlHost);
            Instance = this;
            this._currentPanel = DraftPanelState.ConfigSpace;            
        }

        #region bridge
        public void ChangePanelState(DraftPanelState nextPanel, object? data) {
            _sharedData = data;
            NavigetBasedState(nextPanel);
        }

        public object? GetSharedData() => _sharedData;
        #endregion

        #region navigate
        private void FrameCardComp_Loaded(object sender, RoutedEventArgs e) {
            NavigetBasedState(_currentPanel);
        }

        private void NavigetBasedState(DraftPanelState nextPanel) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                _currentPanel = nextPanel;

                Type targetPageType;
                switch (_currentPanel) {
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

        private DraftPanelState _currentPanel;
        private object? _sharedData;
    }
}
