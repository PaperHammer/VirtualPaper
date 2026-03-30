using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.UIComponent.Data;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI draft structure,
// and more about our draft templates, see: http://aka.ms/winui-draft-info.

namespace VirtualPaper.DraftPanel.Views.ConfigSpaceComponents {
    public sealed partial class DraftConfig : Page, ICardComponent {
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

        public DraftConfig() {
            this.InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<DraftConfigViewModel>();
            this.DataContext = _viewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is FrameworkPayload payload) {
                if (payload.TryGet(NaviPayloadKey.INavigateComponent, out _viewModel._navigateComponent)) {
                    _viewModel.IsFromWorkSpace = payload.Get<bool>(NaviPayloadKey.IsFromWorkSpace);
                    _viewModel.DraftConfigTCS = payload.Get<TaskCompletionSource<PreProjectData[]?>>(NaviPayloadKey.DraftConfigTCS);
                    await _viewModel.InitContentAsync(); 
                    _viewModel.UpdateCardComponentUI();
                }
            }
        }

        private void OnFilterChanged(object sender, TextChangedEventArgs e) {
            _viewModel.ApplyFilter(tbSearchName.Text);
        }

        private readonly DraftConfigViewModel _viewModel;
    }
}
