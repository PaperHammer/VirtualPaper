using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.Common.Logging;
using VirtualPaper.UIComponent.Dialogs;
using VirtualPaper.WpSettingsPanel.ViewModels;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace VirtualPaper.WpSettingsPanel.Views {
    public sealed partial class ScreenFitDialog : ArcContentDialog {
        public ScreenFitViewModel ViewModel { get; }
        public bool Applied { get; private set; }
        private CancellationTokenSource? _operation;
        private CancellationTokenSource? _previewLoad;
        private string? _previewPath;
        private bool _isOpen;
        private bool _closeAfterCancel;
        public ScreenFitDialog(ScreenFitViewModel viewModel) {
            ViewModel = viewModel;
            InitializeComponent();
        }

        private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) {
            _isOpen = true;
            ViewModel.PropertyChanged += Preview_PropertyChanged;
            QueuePreview();
        }

        private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args) {
            _isOpen = false;
            ViewModel.PropertyChanged -= Preview_PropertyChanged;
            _previewLoad?.Cancel();
            _previewLoad = null;
            _previewPath = null;
            PreviewImage.Source = null;
            DefaultPreviewPattern.Visibility = Visibility.Visible;
        }

        private void Preview_PropertyChanged(object? sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(ScreenFitViewModel.PreviewPath)) QueuePreview();
        }

        private void QueuePreview() {
            if (!_isOpen) return;
            string path = ViewModel.PreviewPath;
            if (path == _previewPath && (_previewLoad != null || PreviewImage.Source != null)) return;
            _previewPath = path;
            _previewLoad?.Cancel();
            var cts = new CancellationTokenSource();
            _previewLoad = cts;
            PreviewImage.Source = null;
            DefaultPreviewPattern.Visibility = Visibility.Visible;
            // Let the dialog render before starting file access and image decoding.
            if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, async () => await LoadPreviewAsync(path, cts))) {
                _previewLoad = null;
                cts.Dispose();
            }
        }

        private async Task LoadPreviewAsync(string path, CancellationTokenSource cts) {
            var token = cts.Token;
            try {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(path)) return;

                var file = await StorageFile.GetFileFromPathAsync(path).AsTask(token);
                using var stream = await file.OpenReadAsync().AsTask(token);
                var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(token);
                int decodeSize = Math.Clamp((int)Math.Ceiling(460 * (XamlRoot?.RasterizationScale ?? 1)), 512, 2048);
                var bitmap = new BitmapImage();
                if (decoder.OrientedPixelWidth >= decoder.OrientedPixelHeight) {
                    bitmap.DecodePixelWidth = (int)Math.Min(decoder.OrientedPixelWidth, (uint)decodeSize);
                }
                else {
                    bitmap.DecodePixelHeight = (int)Math.Min(decoder.OrientedPixelHeight, (uint)decodeSize);
                }
                stream.Seek(0);
                await bitmap.SetSourceAsync(stream).AsTask(token);
                if (token.IsCancellationRequested || !_isOpen) return;
                PreviewImage.Source = bitmap;
                DefaultPreviewPattern.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) {
                if (!token.IsCancellationRequested) ArcLog.GetLogger<ScreenFitDialog>().Error(ex);
            }
            finally {
                if (ReferenceEquals(_previewLoad, cts)) _previewLoad = null;
                cts.Dispose();
            }
        }
        private async void SelectImage_Click(object sender, RoutedEventArgs e) {
            await ViewModel.SelectImageAsync();
            if (_closeAfterCancel) Hide();
        }
        private async void Apply_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args) {
            args.Cancel = true;
            if (ViewModel.IsBusy) return;
            using var cts = new CancellationTokenSource();
            _operation = cts;
            try {
                Applied = await ViewModel.ApplyAsync(cts.Token);
                if (Applied || _closeAfterCancel) Hide();
            }
            finally { _operation = null; }
        }
        private void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args) {
            if (!ViewModel.IsBusy) return;
            args.Cancel = true;
            _closeAfterCancel = true;
            _operation?.Cancel();
        }
    }
}
