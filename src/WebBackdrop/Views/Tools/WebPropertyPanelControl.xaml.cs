using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Utils.DI;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;
using Workloads.Creation.WebBackdrop.ViewModels;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebPropertyPanelControl : UserControl {
        public WebPropertyPanelControl() {
            _viewModel = AppServiceLocator.Services.GetRequiredService<WebPropertyPanelViewModel>();
            DataContext = _viewModel;
            InitializeComponent();
        }

        public void LoadProject(WebDesignFileUtil designFileUtil) {
            _viewModel.LoadProject(designFileUtil);
        }

        public void Load(WebEditorFile? file, string language) {
            _viewModel.Load(file, language);
        }

        public void LoadFolder(string folderPath) {
            _viewModel.LoadFolder(folderPath);
        }

        public void Clear() {
            _viewModel.Clear();
        }

        private readonly WebPropertyPanelViewModel _viewModel;
    }
}
