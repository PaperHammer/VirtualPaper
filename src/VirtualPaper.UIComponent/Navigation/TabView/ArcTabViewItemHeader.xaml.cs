using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Navigation.TabView {
    public sealed partial class ArcTabViewItemHeader : UserControl, IArcTabViewItemHeader {
        public object MainContent {
            get { return (object)GetValue(MainContentProperty); }
            set { SetValue(MainContentProperty, value); }
        }
        public static readonly DependencyProperty MainContentProperty =
            DependencyProperty.Register(nameof(MainContent), typeof(object), typeof(ArcTabViewItemHeader), new PropertyMetadata(null));

        public bool IsSaved {
            get { return (bool)GetValue(IsSavedProperty); }
            set { SetValue(IsSavedProperty, value); }
        }
        public static readonly DependencyProperty IsSavedProperty =
        DependencyProperty.Register(
            nameof(IsSaved),
            typeof(bool),
            typeof(ArcTabViewItemHeader),
            new PropertyMetadata(false, OnIsUnsavedChanged));

        private static void OnIsUnsavedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ArcTabViewItemHeader control) {
                control.IsSaved = (bool)e.NewValue;
            }
        }

        public ArcTabViewItemHeader() {
            this.InitializeComponent();
        }
    }
}
