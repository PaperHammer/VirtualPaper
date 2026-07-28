using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Workloads.Creation.WebBackdrop.Core.Preview;
using Workloads.Creation.WebBackdrop.Core.Theme;
using Workloads.Creation.WebBackdrop.Core.Utils;

namespace Workloads.Creation.WebBackdrop.Views.Components.EditorContent {
    public sealed partial class MarkdownEditorView : UserControl {
        private const double PaneMinWidth = 240;

        public event EventHandler<string>? ContentChanged;
        public event EventHandler<MonacoCursorPosition>? CursorPositionChanged;
        public event EventHandler<IReadOnlyList<MonacoMarker>>? MarkersChanged;
        public event EventHandler<string>? ShortcutRequested;
        public event EventHandler<MonacoEditorState>? EditorStateChanged;
        public event EventHandler? PreviewReady;

        public MonacoEditor MonacoEditor => monacoEditor;

        public MarkdownEditorView() {
            InitializeComponent();
            UpdatePreviewWebViewBackground();
            _ = PreparePreviewWebViewAsync();
            ActualThemeChanged += MarkdownEditorView_ActualThemeChanged;
        }

        public void Load(string content, string language) {
            _content = content;
            ResetLayout();
            monacoEditor.EditorContent = content;
            monacoEditor.EditorLanguage = WebEditorFileUtil.GetEditorLanguage(language);
            LoadPreviewDocument(content);
        }

        public Task RevealPositionAsync(int lineNumber, int columnNumber) {
            return monacoEditor.RevealPositionAsync(lineNumber, columnNumber);
        }

        public Task UndoAsync() {
            return monacoEditor.UndoAsync();
        }

        public Task RedoAsync() {
            return monacoEditor.RedoAsync();
        }

        public Task MarkSavedAsync() {
            return monacoEditor.MarkSavedAsync();
        }

        public Task<string> GetContentAsync() {
            return monacoEditor.GetContentAsync();
        }

        public Task<MonacoEditorState> GetEditorStateAsync() {
            return monacoEditor.GetEditorStateAsync();
        }

        public void ReleaseResources() {
            ResetLayout();
            _previewVersion++;
            _pendingPreviewHtml = null;
            _previewDocumentLoaded = false;
            if (previewWebView.CoreWebView2 != null) {
                previewWebView.NavigateToString(string.Empty);
            }
        }

        public void ResetLayout() {
            _resizePointer = null;
            sourceColumn.ClearValue(ColumnDefinition.WidthProperty);
            previewColumn.ClearValue(ColumnDefinition.WidthProperty);
        }

        private void MarkdownEditorView_ActualThemeChanged(FrameworkElement sender, object args) {
            UpdatePreviewWebViewBackground();
            LoadPreviewDocument(_content);
        }

        private void UpdatePreviewWebViewBackground() {
            var backgroundRole = ActualTheme == ElementTheme.Light
                ? WebBackdropColorRole.WebViewLightBackground
                : WebBackdropColorRole.WebViewDarkBackground;
            previewWebView.DefaultBackgroundColor = WebBackdropThemeResource.GetColor(this, backgroundRole);
        }

        private async Task PreparePreviewWebViewAsync() {
            await previewWebView.EnsureCoreWebView2Async();
            UpdatePreviewWebViewBackground();
        }

        private void LoadPreviewDocument(string content) {
            NavigatePreview(MarkdownPreviewRenderer.Render(content, MarkdownPreviewTheme.FromElement(this)));
        }

        private void UpdatePreviewBody(string content) {
            if (!_previewDocumentLoaded || previewWebView.CoreWebView2 == null) {
                LoadPreviewDocument(content);
                return;
            }

            ExecutePreviewBodyUpdate(MarkdownPreviewRenderer.RenderBody(content));
        }

        private async void ExecutePreviewBodyUpdate(string bodyHtml) {
            var version = _previewVersion;
            var script = $"window.setMarkdownHtml({JsonSerializer.Serialize(bodyHtml)})";
            await previewWebView.ExecuteScriptAsync(script);
            if (version != _previewVersion) return;
        }

        private async void NavigatePreview(string html) {
            var version = ++_previewVersion;
            _pendingPreviewHtml = html;
            previewOverlay.Visibility = Visibility.Visible;
            await PreparePreviewWebViewAsync();
            if (version == _previewVersion && _pendingPreviewHtml == html) {
                previewWebView.NavigateToString(html);
                _previewDocumentLoaded = true;
            }
        }

        private void PreviewWebView_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args) {
            previewOverlay.Visibility = Visibility.Collapsed;
            PreviewReady?.Invoke(this, EventArgs.Empty);
        }

        private void Splitter_PointerPressed(object sender, PointerRoutedEventArgs e) {
            if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse) return;

            _resizePointer = e.Pointer;
            _resizeStartX = e.GetCurrentPoint(rootGrid).Position.X;
            _resizeStartLeftWidth = sourceColumn.ActualWidth;
            _resizeStartRightWidth = previewColumn.ActualWidth;
            splitter.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void Splitter_PointerMoved(object sender, PointerRoutedEventArgs e) {
            if (_resizePointer == null) return;

            var delta = e.GetCurrentPoint(rootGrid).Position.X - _resizeStartX;
            var totalWidth = _resizeStartLeftWidth + _resizeStartRightWidth;
            var leftWidth = Math.Clamp(_resizeStartLeftWidth + delta, PaneMinWidth, totalWidth - PaneMinWidth);
            var leftRatio = leftWidth / totalWidth;
            sourceColumn.Width = new GridLength(leftRatio, GridUnitType.Star);
            previewColumn.Width = new GridLength(1 - leftRatio, GridUnitType.Star);
            e.Handled = true;
        }

        private void Splitter_PointerReleased(object sender, PointerRoutedEventArgs e) {
            if (_resizePointer != null) {
                splitter.ReleasePointerCapture(_resizePointer);
            }

            _resizePointer = null;
            e.Handled = true;
        }

        private void Splitter_PointerCaptureLost(object sender, PointerRoutedEventArgs e) {
            _resizePointer = null;
        }

        private void Splitter_PointerEntered(object sender, PointerRoutedEventArgs e) {
            splitterLine.Fill = (Brush)Resources[WebBackdropThemeResource.GetBrushKey(WebBackdropBrushRole.SplitterHover)];
        }

        private void Splitter_PointerExited(object sender, PointerRoutedEventArgs e) {
            if (_resizePointer == null) {
                splitterLine.ClearValue(Shape.FillProperty);
            }
        }

        private void MonacoEditor_ContentChanged(object? sender, string content) {
            _content = content;
            UpdatePreviewBody(content);
            ContentChanged?.Invoke(this, content);
        }

        private void MonacoEditor_CursorPositionChanged(object? sender, MonacoCursorPosition position) {
            CursorPositionChanged?.Invoke(this, position);
        }

        private void MonacoEditor_MarkersChanged(object? sender, IReadOnlyList<MonacoMarker> markers) {
            MarkersChanged?.Invoke(this, markers);
        }

        private void MonacoEditor_ShortcutRequested(object? sender, string command) {
            ShortcutRequested?.Invoke(this, command);
        }

        private void MonacoEditor_EditorStateChanged(object? sender, MonacoEditorState state) {
            EditorStateChanged?.Invoke(this, state);
        }

        private string _content = string.Empty;
        private string? _pendingPreviewHtml;
        private bool _previewDocumentLoaded;
        private int _previewVersion;
        private Pointer? _resizePointer;
        private double _resizeStartX;
        private double _resizeStartLeftWidth;
        private double _resizeStartRightWidth;
    }
}
