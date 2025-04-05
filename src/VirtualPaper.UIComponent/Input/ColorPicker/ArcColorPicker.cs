using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Input {
    public partial class ArcColorPicker : ColorPicker {
        public bool AddBtnIsVisible {
            get { return (bool)GetValue(AddBtnIsVisibleProperty); }
            set { SetValue(AddBtnIsVisibleProperty, value); }
        }
        public static readonly DependencyProperty AddBtnIsVisibleProperty =
            DependencyProperty.Register("AddBtnIsVisible", typeof(bool), typeof(ArcColorPicker), new PropertyMetadata(false));

        public ICommand AddToCustomCommand {
            get { return (ICommand)GetValue(AddToCustomCommandProperty); }
            set { SetValue(AddToCustomCommandProperty, value); }
        }
        public static readonly DependencyProperty AddToCustomCommandProperty =
            DependencyProperty.Register("AddToCustomCommand", typeof(ICommand), typeof(ArcColorPicker), new PropertyMetadata(null));

        public string Text_AddToCustom {
            get { return (string)GetValue(Text_AddToCustomProperty); }
            set { SetValue(Text_AddToCustomProperty, value); }
        }
        public static readonly DependencyProperty Text_AddToCustomProperty =
            DependencyProperty.Register("Text_AddToCustom", typeof(string), typeof(ArcColorPicker), new PropertyMetadata(string.Empty));

        public ArcColorPicker() {
            Text_AddToCustom = LanguageUtil.GetI18n(nameof(Constants.I18n.ArcColorPicker_AddToCustom));
        }
    }
}
