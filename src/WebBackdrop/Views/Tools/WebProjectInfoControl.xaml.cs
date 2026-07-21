using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Models.Cores;
using Workloads.Creation.WebBackdrop.ViewModels;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebProjectInfoControl : UserControl {
        public WebProjectInfoControl() {
            _viewModel = AppServiceLocator.Services.GetRequiredService<WebProjectInfoViewModel>();
            DataContext = _viewModel;
            InitializeComponent();
        }

        public void Load(WpWebProjectData data) {
            _viewModel.Load(data);
        }

        private readonly WebProjectInfoViewModel _viewModel;
    }
}
