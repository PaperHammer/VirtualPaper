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
        public event EventHandler<MonacoCursorPosition>? CursorPositionChanged;
        public event EventHandler<IReadOnlyList<MonacoMarker>>? MarkersChanged;
        public event EventHandler<string>? ShortcutRequested;
        public event EventHandler<MonacoEditorState>? EditorStateChanged;

        public WebEditorContentView() {
            InitializeComponent();
        }

        public void LoadFile(WebEditorFile? file, string language) {
            _currentKind = file?.Kind;
            HideAll();

            if (file == null) {
                textEditor.EditorContent = string.Empty;
                textEditor.EditorLanguage = WebEditorFileUtil.GetEditorLanguage(language);
                welcomeView.Visibility = Visibility.Visible;
                return;
            }

            switch (file.Kind) {
                case WebEditorFileKind.Image:
                    imagePreview.Load(file.FilePath);
                    imagePreview.Visibility = Visibility.Visible;
                    break;
                case WebEditorFileKind.Markdown:
                    markdownEditor.Load(file.Content, language);
                    markdownEditor.Visibility = Visibility.Visible;
                    break;
                case WebEditorFileKind.Text:
                    textEditor.EditorContent = file.Content;
                    textEditor.EditorLanguage = WebEditorFileUtil.GetEditorLanguage(language);
                    textEditor.Visibility = Visibility.Visible;
                    break;
                case WebEditorFileKind.Unsupported:
                    fallbackView.Visibility = Visibility.Visible;
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

        public Task<MonacoEditorState> GetEditorStateAsync() {
            return _currentKind == WebEditorFileKind.Markdown
                ? markdownEditor.GetEditorStateAsync()
                : textEditor.GetEditorStateAsync();
        }

        public void ReleaseResources() {
            imagePreview.ReleaseResources();
            markdownEditor.ReleaseResources();
        }

        private void HideAll() {
            textEditor.Visibility = Visibility.Collapsed;
            markdownEditor.Visibility = Visibility.Collapsed;
            markdownEditor.ReleaseResources();
            imagePreview.Visibility = Visibility.Collapsed;
            imagePreview.ReleaseResources();
            fallbackView.Visibility = Visibility.Collapsed;
            welcomeView.Visibility = Visibility.Collapsed;
        }

        private void TextEditor_ContentChanged(object? sender, string content) {
            ContentChanged?.Invoke(this, content);
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

        private WebEditorFileKind? _currentKind;
    }
}
