using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.DraftPanel.Views.ConfigSpaceComponents;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Data;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConfigSpace : ArcPage, ICardComponent {
        public override ArcPageContext Context { get; set; }
        public override Type PageType => typeof(ConfigSpace);

        public ConfigSpace() {
            this.Unloaded += ConfigSpace_Unloaded;
            this.InitializeComponent();
            _viewModel = ObjectProvider.GetRequiredService<ConfigSpaceViewModel>();
            this.DataContext = _viewModel;
            Context = new ArcPageContext(this);
        }

        private void ConfigSpace_Unloaded(object sender, RoutedEventArgs e) {
            this.Unloaded -= ConfigSpace_Unloaded;
            _viewModel.Dispose();
            _viewModel = null;
        }

        #region nav
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is NavigationPayload payload) {
                payload.TryGet(NaviPayloadKey.DraftPage.ToString(), out _draftPage);
                Payload = Payload?.Merge(payload);
            }
        }

        private void FrameComp_Loaded(object sender, RoutedEventArgs e) {
            (Payload ??= new NavigationPayload())[NaviPayloadKey.ConfigSpacePage.ToString()] = this;
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

        public void BindingPreviousBtnAction(RoutedEventHandler action) {
            _viewModel.PreviousStep = action;
        }

        public void BindingNextBtnAction(RoutedEventHandler action) {
            _viewModel.NextStep = action;
        }
        #endregion

        private ConfigSpaceViewModel _viewModel;
        private Draft? _draftPage;
    }
}
