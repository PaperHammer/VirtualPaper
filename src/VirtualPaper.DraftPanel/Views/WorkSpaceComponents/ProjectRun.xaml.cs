//using System.IO;
//using Microsoft.UI.Xaml;
//using Microsoft.UI.Xaml.Controls;
//using VirtualPaper.DraftPanel.ViewModels;
//using VirtualPaper.UIComponent.Utils;

//// To learn more about WinUI, the WinUI project structure,
//// and more about our project templates, see: http://aka.ms/winui-project-info.

//namespace VirtualPaper.DraftPanel.Views.WorkSpaceComponents {
//    /// <summary>
//    /// An empty page that can be used on its own or navigated to within a Frame.
//    /// </summary>
//    public sealed partial class ProjectRun : Page {
//        internal static BasicComponentUtil BasicComp { get; private set; } = new();
//        internal static string ProjectFilePath { get; private set; } = string.Empty;
//        internal static string ProjectFolderPath { get; private set; } = string.Empty;

//        public ProjectRun(string projectFilePath) {
//            this.InitializeComponent();

//            _viewModel = new();
//            ProjectFilePath = projectFilePath;
//            ProjectFolderPath = Points.GetDirectoryName(projectFilePath);
//            this.DataContext = this._viewModel;
//        }

//        private void Page_Loaded(object sender, RoutedEventArgs e) {
//            _viewModel.InitProjectAsync();
//        }

//        internal void Save() {
//            _viewModel.Save();
//        }

//        internal void ExitAsync() {
//            _viewModel.ExitAsync();
//        }

//        private readonly ProjectRunViewModel _viewModel;
//    }
//}
