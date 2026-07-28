using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Workloads.Creation.WebBackdrop.Views.Components.EditorContent {
    public sealed partial class ImagePreviewView : UserControl {
        private const double MinScale = 0.1;
        private const double MaxScale = 8;
        private const double ScaleStep = 1.1;

        public event EventHandler? PreviewReady;

        public ImagePreviewView() {
            InitializeComponent();
        }

        public void Load(string filePath) {
            previewOverlay.Visibility = Visibility.Visible;
            _zoomScale = 1;
            imagePreview.Source = new BitmapImage(new Uri(filePath));
            UpdateImageSize();
        }

        public void ReleaseResources() {
            previewOverlay.Visibility = Visibility.Visible;
            imagePreview.Source = null;
            _zoomScale = 1;
            imagePreview.Width = double.NaN;
            imagePreview.Height = double.NaN;
        }

        private void ImagePreview_ImageOpened(object sender, RoutedEventArgs e) {
            UpdateImageSize();
            previewOverlay.Visibility = Visibility.Collapsed;
            PreviewReady?.Invoke(this, EventArgs.Empty);
        }

        private void ImagePreview_ImageFailed(object sender, ExceptionRoutedEventArgs e) {
            previewOverlay.Visibility = Visibility.Collapsed;
            PreviewReady?.Invoke(this, EventArgs.Empty);
        }

        private void ImageScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateImageSize();
        }

        private void UpdateImageSize() {
            if (imagePreview.Source is not BitmapImage bitmap || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0) return;

            var availableWidth = Math.Max(0, imageScrollViewer.ActualWidth - imagePreview.Margin.Left - imagePreview.Margin.Right);
            var availableHeight = Math.Max(0, imageScrollViewer.ActualHeight - imagePreview.Margin.Top - imagePreview.Margin.Bottom);
            if (availableWidth <= 0 || availableHeight <= 0) return;

            var fitScale = Math.Min(availableWidth / bitmap.PixelWidth, availableHeight / bitmap.PixelHeight);
            var scale = Math.Max(0, fitScale) * _zoomScale;
            imagePreview.Width = bitmap.PixelWidth * scale;
            imagePreview.Height = bitmap.PixelHeight * scale;
        }

        private void ImagePreviewPanel_PointerWheelChanged(object sender, PointerRoutedEventArgs e) {
            var point = e.GetCurrentPoint(imageScrollViewer);
            var oldZoomScale = _zoomScale;
            var oldExtentWidth = imageScrollViewer.ExtentWidth;
            var oldExtentHeight = imageScrollViewer.ExtentHeight;
            var oldHorizontalOffset = imageScrollViewer.HorizontalOffset;
            var oldVerticalOffset = imageScrollViewer.VerticalOffset;
            var pointerX = point.Position.X;
            var pointerY = point.Position.Y;

            _zoomScale = point.Properties.MouseWheelDelta > 0
                ? _zoomScale * ScaleStep
                : _zoomScale / ScaleStep;
            _zoomScale = Math.Clamp(_zoomScale, MinScale, MaxScale);
            if (Math.Abs(_zoomScale - oldZoomScale) < double.Epsilon) {
                e.Handled = true;
                return;
            }

            UpdateImageSize();
            imagePreview.UpdateLayout();

            var scaleRatio = _zoomScale / oldZoomScale;
            var centerHorizontalOffset = Math.Max(0, (imageScrollViewer.ExtentWidth - imageScrollViewer.ViewportWidth) / 2);
            var centerVerticalOffset = Math.Max(0, (imageScrollViewer.ExtentHeight - imageScrollViewer.ViewportHeight) / 2);
            var horizontalOffset = oldExtentWidth > imageScrollViewer.ViewportWidth
                ? (oldHorizontalOffset + pointerX) * scaleRatio - pointerX
                : centerHorizontalOffset;
            var verticalOffset = oldExtentHeight > imageScrollViewer.ViewportHeight
                ? (oldVerticalOffset + pointerY) * scaleRatio - pointerY
                : centerVerticalOffset;

            imageScrollViewer.ChangeView(
                Math.Clamp(horizontalOffset, 0, Math.Max(0, imageScrollViewer.ExtentWidth - imageScrollViewer.ViewportWidth)),
                Math.Clamp(verticalOffset, 0, Math.Max(0, imageScrollViewer.ExtentHeight - imageScrollViewer.ViewportHeight)),
                null,
                true);
            e.Handled = true;
        }

        private double _zoomScale = 1;
    }
}
