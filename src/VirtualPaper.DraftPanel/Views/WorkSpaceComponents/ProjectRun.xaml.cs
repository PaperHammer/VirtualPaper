using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.DraftPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views.WorkSpaceComponents {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ProjectRun : Page {
        public ProjectRun(string filePath) {
            this.InitializeComponent();

            this._viewModel = new(filePath);
            this.DataContext = this._viewModel;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            await _viewModel.InitProjectAsync();
        }

        internal void Save() {
            _viewModel.Save();
        }

        internal void Exit() {
            _viewModel.Exit();
        }

        private readonly ProjectRunViewModel _viewModel;
    }
}
