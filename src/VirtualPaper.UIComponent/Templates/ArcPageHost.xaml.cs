using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using VirtualPaper.UIComponent.Feedback;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Templates {
    [ContentProperty(Name = "RootContent")]
    public sealed partial class ArcPageHost : UserControl {
        public object RootContent {
            get => GetValue(RootContentProperty);
            set => SetValue(RootContentProperty, value);
        }
        public static readonly DependencyProperty RootContentProperty =
            DependencyProperty.Register(nameof(RootContent), typeof(object), typeof(ArcPageHost), new PropertyMetadata(null));
        
        public Grid ContentGrid => this.PART_ContentGrid;
        public ContentPresenter ContentPresenterHost => this.PART_ContentPresenterHost;
        public Loading LoadingControlHost => this.PART_LoadingControlHost;

        public ArcPageHost() {
            this.InitializeComponent();
        }
    }
}
