using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Components {
    public sealed partial class LayerItem : UserControl {
        public string Project_NewName_InvalidTip { get; set; }

        public string LayerName {
            get { return (string)GetValue(LayerNameProperty); }
            set { SetValue(LayerNameProperty, value); }
        }
        public static readonly DependencyProperty LayerNameProperty =
            DependencyProperty.Register("LayerName", typeof(string), typeof(LayerItem), new PropertyMetadata(string.Empty));

        public bool IsEnable {
            get { return (bool)GetValue(IsEnableProperty); }
            set { SetValue(IsEnableProperty, value); }
        }
        public static readonly DependencyProperty IsEnableProperty =
            DependencyProperty.Register("IsEnable", typeof(bool), typeof(LayerItem), new PropertyMetadata(true));

        public ImageSource LayerThum {
            get { return (ImageSource)GetValue(LayerThumProperty); }
            set { SetValue(LayerThumProperty, value); }
        }
        public static readonly DependencyProperty LayerThumProperty =
            DependencyProperty.Register("LayerThum", typeof(ImageSource), typeof(LayerItem), new PropertyMetadata(null));

        public long ItemTag {
            get { return (long)GetValue(ItemTagProperty); }
            set { SetValue(ItemTagProperty, value); }
        }
        public static readonly DependencyProperty ItemTagProperty =
            DependencyProperty.Register("ItemTag", typeof(long), typeof(LayerItem), new PropertyMetadata(0));

        public LayerItem() {
            this.InitializeComponent();

            InitText();
        }

        private void InitText() {
            Project_NewName_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_NewName_InvalidTip));
        }
    }
}
