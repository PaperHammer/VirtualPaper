using Microsoft.UI.Xaml;
using VirtualPaper.AccountPanel.Views.UserCenterComponents;
using VirtualPaper.Models.Cores.Interfaces;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AccountPanel.Views.Utils {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WallpaperEdit : Window {
        public WallpaperEdit(IWpBasicData data) {
            this.InitializeComponent();
            this._data = data;
        }

        private void FrameComp_Loaded(object sender, RoutedEventArgs e) {
            FrameComp.Content = new Upload(_data);
        }
        
        private readonly IWpBasicData _data;
    }
}
