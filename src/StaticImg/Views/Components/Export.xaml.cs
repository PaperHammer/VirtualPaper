using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.UIComponent.Data;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg.ViewModels;
using Workloads.Utils.DraftUtils.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Components {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Export : Page, ICardComponent {
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

        public Export() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<ExportViewModel>();
            this.DataContext = _viewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is FrameworkPayload payload) {
                if (payload.TryGet(NaviPayloadKey.INavigateComponent, out _viewModel._navigateComponent)) {
                    _viewModel.DraftConfigTCS = payload.Get<TaskCompletionSource<ExportDataStaticImg?>>(NaviPayloadKey.DraftConfigTCS);
                    await _viewModel.InitContentAsync();
                    _viewModel.UpdateCardComponentUI();
                }
            }
        }

        private readonly ExportViewModel _viewModel;
    }
}
