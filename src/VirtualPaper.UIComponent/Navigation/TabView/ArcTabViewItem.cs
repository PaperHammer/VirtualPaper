using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VirtualPaper.UIComponent.Navigation {
    public partial class ArcTabViewItem : TabViewItem {
        public bool IsUnsaved {
            get { return (bool)GetValue(IsUnsaveProperty); }
            set { SetValue(IsUnsaveProperty, value); }
        }
        public static readonly DependencyProperty IsUnsaveProperty =
            DependencyProperty.Register("IsUnsaved", typeof(bool), typeof(ArcTabViewItem), new PropertyMetadata(true));
    }
}
