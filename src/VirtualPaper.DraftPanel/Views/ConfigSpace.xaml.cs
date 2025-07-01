using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.DraftPanel.Views.ConfigSpaceComponents;
using VirtualPaper.UIComponent.Data;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConfigSpace : Page, ICardComponent, IDraftPanelBridge {
        public ConfigSpace() {
            this.InitializeComponent();

            _currentPanel = DraftPanelState.GetStart;
        }

        #region nav
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            _viewModel = ObjectProvider.GetRequiredService<ConfigSpaceViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
            this.DataContext = _viewModel;
        }

        private void FrameComp_Loaded(object sender, RoutedEventArgs e) {
            NavigetBasedState(_currentPanel);
        }

        internal void NavigetBasedState(DraftPanelState nextState) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                _currentPanel = nextState;

                Type targetPageType;
                switch (_currentPanel) {
                    case DraftPanelState.GetStart:
                        targetPageType = typeof(GetStart);
                        break;
                    case DraftPanelState.ProjectConfig:
                        targetPageType = typeof(ProjectConfig);
                        break;
                    case DraftPanelState.DraftConfig:
                        targetPageType = typeof(DraftConfig);
                        break;
                    default:
                        Draft.Instance.ChangePanelState(nextState, _sharedData);
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

        public uint GetHardwareDpi() {
            return Draft.Instance.GetHardwareDpi();
        }
    
        public void ChangePanelState(DraftPanelState nextPanel, object data) {
            _sharedData = data;
            NavigetBasedState(nextPanel);
        }

        public object GetSharedData() => _sharedData;

        public nint GetWindowHandle() {
            return Draft.Instance.GetWindowHandle();
        }

        public void Log(LogType type, object message) {
            Draft.Instance.Log(type, message);
        }

        public INoifyBridge GetNotify() {
            return Draft.Instance.GetNotify();
        }

        public IDialogService GetDialog() {
            return Draft.Instance.GetDialog();
        }
        #endregion

        private ConfigSpaceViewModel _viewModel;
        private DraftPanelState _currentPanel;
        private object _sharedData;
    }
}
