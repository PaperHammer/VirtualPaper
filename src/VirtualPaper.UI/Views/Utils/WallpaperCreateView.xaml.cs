using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UI.ViewModels.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views.Utils {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WallpaperCreateView : Page {
        public WallpaperCreateView(
            WallpaperCreateDialogViewModel wpCreateDialogViewModel) {
            this.InitializeComponent();

            this.DataContext = wpCreateDialogViewModel;
        }
    }
}
