using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Container {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PageContainer : Page {
        public PageContainer() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is object content) {
                if (FrameComp != null) {
                    FrameComp.Content = content;
                }
            }
        }
    }
}
