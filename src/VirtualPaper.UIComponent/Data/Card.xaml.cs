using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Utils.Bridge;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Data {
    public sealed partial class Card : UserControl {
        public new object Content {
            get { return (object)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }
        public new static readonly DependencyProperty ContentProperty =
           DependencyProperty.Register(nameof(Content), typeof(object), typeof(Card), new PropertyMetadata(null));

        public Card() {
            this.InitializeComponent();
        }
    }

    public interface ICardComponent {
        void SetPreviousStepBtnText(string text);
        void SetNextStepBtnText(string text);
        void SetNextStepBtnEnable(bool isEnable);
        void SetBtnVisible(bool isVisible);
        void BindingPreviousBtnAction(RoutedEventHandler action);
        void BindingNextBtnAction(RoutedEventHandler action);
    }
}
