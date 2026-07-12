using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Styles {
    public partial class ArcImage : Control {
        public string ResourceKey {
            get { return (string)GetValue(ResourceKeyProperty); }
            set { SetValue(ResourceKeyProperty, value); }
        }
        public static readonly DependencyProperty ResourceKeyProperty =
            DependencyProperty.Register(nameof(ResourceKey), typeof(string), typeof(ArcImage), new PropertyMetadata(null, OnThemeResourceKeyChanged));

        public ImageSource? Source {
            get { return (ImageSource?)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(ArcImage), new PropertyMetadata(null));

        public Stretch Stretch {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }
        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register(nameof(Stretch), typeof(Stretch), typeof(ArcImage), new PropertyMetadata(Stretch.Uniform));

        public ArcImage() {
            DefaultStyleKey = typeof(ArcImage);
            Loaded += ArcImage_Loaded;
            Unloaded += ArcImage_Unloaded;
        }

        private void ArcImage_Loaded(object sender, RoutedEventArgs e) {
            UpdateSource();
        }

        private void ArcImage_Unloaded(object sender, RoutedEventArgs e) {
            Loaded -= ArcImage_Loaded;
            Unloaded -= ArcImage_Unloaded;
        }

        private static void OnThemeResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ArcImage image) {
                image.UpdateSource();
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
