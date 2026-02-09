using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Models.Cores;
using VirtualPaper.PlayerWeb.Core.ViewModels;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Components.General {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GeneralInfo : Page {
        public GeneralInfo() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            if (e.Parameter is NavigationPayload payload) {
                if (payload.TryGet(NaviPayloadKey.IWpBasicData.ToString(), out _wpBasicData)) {
                    InitViewModel();
                }
            }
        }

        private void InitViewModel() {
            _viewModel ??= new GeneralInfoViewModel(_wpBasicData);
        }

        private WpBasicData? _wpBasicData;
        private GeneralInfoViewModel _viewModel = null!;
    }
}
