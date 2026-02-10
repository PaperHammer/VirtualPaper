using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Templates {
    public partial class ArcUserControl : UserControl {
        public FrameworkPayload? Payload {
            get { return (FrameworkPayload?)GetValue(MyPropertyProperty); }
            set { SetValue(MyPropertyProperty, value); }
        }
        public static readonly DependencyProperty MyPropertyProperty =
            DependencyProperty.Register(nameof(Payload), typeof(FrameworkPayload), typeof(ArcUserControl), new PropertyMetadata(null));
    }
}
