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

        // [已废弃，暂注释] 上游 MonacoEditor.ContentChanged 从未触发
        // 完整内容已获取/同步的通知
        // public event EventHandler<string>? ContentChanged;
        // 内容被编辑过的轻量通知，不携带文本；用于立即标记 IsSaved = false、更新标签/文件树状态，输入路径不跨 WebView 传输全文
        public event EventHandler? ContentModified;
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

        public void Load(string filePath, string content, string language) {
            _content = content ?? string.Empty;
            // 丢弃上一文件遗留的防抖更新任务，避免旧文件的预览内容覆盖新文件
            _previewUpdateVersion++;
            ResetLayout();
            // 关联文件 URI，使状态上报携带路径，保存状态（含 undo/redo）才能正确匹配
            monacoEditor.FilePath = filePath;
            monacoEditor.EditorLanguage = WebEditorFileUtil.GetEditorLanguage(language);
            monacoEditor.EditorContent = _content;
            LoadPreviewDocument(_content);
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

        public Task CopyLineUpAsync() {
            return monacoEditor.CopyLineUpAsync();
        }

        public Task CopyLineDownAsync() {
            return monacoEditor.CopyLineDownAsync();
        }

        public Task MoveLineUpAsync() {
            return monacoEditor.MoveLineUpAsync();
        }

        public Task MoveLineDownAsync() {
            return monacoEditor.MoveLineDownAsync();
        }

        public Task FocusEditorAsync() {
            return monacoEditor.FocusEditorAsync();
        }

        public Task MarkSavedAsync(int? versionId = null) {
            return monacoEditor.MarkSavedAsync(versionId);
        }

        public Task<string> GetContentAsync() {
            return monacoEditor.GetContentAsync();
        }

        public Task<(string Content, int VersionId, string? FilePath)> GetContentWithVersionAsync() {
            return monacoEditor.GetContentWithVersionAsync();
        }

        public Task WaitForContentUpdateAsync() {
            return monacoEditor.WaitForContentUpdateAsync();
        }

        public void PrepareEncoding(string encoding) {
            monacoEditor.PrepareEncoding(encoding);
        }

        public Task<MonacoEditorState> GetEditorStateAsync() {
            return monacoEditor.GetEditorStateAsync();
        }

        public void ReleaseResources() {
            ResetLayout();
            // 同时使在途的防抖预览更新任务失效
            _previewUpdateVersion++;
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
            NavigatePreview(MarkdownPreviewRenderer.Render(content ?? string.Empty, MarkdownPreviewTheme.FromElement(this)));
        }

        private void UpdatePreviewBody(string content) {
            content ??= string.Empty;
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

        // [已废弃，暂注释] 上游 MonacoEditor.ContentChanged 从未触发
        /*
        private void MonacoEditor_ContentChanged(object? sender, string content) {
            _content = content;
            UpdatePreviewBody(content);
            ContentChanged?.Invoke(this, content);
        }
        */

        private void MonacoEditor_ContentModified(object? sender, EventArgs e) {
            ContentModified?.Invoke(this, EventArgs.Empty);
            _previewUpdateVersion++;
            _ = UpdatePreviewAsync(_previewUpdateVersion);
        }

        private async Task UpdatePreviewAsync(int version) {
            await Task.Delay(150);
            if (version != _previewUpdateVersion) return;

            var content = await monacoEditor.GetContentAsync() ?? string.Empty;
            if (version != _previewUpdateVersion || _content == content) return;

            _content = content;
            UpdatePreviewBody(content);
        }

        private void MonacoEditor_CursorPositionChanged(object? sender, MonacoCursorPosition position) {
            CursorPositionChanged?.Invoke(sender ?? this, position);
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
        private int _previewUpdateVersion;
        private Pointer? _resizePointer;
        private double _resizeStartX;
        private double _resizeStartLeftWidth;
        private double _resizeStartRightWidth;
    }
}
