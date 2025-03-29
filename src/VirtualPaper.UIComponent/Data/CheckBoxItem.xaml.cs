using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Data {
    public sealed partial class CheckBoxItem : UserControl {        
        public string LayerName {
            get { return (string)GetValue(LayerNameProperty); }
            set { SetValue(LayerNameProperty, value); }
        }
        public static readonly DependencyProperty LayerNameProperty =
            DependencyProperty.Register("LayerName", typeof(string), typeof(CheckBoxItem), new PropertyMetadata(string.Empty));

        public bool IsEnable {
            get { return (bool)GetValue(IsEnableProperty); }
            set { SetValue(IsEnableProperty, value); }
        }
        public static readonly DependencyProperty IsEnableProperty =
            DependencyProperty.Register("IsEnable", typeof(bool), typeof(CheckBoxItem), new PropertyMetadata(true));
        
        public UIElement LayerThum {
            get { return (UIElement)GetValue(LayerThumProperty); }
            set { SetValue(LayerThumProperty, value); }
        }
        public static readonly DependencyProperty LayerThumProperty =
            DependencyProperty.Register("LayerThum", typeof(UIElement), typeof(CheckBoxItem), new PropertyMetadata(null));

        //private static void OnLayerThumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        //    var instance = d as CheckBoxItem;
        //    if (instance != null) {
        //        instance.layerThum.Content = new Grid() {
        //            Children = (e.NewValue as Grid).Children
        //        };
        //    }
        //}

        public CheckBoxItem() {
            this.InitializeComponent();
        }
    }
}
