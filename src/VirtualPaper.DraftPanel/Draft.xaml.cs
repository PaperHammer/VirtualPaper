using System;
using System.Linq;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.Views;
using VirtualPaper.UIComponent.Attributes;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    [KeepAlive]
    public sealed partial class Draft : ArcPage {
        public override ArcPageContext ArcContext { get; set; }
        public override Type ArcType => typeof(Draft);

        public Draft() {
            this.InitializeComponent();
            ArcContext = new ArcPageContext(this, this.MainHost.LoadingControlHost);
        }

        #region navigate
        private void FrameCardComp_Loaded(object sender, RoutedEventArgs e) {
            Payload = new FrameworkPayload() {
                [NaviPayloadKey.DraftPage.ToString()] = this,
            };
            NavigateByState(DraftPanelState.ConfigSpace);
        }

        public void NavigateByState(DraftPanelState nextPanel, params NaviPayloadData[] naviPayloadDatas) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                Type targetPageType;
                switch (nextPanel) {
                    case DraftPanelState.ConfigSpace:
                        targetPageType = typeof(ConfigSpace);
                        break;
                    case DraftPanelState.WorkSpace:
                        targetPageType = typeof(WorkSpace);
                        FrameCardComp.BackStack.Clear();
                        FrameCardComp.ForwardStack.Clear();
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
                        Payload?.AddRange(naviPayloadDatas);
                        FrameCardComp.Navigate(targetPageType, Payload);
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
    }
}
