using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Launcher.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.Launcher.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page {
        public HomePage() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<HomePageViewModel>();
            this.DataContext = _viewModel;
        }

        private readonly HomePageViewModel _viewModel;
    }
}
