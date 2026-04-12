using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.Views;
using VirtualPaper.UIComponent.Attributes;
using VirtualPaper.UIComponent.Navigation.Interfaces;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Workloads.Utils.DraftUtils.Interfaces;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    [KeepAlive]
    public sealed partial class Draft : ArcPage, IConfirmClose {
        public override Type ArcType => typeof(Draft);

        public Draft() {
            this.InitializeComponent();
            ArcContext.AttachLoadingComponent(this.MainHost.LoadingControlHost);
        }

        public async Task<bool> CanCloseAsync() {
            if (FrameCardComp.Content is IConfirmClose currentSubPage) {
                bool canClose = await currentSubPage.CanCloseAsync();

                if (!canClose) {
                    return false;
                }
            }

            return true;
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
