using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Components {
    public sealed partial class LayerItem : UserControl {
        public string Project_NewName_InvalidTip => LanguageUtil.GetI18n(nameof(Constants.I18n.Project_NewName_InvalidTip));

        public string LayerName {
            get { return (string)GetValue(LayerNameProperty); }
            set { SetValue(LayerNameProperty, value); }
        }
        public static readonly DependencyProperty LayerNameProperty =
            DependencyProperty.Register(nameof(LayerName), typeof(string), typeof(LayerItem), new PropertyMetadata(string.Empty));

        public bool IsVisible {
            get { return (bool)GetValue(IsVisibleProperty); }
            set { SetValue(IsVisibleProperty, value); }
        }
        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(nameof(IsVisible), typeof(bool), typeof(LayerItem), new PropertyMetadata(true));

        public ImageSource LayerThum {
            get { return (ImageSource)GetValue(LayerThumProperty); }
            set { SetValue(LayerThumProperty, value); }
        }
        public static readonly DependencyProperty LayerThumProperty =
            DependencyProperty.Register(nameof(LayerThum), typeof(ImageSource), typeof(LayerItem), new PropertyMetadata(null));

        public Guid ItemTag {
            get { return (Guid)GetValue(ItemTagProperty); }
            set { SetValue(ItemTagProperty, value); }
        }
        public static readonly DependencyProperty ItemTagProperty =
            DependencyProperty.Register(nameof(ItemTag), typeof(Guid), typeof(LayerItem), new PropertyMetadata(Guid.Empty));

        public LayerItem() {
            this.InitializeComponent();
        }
    }
}
