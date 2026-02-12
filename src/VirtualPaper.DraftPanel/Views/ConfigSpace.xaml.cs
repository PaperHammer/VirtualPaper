using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.DraftPanel.Views.ConfigSpaceComponents;
using VirtualPaper.UIComponent.Data;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConfigSpace : ArcPage, ICardComponent, INavigateComponent {
        public override Type ArcType => typeof(ConfigSpace);

        public ConfigSpace() {
            this.InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<ConfigSpaceViewModel>();
            this.DataContext = _viewModel;            
        }

        #region nav
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is FrameworkPayload payload) {
                payload.TryGet(NaviPayloadKey.DraftPage, out _draftPage);
                Payload = Payload.Merge(payload);
                Payload?.Set(NaviPayloadKey.ICardComponent, this);
                Payload?.Set(NaviPayloadKey.INavigateComponent, this);
                Payload?.Set(NaviPayloadKey.ConfigSpacePage, this);
            }
        }

        private void FrameComp_Loaded(object sender, RoutedEventArgs e) {
            NavigateByState(DraftPanelState.GetStart);
        }

        public void NavigateByState(DraftPanelState nextState, params NaviPayloadData[] naviPayloadDatas) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                Type targetPageType;
                switch (nextState) {
                    case DraftPanelState.GetStart:
                        targetPageType = typeof(GetStart);
                        break;
                    case DraftPanelState.DraftConfig:
                        targetPageType = typeof(DraftConfig);
                        break;
                    default:
                        _draftPage?.NavigateByState(nextState, Payload?.ToArray() ?? []);
                        FrameComp.BackStack.Clear();
                        FrameComp.ForwardStack.Clear();
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
                        Payload?.AddRange(naviPayloadDatas);
                        FrameComp.Navigate(targetPageType, Payload);
                    }
                }
            });
        }

        public FrameworkPayload? GetPaylaod() {
            return Payload;
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
        public void SetPreviousStepBtnText(string text) {
            _viewModel.PreviousStepBtnText = text;
        }

        public void SetNextStepBtnText(string text) {
            _viewModel.NextStepBtnText = text;
        }

        public void SetNextStepBtnEnable(bool isEnable) {
            _viewModel.IsNextEnable = isEnable;
        }

        public void SetBtnVisible(bool isVisible) {
            _viewModel.BtnVisible = isVisible;
        }

        public void BindingPreviousBtnAction(Action action) {
            _viewModel.PreviousStep = action;
        }

        public void BindingNextBtnAction(Action action) {
            _viewModel.NextStep = action;
        }
        #endregion

        private ConfigSpaceViewModel _viewModel;
        private Draft? _draftPage;
    }
}
