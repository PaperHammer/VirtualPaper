using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Styles {
    public partial class ArcImageIcon : ImageIcon {
        public string ResourceKey {
            get { return (string)GetValue(ResourceKeyProperty); }
            set { SetValue(ResourceKeyProperty, value); }
        }
        public static readonly DependencyProperty ResourceKeyProperty =
            DependencyProperty.Register(nameof(ResourceKey), typeof(string), typeof(ArcImageIcon), new PropertyMetadata(null, OnThemeResourceKeyChanged));

        public ArcImageIcon() {
            Loaded += ArcImageIcon_Loaded;
            Unloaded += ArcImageIcon_Unloaded;
        }

        private void ArcImageIcon_Loaded(object sender, RoutedEventArgs e) {
            UpdateSource();
        }

        private void ArcImageIcon_Unloaded(object sender, RoutedEventArgs e) {
            Loaded -= ArcImageIcon_Loaded;
            Unloaded -= ArcImageIcon_Unloaded;
        }

        private static void OnThemeResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ArcImageIcon icon) {
                icon.UpdateSource();
            }
        }

        private void UpdateSource() {
            if (string.IsNullOrEmpty(ResourceKey)) {
                this.Source = null;
                this.Visibility = Visibility.Collapsed;
                return;
            }

            this.Visibility = Visibility.Visible;
            if (ArcThemeUtil.TryGetThemeResource(ResourceKey, this, out var resource) && resource is BitmapImage image) {
                Source = image;
            }
            else {
                this.Source = null;
            }
        }
    }
}
