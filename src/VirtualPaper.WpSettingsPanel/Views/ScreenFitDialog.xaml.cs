using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.UIComponent.Dialogs;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.Utils.Interfaces;
using VirtualPaper.WpSettingsPanel.ViewModels;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace VirtualPaper.WpSettingsPanel.Views {
    public sealed partial class ScreenFitDialog : ArcContentDialog {
        public ScreenFitViewModel ViewModel { get; }
        public bool Applied { get; private set; }
        internal bool AiNoticeRequested { get; private set; }
        internal bool ResumeAfterNotice { get; set; }
        private readonly IAiUseNoticeService _aiNotice;
        private readonly IWallpaperIndexService _index;
        private readonly DispatcherQueueTimer _sourceSearchTimer;
        private WallpaperSourceQuery? _sourceQuery;
        private CancellationTokenSource? _sourcePickerLoad;
        private CancellationTokenSource? _operation;
        private CancellationTokenSource? _previewLoad;
        private string? _previewPath;
        private bool _isOpen;
        private bool _closeAfterCancel;
        public ScreenFitDialog(ScreenFitViewModel viewModel, IAiUseNoticeService aiNotice, IWallpaperIndexService index) {
            ViewModel = viewModel;
            _aiNotice = aiNotice;
            _index = index;
            InitializeComponent();
            _sourceSearchTimer = DispatcherQueue.CreateTimer();
            _sourceSearchTimer.Interval = TimeSpan.FromMilliseconds(150);
            _sourceSearchTimer.IsRepeating = false;
        }

        private async void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) {
            _isOpen = true;
            AiNoticeRequested = false;
            _sourceSearchTimer.Tick += SourceSearch_Tick;
            ViewModel.PropertyChanged += Preview_PropertyChanged;
            QueuePreview();
            if (ResumeAfterNotice) {
                ResumeAfterNotice = false;
                await ApplyAsync();
            }
        }

        private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args) {
            _isOpen = false;
            ViewModel.PropertyChanged -= Preview_PropertyChanged;
            _sourceSearchTimer.Tick -= SourceSearch_Tick;
            _previewLoad?.Cancel();
            _previewLoad = null;
            _previewPath = null;
            PreviewImage.Source = null;
            DefaultPreviewPattern.Visibility = Visibility.Visible;
            SourceFlyout.Hide();
            ClearSources();
            UpdateLoadingState();
        }

        private void Preview_PropertyChanged(object? sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(ScreenFitViewModel.PreviewPath)) QueuePreview();
            if (args.PropertyName == nameof(ScreenFitViewModel.IsBusy)) UpdateLoadingState();
        }

        private void UpdateLoadingState() {
            bool loading = _isOpen && (ViewModel.IsBusy || _previewLoad != null || _sourcePickerLoad != null);
            SourceLoading.IsActive = loading;
            SourceLoading.Opacity = loading ? 1 : 0;
            AutomationProperties.SetAccessibilityView(SourceLoading, loading ? AccessibilityView.Content : AccessibilityView.Raw);
        }

        private async void SourceFlyout_Opened(object sender, object args) {
            if (!_isOpen) return;
            ClearSources();
            using var cts = new CancellationTokenSource();
            _sourcePickerLoad = cts;
            var token = cts.Token;
            SourceEmpty.Visibility = Visibility.Collapsed;
            SourceSearch.Focus(FocusState.Programmatic);
            UpdateLoadingState();
            try {
                // Read the index once per open; filtering never reloads metadata or full-size images.
                await _index.Initialized.Task.WaitAsync(token);
                var query = await Task.Run(() => new WallpaperSourceQuery(_index.Query(0, int.MaxValue),
                    uid => _index.GetOrganization(uid).Favorite, FileType.FImage), token);
                if (!_isOpen || !ReferenceEquals(_sourcePickerLoad, cts)) return;
                _sourceQuery = query;
                RefreshSources();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) {
                if (_isOpen && !token.IsCancellationRequested) {
                    ArcLog.GetLogger<ScreenFitDialog>().Error(ex);
                    GlobalMessageUtil.ShowException(ex);
                }
            }
            finally {
                if (ReferenceEquals(_sourcePickerLoad, cts)) _sourcePickerLoad = null;
                UpdateLoadingState();
            }
        }

        private void SourceFlyout_Closed(object sender, object args) => ClearSources();

        private void ClearSources() {
            _sourcePickerLoad?.Cancel();
            _sourcePickerLoad = null;
            _sourceSearchTimer.Stop();
            _sourceQuery = null;
            SourcePicker.ItemsSource = null;
            UpdateLoadingState();
        }

        private void SourceSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) {
            if (!_isOpen || args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            _sourceSearchTimer.Stop();
            _sourceSearchTimer.Start();
        }

        private void SourceSearch_Tick(DispatcherQueueTimer sender, object args) => RefreshSources();
        private void SourceFavorites_Changed(object sender, RoutedEventArgs args) => RefreshSources();

        private void RefreshSources() {
            if (!_isOpen || _sourceQuery == null) return;
            var items = _sourceQuery.Search(SourceSearch.Text, SourceFavorites.IsChecked == true);
            SourcePicker.ItemsSource = items;
            SourcePicker.SelectedItem = items.FirstOrDefault(item => item.Uid.Equals(ViewModel.LibrarySourceUid, StringComparison.OrdinalIgnoreCase));
            SourceEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SourceThumbnail_ImageFailed(object sender, ExceptionRoutedEventArgs args) {
            if (sender is Image { Parent: Grid host } && host.FindName("SourceNoPreview") is UIElement placeholder)
                placeholder.Visibility = Visibility.Visible;
        }

        private async void Source_ItemClick(object sender, ItemClickEventArgs args) {
            if (!_isOpen || ViewModel.IsBusy || args.ClickedItem is not WallpaperIndexEntry entry) return;
            SourceFlyout.Hide();
            await ViewModel.SelectLibraryImageAsync(entry);
            if (_closeAfterCancel) Hide();
        }

        private void QueuePreview() {
            if (!_isOpen) return;
            string path = ViewModel.PreviewPath;
            if (path == _previewPath && (_previewLoad != null || PreviewImage.Source != null)) return;
            _previewPath = path;
            _previewLoad?.Cancel();
            var cts = new CancellationTokenSource();
            _previewLoad = cts;
            UpdateLoadingState();
            PreviewImage.Source = null;
            DefaultPreviewPattern.Visibility = Visibility.Visible;
            // Let the dialog render before starting file access and image decoding.
            if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, async () => await LoadPreviewAsync(path, cts))) {
                _previewLoad = null;
                cts.Dispose();
                UpdateLoadingState();
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
                UpdateLoadingState();
            }
        }
        private async void BrowseImage_Click(object sender, RoutedEventArgs e) {
            SourceFlyout.Hide();
            await ViewModel.SelectImageAsync();
            if (_closeAfterCancel) Hide();
        }
        private async void Apply_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args) {
            args.Cancel = true;
            if (ViewModel.IsBusy) return;
            if (ViewModel.UseEnhancement && ViewModel.NeedsEnhancement && !_aiNotice.IsAcknowledged) {
                // Finish this modal before its owner opens the notice; keep the same form/view-model.
                AiNoticeRequested = true;
                args.Cancel = false;
                return;
            }
            await ApplyAsync();
        }

        private async Task ApplyAsync() {
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
