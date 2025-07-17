using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Navigation.TabView {
    public sealed partial class ArcTabViewItemHeader : UserControl {
        public object MainContent {
            get { return (object)GetValue(MainContentProperty); }
            set { SetValue(MainContentProperty, value); }
        }
        public static readonly DependencyProperty MainContentProperty =
            DependencyProperty.Register(nameof(MainContent), typeof(object), typeof(ArcTabViewItemHeader), new PropertyMetadata(null));

        public bool IsUnsaved {
            get { return (bool)GetValue(IsUnsaveProperty); }
            set { SetValue(IsUnsaveProperty, value); }
        }
        public static readonly DependencyProperty IsUnsaveProperty =
            DependencyProperty.Register(nameof(IsUnsaved), typeof(bool), typeof(ArcTabViewItem), new PropertyMetadata(true));

        public ArcTabViewItemHeader() {
            this.InitializeComponent();
        }
    }
}
