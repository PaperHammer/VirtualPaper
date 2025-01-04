using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Data {
    public sealed partial class Details : UserControl {
        public Details() {
            this.InitializeComponent();
        }

        public Details(string wpBasicDataFilePath) : this() {
            _viewModel = new DetailsViewModel(wpBasicDataFilePath);
            this.DataContext = _viewModel;
        }

        public Details(IWpBasicData wpBasicData) : this() {
            _viewModel = new DetailsViewModel(wpBasicData);
            this.DataContext = _viewModel;
        }

        private readonly DetailsViewModel _viewModel;
    }
}
