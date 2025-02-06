using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Data {
    public sealed partial class Card : UserControl {
        public new static readonly DependencyProperty ContentProperty =
           DependencyProperty.Register(nameof(Content), typeof(object), typeof(Card), new PropertyMetadata(null));

        public new object Content {
            get { return (object)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public Card() {
            this.InitializeComponent();
        }
    }
}
