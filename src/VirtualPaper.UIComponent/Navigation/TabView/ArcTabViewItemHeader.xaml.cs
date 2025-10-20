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
            get { return (bool)GetValue(IsUnsavedProperty); }
            set { SetValue(IsUnsavedProperty, value); }
        }
        public static readonly DependencyProperty IsUnsavedProperty =
        DependencyProperty.Register(
            nameof(IsUnsaved),
            typeof(bool),
            typeof(ArcTabViewItemHeader),
            new PropertyMetadata(false, OnIsUnsavedChanged));

        private static void OnIsUnsavedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ArcTabViewItemHeader control) {
                control.IsUnsaved = (bool)e.NewValue;
            }
        }

        public ArcTabViewItemHeader() {
            this.InitializeComponent();
        }
    }
}
