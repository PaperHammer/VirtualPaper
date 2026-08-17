using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;

namespace Workloads.Creation.WebBackdrop.Views.Components.EditorContent {
    public sealed partial class WebEditorContentView : UserControl {
        public event EventHandler<string>? ContentChanged;
        public event EventHandler? ContentModified;
        public event EventHandler<MonacoCursorPosition>? CursorPositionChanged;
        public event EventHandler<IReadOnlyList<MonacoMarker>>? MarkersChanged;
        public event EventHandler<string>? ShortcutRequested;
        public event EventHandler<MonacoEditorState>? EditorStateChanged;

        private const int OverlayDelayMilliseconds = 80;

        public MonacoEditor ActiveMonacoEditor => _currentKind == WebEditorFileKind.Markdown
            ? markdownEditor.MonacoEditor
            : textEditor;

        public MonacoEditor TextEditor => textEditor;
        public MarkdownEditorView MarkdownEditor => markdownEditor;

        public WebEditorContentView() {
            InitializeComponent();
        }

        public void LoadFile(WebEditorFile? file, string language) {
            ShowContentOverlay();
            var previousKind = _currentKind;
            _currentKind = file?.Kind;
            _previousKind = previousKind;
            HideAll();

            if (file == null) {
                textEditor.EditorContent = string.Empty;
                textEditor.EditorLanguage = WebEditorFileUtil.GetEditorLanguage(language);
                welcomeView.Visibility = Visibility.Visible;
                CompleteContentSwitch(previousKind);
                return;
            }

            switch (file.Kind) {
                case WebEditorFileKind.Image:
                    imagePreview.Load(file.FilePath);
                    imagePreview.Visibility = Visibility.Visible;
                    break;
                case WebEditorFileKind.Markdown:
                    markdownEditor.Load(file.FilePath, file.Content, language);
                    markdownEditor.Visibility = Visibility.Visible;
                    break;
                case WebEditorFileKind.Text:
                    textEditor.FilePath = file.FilePath;
                    textEditor.EditorContent = file.Content;
                    textEditor.EditorLanguage = WebEditorFileUtil.GetEditorLanguage(language);
                    textEditor.Visibility = Visibility.Visible;
                    CompleteContentSwitch(previousKind);
                    break;
                case WebEditorFileKind.Unsupported:
                    fallbackView.Visibility = Visibility.Visible;
                    CompleteContentSwitch(previousKind);
                    break;
            }
        }

        public void ReloadContent(WebEditorFile file, string language) {
            if (_currentKind != file.Kind) {
                LoadFile(file, language);
                return;
            }

            switch (file.Kind) {
                case WebEditorFileKind.Text:
                    textEditor.EditorContent = file.Content;
                    textEditor.EditorLanguage = WebEditorFileUtil.GetEditorLanguage(language);
                    break;
                case WebEditorFileKind.Markdown:
                    markdownEditor.Load(file.FilePath, file.Content, language);
                    break;
                case WebEditorFileKind.Image:
                    imagePreview.Reload(file.FilePath);
                    break;
            }
        }

        public Task RevealPositionAsync(int lineNumber, int columnNumber) {
            return _currentKind == WebEditorFileKind.Markdown
                ? markdownEditor.RevealPositionAsync(lineNumber, columnNumber)
                : textEditor.RevealPositionAsync(lineNumber, columnNumber);
        }

        public Task UndoAsync() {
            return _currentKind == WebEditorFileKind.Markdown
                ? markdownEditor.UndoAsync()
                : textEditor.UndoAsync();
        }

        public Task RedoAsync() {
            return _currentKind == WebEditorFileKind.Markdown
                ? markdownEditor.RedoAsync()
                : textEditor.RedoAsync();
        }

        public Task MarkSavedAsync() {
            return _currentKind == WebEditorFileKind.Markdown
                ? markdownEditor.MarkSavedAsync()
                : textEditor.MarkSavedAsync();
        }

        public Task<string> GetContentAsync() {
            return _currentKind == WebEditorFileKind.Markdown
                ? markdownEditor.GetContentAsync()
                : textEditor.GetContentAsync();
        }

        public Task<MonacoEditorState> GetEditorStateAsync() {
            return _currentKind == WebEditorFileKind.Markdown
                ? markdownEditor.GetEditorStateAsync()
                : textEditor.GetEditorStateAsync();
        }

        public void ReleaseResources() {
            imagePreview.ReleaseResources();
            markdownEditor.ReleaseResources();
        }

        /// <summary>
        /// 释放指定文件对应的 Monaco 模型，避免一次会话打开过的文件模型持续累积。
        /// </summary>
        public Task DisposeModelAsync(string filePath) {
            return Task.WhenAll(
                textEditor.DisposeModelAsync(filePath),
                markdownEditor.MonacoEditor.DisposeModelAsync(filePath));
        }

        private void ShowContentOverlay() {
            _overlayVersion++;
            contentOverlay.Visibility = Visibility.Visible;
        }

        private void HideContentOverlayAfterDelay() {
            var version = _overlayVersion;
            _ = HideContentOverlayAfterDelayAsync(version);
        }

        private async Task HideContentOverlayAfterDelayAsync(int version) {
            await Task.Delay(OverlayDelayMilliseconds);
            if (version != _overlayVersion) return;
            contentOverlay.Visibility = Visibility.Collapsed;
        }

        private void CompleteContentSwitch(WebEditorFileKind? previousKind) {
            ReleaseInactivePreviewResources(previousKind);
            HideContentOverlayAfterDelay();
        }

        private void ReleaseInactivePreviewResources(WebEditorFileKind? previousKind) {
            if (previousKind == WebEditorFileKind.Markdown && _currentKind != WebEditorFileKind.Markdown) {
                markdownEditor.ReleaseResources();
            }
            if (previousKind == WebEditorFileKind.Image && _currentKind != WebEditorFileKind.Image) {
                imagePreview.ReleaseResources();
            }
        }

        private void HideAll() {
            textEditor.Visibility = Visibility.Collapsed;
            markdownEditor.Visibility = Visibility.Collapsed;
            imagePreview.Visibility = Visibility.Collapsed;
            fallbackView.Visibility = Visibility.Collapsed;
            welcomeView.Visibility = Visibility.Collapsed;
        }

        private void TextEditor_ContentChanged(object? sender, string content) {
            ContentChanged?.Invoke(this, content);
        }

        private void TextEditor_ContentModified(object? sender, EventArgs e) {
            ContentModified?.Invoke(this, EventArgs.Empty);
        }

        private void Editor_CursorPositionChanged(object? sender, MonacoCursorPosition position) {
            CursorPositionChanged?.Invoke(this, position);
        }

        private void Editor_MarkersChanged(object? sender, IReadOnlyList<MonacoMarker> markers) {
            MarkersChanged?.Invoke(this, markers);
        }

        private void Editor_ShortcutRequested(object? sender, string command) {
            ShortcutRequested?.Invoke(this, command);
        }

        private void Editor_EditorStateChanged(object? sender, MonacoEditorState state) {
            EditorStateChanged?.Invoke(this, state);
        }

        private void Preview_PreviewReady(object? sender, EventArgs e) {
            CompleteContentSwitch(_previousKind);
        }

        private WebEditorFileKind? _currentKind;
        private WebEditorFileKind? _previousKind;
        private int _overlayVersion;
    }
}
