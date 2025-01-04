using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UI.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Gallery : Page {
        GalleryViewModel _viewModel;

        public Gallery() {
            this.InitializeComponent();
            _viewModel = this.DataContext as GalleryViewModel;
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e) {

        }
    }
}
