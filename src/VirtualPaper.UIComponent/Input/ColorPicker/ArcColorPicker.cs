using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VirtualPaper.UIComponent.Input {
    public partial class ArcColorPicker : ColorPicker {
        public bool AddBtnIsVisible {
            get { return (bool)GetValue(AddBtnIsVisibleProperty); }
            set { SetValue(AddBtnIsVisibleProperty, value); }
        }
        public static readonly DependencyProperty AddBtnIsVisibleProperty =
            DependencyProperty.Register(nameof(AddBtnIsVisible), typeof(bool), typeof(ArcColorPicker), new PropertyMetadata(false));

        public ICommand AddToCustomCommand {
            get { return (ICommand)GetValue(AddToCustomCommandProperty); }
            set { SetValue(AddToCustomCommandProperty, value); }
        }
        public static readonly DependencyProperty AddToCustomCommandProperty =
            DependencyProperty.Register(nameof(AddToCustomCommand), typeof(ICommand), typeof(ArcColorPicker), new PropertyMetadata(null));
    }
}
