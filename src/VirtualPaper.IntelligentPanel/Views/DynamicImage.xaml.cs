using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.Utils.Interfaces;
using VirtualPaper.IntelligentPanel.ViewModels;

namespace VirtualPaper.IntelligentPanel.Views {
    public sealed partial class DynamicImage : Page, IIntelligentPage {
        public DynamicImage() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<DynamicImageViewModel>();
            DataContext = _viewModel;
        }

        public bool AddTask(IIntelliData data) =>
            data is DynamicImageData input && _viewModel.AddTask(input);

        private readonly DynamicImageViewModel _viewModel;
    }
}
