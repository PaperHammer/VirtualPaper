using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VirtualPaper.UIComponent.Dialogs {
    /// <summary>
    /// Shared dialog chrome. Only the body scrolls; title and native command buttons stay visible.
    /// Dialog dimensions include the title, content padding and footer, and fit within the XamlRoot.
    /// </summary>
    public partial class ArcContentDialog : ContentDialog {
        public double DialogWidth { get => (double)GetValue(DialogWidthProperty); set => SetValue(DialogWidthProperty, value); }
        public static readonly DependencyProperty DialogWidthProperty = DependencyProperty.Register(
            nameof(DialogWidth), typeof(double), typeof(ArcContentDialog), new PropertyMetadata(double.NaN, OnSizeChanged));

        public double DialogHeight { get => (double)GetValue(DialogHeightProperty); set => SetValue(DialogHeightProperty, value); }
        public static readonly DependencyProperty DialogHeightProperty = DependencyProperty.Register(
            nameof(DialogHeight), typeof(double), typeof(ArcContentDialog), new PropertyMetadata(double.NaN, OnSizeChanged));

        public double DialogMaxWidth { get => (double)GetValue(DialogMaxWidthProperty); set => SetValue(DialogMaxWidthProperty, value); }
        public static readonly DependencyProperty DialogMaxWidthProperty = DependencyProperty.Register(
            nameof(DialogMaxWidth), typeof(double), typeof(ArcContentDialog), new PropertyMetadata(800d, OnSizeChanged));

        public double DialogMaxHeight { get => (double)GetValue(DialogMaxHeightProperty); set => SetValue(DialogMaxHeightProperty, value); }
        public static readonly DependencyProperty DialogMaxHeightProperty = DependencyProperty.Register(
            nameof(DialogMaxHeight), typeof(double), typeof(ArcContentDialog), new PropertyMetadata(756d, OnSizeChanged));

        private Border? _background;
        private XamlRoot? _viewport;

        public ArcContentDialog() {
            Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("ms-appx:///VirtualPaper.UIComponent/Dialogs/ArcContentDialog.xaml")
            });
            Style = (Style)Resources["ArcContentDialogStyle"];
            Opened += Dialog_Opened;
            Closed += Dialog_Closed;
        }

        protected override void OnApplyTemplate() {
            base.OnApplyTemplate();
            _background = GetTemplateChild("BackgroundElement") as Border;
            UpdateBounds();
        }

        private static void OnSizeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
            ((ArcContentDialog)sender).UpdateBounds();
        }

        private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) {
            _viewport = XamlRoot;
            if (_viewport != null) _viewport.Changed += Viewport_Changed;
            UpdateBounds();
        }

        private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args) {
            if (_viewport != null) _viewport.Changed -= Viewport_Changed;
            _viewport = null;
        }

        private void Viewport_Changed(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateBounds();

        private void UpdateBounds() {
            if (_background == null) return;
            var viewport = XamlRoot?.Size;
            double maxWidth = Math.Min(DialogMaxWidth, Math.Max(1, (viewport?.Width ?? double.PositiveInfinity) - 48));
            double maxHeight = Math.Min(DialogMaxHeight, Math.Max(1, (viewport?.Height ?? double.PositiveInfinity) - 48));
            _background.MinWidth = Math.Min(320, maxWidth);
            _background.MinHeight = 0;
            _background.MaxWidth = maxWidth;
            _background.MaxHeight = maxHeight;
            _background.Width = double.IsNaN(DialogWidth) ? double.NaN : Math.Min(DialogWidth, maxWidth);
            _background.Height = double.IsNaN(DialogHeight) ? double.NaN : Math.Min(DialogHeight, maxHeight);
        }
    }
}
