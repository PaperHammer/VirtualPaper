using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI draft structure,
// and more about our draft templates, see: http://aka.ms/winui-draft-info.

namespace VirtualPaper.DraftPanel.Views.ConfigSpaceComponents {
    public sealed partial class DraftConfig : Page {
        public DraftConfig() {
            this.InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<DraftConfigViewModel>();
            this.DataContext = _viewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is FrameworkPayload payload) {
                if (payload.TryGet(NaviPayloadKey.ICardComponent, out _viewModel._cardComponent) &&
                    payload.TryGet(NaviPayloadKey.INavigateComponent, out _viewModel._navigateComponent)) {
                    await _viewModel.InitContentAsync();
                    _viewModel.InitConfigSpace();
                }
            }
        }

        private void OnFilterChanged(object sender, TextChangedEventArgs e) {
            _viewModel.ApplyFilter(tbSearchName.Text);
        }

        private readonly DraftConfigViewModel _viewModel;
    }
}
