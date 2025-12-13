using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.PlayerWeb.Core.Utils.Interfaces;
using VirtualPaper.PlayerWeb.Core.ViewModels;

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
            _startArgs ??= (e.Parameter as IMainPage)?.StartArgs;

            InitViewModel();
        }

        private void InitViewModel() {
            _viewModel ??= new GeneralInfoViewModel(_startArgs.WpBasicDataFilePath);
        }

        private StartArgsWeb _startArgs = null!;
        private GeneralInfoViewModel _viewModel = null!;
    }
}
