using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.Utils.Interfaces;
using VirtualPaper.IntelligentPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SuperResolution : Page, IIntelligentPage {
        public SuperResolution() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<SuperResolutionViewModel>();
            this.DataContext = _viewModel;
        }

        public bool AddTask(IIntelliData data) {
            if (data is SuperResolutionData input) {
                return _viewModel.AddTask(input);
            }

            return false;
        }

        private readonly SuperResolutionViewModel _viewModel;
    }
}
