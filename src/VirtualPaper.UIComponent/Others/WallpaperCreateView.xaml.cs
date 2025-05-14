using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Others {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WallpaperCreateView : Page {
        public WallpaperCreateView(
            WallpaperCreateViewModel wpCreateViewModel) {
            this.InitializeComponent();

            this.DataContext = wpCreateViewModel;
        }
    }
}
