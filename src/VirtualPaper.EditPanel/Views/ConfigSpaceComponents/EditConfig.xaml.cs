using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.EditPanel.Model;
using VirtualPaper.EditPanel.ViewModels;
using VirtualPaper.UIComponent.Data;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI draft structure,
// and more about our draft templates, see: http://aka.ms/winui-draft-info.

namespace VirtualPaper.EditPanel.Views.ConfigSpaceComponents {
    public sealed partial class EditConfig : Page, ICardComponent {
        public Action? CardUIStateChanged {
            get => _viewModel.CardUIStateChanged;
            set => _viewModel.CardUIStateChanged = value;
        }
        public string PreviousStepBtnText => _viewModel.PreviousStepBtnText;
        public string NextStepBtnText => _viewModel.NextStepBtnText;
        public bool BtnVisible => _viewModel.BtnVisible;
        public bool IsNextEnable => _viewModel.IsNextEnable;
        public Func<object?, Task>? PreviousStepAction => async (_) => await _viewModel.OnPreviousStepClickedAsync();
        public Func<object?, Task>? NextStepAction => async (_) => await _viewModel.OnNextStepClickedAsync();

        public EditConfig() {
            this.InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<EditConfigViewModel>();
            this.DataContext = _viewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is FrameworkPayload payload) {
                if (payload.TryGet(NaviPayloadKey.INavigateComponent, out _viewModel._navigateComponent)) {
                    _viewModel.IsFromWorkSpaceForAddProj = payload.Get<bool>(NaviPayloadKey.IsFromWorkSpaceForAddProj);
                    _viewModel.EditConfigTCS = payload.Get<TaskCompletionSource<PreProjectData[]?>>(NaviPayloadKey.EditConfigTCS);
                    await _viewModel.InitContentAsync();
                }
            }
        }

        private void OnFilterChanged(object sender, TextChangedEventArgs e) {
            _viewModel.ApplyFilter(tbSearchName.Text);
        }

        public void UpdateCardComponentUI() {
            _viewModel.UpdateCardComponentUI();
        }

        private readonly EditConfigViewModel _viewModel;
    }
}
