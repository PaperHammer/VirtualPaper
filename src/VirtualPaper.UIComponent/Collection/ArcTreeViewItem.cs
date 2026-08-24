using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Collection {
    public sealed partial class ArcTreeViewItem : TreeViewItem {
        public Visibility ExpandCollapseGlyphVisibility {
            get => (Visibility)GetValue(ExpandCollapseGlyphVisibilityProperty);
            set => SetValue(ExpandCollapseGlyphVisibilityProperty, value);
        }

        public static readonly DependencyProperty ExpandCollapseGlyphVisibilityProperty =
            DependencyProperty.Register(
                nameof(ExpandCollapseGlyphVisibility),
                typeof(Visibility),
                typeof(ArcTreeViewItem),
                new PropertyMetadata(Visibility.Visible));

        public ArcTreeViewItem() {
            DefaultStyleKey = typeof(TreeViewItem);
        }
    }
}
