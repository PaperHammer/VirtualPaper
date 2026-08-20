using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.PlayerWeb.Core.Utils;
using Workloads.Creation.WebBackdrop.Core.Theme;

namespace Workloads.Creation.WebBackdrop.Views.Components {
    public sealed partial class MonacoEditor : UserControl {
        // [已废弃，暂注释] 从未触发；全文同步已改为保存时 GetContentAsync 拉取
        // public event EventHandler<string>? ContentChanged;
        public event EventHandler? ContentModified;
        public event EventHandler<MonacoCursorPosition>? CursorPositionChanged;
        public event EventHandler<IReadOnlyList<MonacoMarker>>? MarkersChanged;
        public event EventHandler<string>? ShortcutRequested;
        public event EventHandler<MonacoEditorState>? EditorStateChanged;
        public event EventHandler<string>? FileOpenRequested;
        public event EventHandler<string>? NavigationRequested;

        public string EditorContent {
            get => _content;
            set {
                if (_content == value) return;
                _content = value;
                SetValue(EditorContentProperty, value);
                _ = SetContentAsync(value);
            }
        }
        public static readonly DependencyProperty EditorContentProperty =
            DependencyProperty.Register(nameof(EditorContent), typeof(string), typeof(MonacoEditor),
                new PropertyMetadata(string.Empty, OnEditorContentPropertyChanged));

        private static void OnEditorContentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is MonacoEditor editor && e.NewValue is string newContent) {
                _ = editor.SetContentAsync(newContent);
            }
        }

        public string EditorLanguage {
            get => (string)GetValue(EditorLanguageProperty);
            set => SetValue(EditorLanguageProperty, value);
        }

        public static readonly DependencyProperty EditorLanguageProperty =
            DependencyProperty.Register(nameof(EditorLanguage), typeof(string), typeof(MonacoEditor),
                new PropertyMetadata("plaintext", OnEditorLanguagePropertyChanged));

        private static void OnEditorLanguagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is MonacoEditor editor && e.NewValue is string lang) {
                editor._pendingLanguage = lang;
                _ = editor.SetLanguageAsync(lang);
            }
        }

        public string FilePath {
            get => (string)GetValue(FilePathProperty);
            set => SetValue(FilePathProperty, value);
        }
        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register(nameof(FilePath), typeof(string), typeof(MonacoEditor),
                new PropertyMetadata(string.Empty));

        public MonacoEditor() {
            InitializeComponent();
            ActualThemeChanged += MonacoEditor_ActualThemeChanged;
            UpdateTheme();
            _ = InitializeWebViewAsync();
        }

        private void MonacoEditor_ActualThemeChanged(FrameworkElement sender, object args) => UpdateTheme();

        private void UpdateTheme() {
            var theme = ActualTheme == ElementTheme.Light ? "vs" : "vs-dark";
            var backgroundRole = ActualTheme == ElementTheme.Light
                ? WebBackdropColorRole.WebViewLightBackground
                : WebBackdropColorRole.WebViewDarkBackground;

            monacoWebView.DefaultBackgroundColor = WebBackdropThemeResource.GetColor(this, backgroundRole);
            if (monacoWebView.CoreWebView2 != null && _isEditorReady) {
                _ = monacoWebView.CoreWebView2.ExecuteScriptAsync($"window.setEditorTheme({JsonSerializer.Serialize(theme)})");
            }
        }

        private async Task InitializeWebViewAsync() {
            try {
                Directory.CreateDirectory(Constants.CommonPaths.TempWebView2Dir);
                var env = await CoreWebView2Environment.CreateWithOptionsAsync(null, Constants.CommonPaths.TempWebView2Dir, _environmentOptions);
                await monacoWebView.EnsureCoreWebView2Async(env);

#if DEBUG
                monacoWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
                monacoWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                monacoWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                monacoWebView.CoreWebView2.OpenDevToolsWindow();
#else
                monacoWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                monacoWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                monacoWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif

                monacoWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                monacoWebView.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed;

                var htmlPath = Path.Combine(AppContext.BaseDirectory, "WebBackdrop", "Assets", "monaco.html");
                ArcLog.GetLogger<MonacoEditor>().Info($"Monaco html path: {htmlPath}");

                if (File.Exists(htmlPath)) {
                    var uri = monacoWebView.CoreWebView2.NavigateToLocalFile(htmlPath);
                    ArcLog.GetLogger<MonacoEditor>().Info($"Navigating to: {uri}");
                } else {
                    ArcLog.GetLogger<MonacoEditor>().Warn("monaco.html not found, using fallback");
                    monacoWebView.CoreWebView2.NavigateToString(GetFallbackHtml());
                }
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e) {
            _ = HandleWebMessageAsync(e);
        }

        private async Task HandleWebMessageAsync(CoreWebView2WebMessageReceivedEventArgs e) {
            try {
                var message = e.TryGetWebMessageAsString();
                using var json = JsonDocument.Parse(message);
                var type = json.RootElement.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : null;

                DebugUtil.Output($"Monaco message type: {type}");
                await (type switch {
                    "ready" => HandleEditorReadyAsync(),
                    "shortcut" => HandleShortcutAsync(json.RootElement),
                    "contentChange" => HandleContentChangeAsync(),
                    "editorStateChanged" => HandleEditorStateChangedAsync(json.RootElement),
                    "cursorPositionChange" => HandleCursorPositionChangeAsync(json.RootElement),
                    "markersChanged" => HandleMarkersChangedAsync(json.RootElement),
                    "openFile" => HandleOpenFileAsync(json.RootElement),
                    "navigateBackForward" => HandleNavigateBackForwardAsync(json.RootElement),
                    _ => Task.CompletedTask
                });
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private Task HandleEditorReadyAsync() {
            if (_isEditorReady) return Task.CompletedTask;
            _isEditorReady = true;
            if (_pendingContent != null) {
                _ = SetContentAsync(_pendingContent);
                _pendingContent = null;
            }
            if (_pendingLanguage != null) {
                _ = SetLanguageAsync(_pendingLanguage);
                _pendingLanguage = null;
            }
            if (_pendingEncoding != null) {
                _ = SetEncodingAsync(_pendingEncoding);
            }
            if (_pendingIndentOptions is { } indentOptions) {
                _ = SetIndentOptionsAsync(indentOptions.TabSize, indentOptions.InsertSpaces);
            }
            UpdateTheme();
            return Task.CompletedTask;
        }

        private Task HandleShortcutAsync(JsonElement rootElement) {
            var command = rootElement.GetProperty("command").GetString();
            if (!string.IsNullOrEmpty(command)) {
                ShortcutRequested?.Invoke(this, command);
            }
            return Task.CompletedTask;
        }

        private Task HandleContentChangeAsync() {
            ContentModified?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        private Task HandleEditorStateChangedAsync(JsonElement rootElement) {
            var isSaved = rootElement.TryGetProperty("isSaved", out var isSavedElement) && isSavedElement.GetBoolean();
            var lineEnding = rootElement.TryGetProperty("lineEnding", out var lineEndingElement)
                ? lineEndingElement.GetString() ?? "LF"
                : "LF";
            var encoding = rootElement.TryGetProperty("encoding", out var encodingElement)
                ? encodingElement.GetString() ?? "UTF-8"
                : "UTF-8";
            var indent = rootElement.TryGetProperty("indent", out var indentElement)
                ? indentElement.GetString() ?? "Spaces: 2"
                : "Spaces: 2";
            var filePath = rootElement.TryGetProperty("filePath", out var filePathElement)
                ? filePathElement.GetString()
                : null;
            EditorStateChanged?.Invoke(this, new MonacoEditorState(isSaved, lineEnding, encoding, indent, filePath));
            return Task.CompletedTask;
        }

        private Task HandleCursorPositionChangeAsync(JsonElement rootElement) {
            var lineNumber = rootElement.GetProperty("lineNumber").GetInt32();
            var column = rootElement.GetProperty("column").GetInt32();
            var selectedCharacterCount = 0;
            var isSelectedCharacterCountOverflow = false;
            if (rootElement.TryGetProperty("selectedCharacterCount", out var selectedCharacterCountElement)) {
                var count = selectedCharacterCountElement.GetInt64();
                isSelectedCharacterCountOverflow = count > MaxSelectedCharacterCount;
                selectedCharacterCount = isSelectedCharacterCountOverflow
                    ? MaxSelectedCharacterCount
                    : (int)count;
            }
            CursorPositionChanged?.Invoke(this, new MonacoCursorPosition(lineNumber, column, selectedCharacterCount, isSelectedCharacterCountOverflow));
            return Task.CompletedTask;
        }

        private Task HandleMarkersChangedAsync(JsonElement rootElement) {
            var markers = JsonSerializer.Deserialize<List<MonacoMarker>>(rootElement.GetProperty("markers").GetRawText(), _jsonSerializerOptions) ?? [];
            MarkersChanged?.Invoke(this, markers);
            return Task.CompletedTask;
        }

        private Task HandleOpenFileAsync(JsonElement rootElement) {
            var filePath = rootElement.GetProperty("filePath").GetString();
            if (!string.IsNullOrEmpty(filePath)) {
                FileOpenRequested?.Invoke(this, filePath);
            }
            return Task.CompletedTask;
        }

        private Task HandleNavigateBackForwardAsync(JsonElement rootElement) {
            var filePath = rootElement.GetProperty("filePath").GetString();
            if (!string.IsNullOrEmpty(filePath)) {
                NavigationRequested?.Invoke(this, filePath);
            }
            return Task.CompletedTask;
        }

        public async Task RevealPositionAsync(int lineNumber, int column) {
            if (monacoWebView.CoreWebView2 == null || !_isEditorReady) {
                return;
            }
            try {
                await monacoWebView.CoreWebView2.ExecuteScriptAsync($"window.revealPosition({lineNumber}, {column})");
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        public void OpenDevTools() {
            if (monacoWebView.CoreWebView2 != null) {
                monacoWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                monacoWebView.CoreWebView2.OpenDevToolsWindow();
            }
        }

        public Task UndoAsync() {
            return ExecuteEditorCommandAsync("window.undo()");
        }

        public Task RedoAsync() {
            return ExecuteEditorCommandAsync("window.redo()");
        }

        public Task MarkSavedAsync(int? versionId = null) {
            var script = versionId.HasValue
                ? $"window.markSaved({versionId.Value})"
                : "window.markSaved()";
            return ExecuteEditorCommandAsync(script);
        }

        /// <summary>
        /// 释放指定文件对应的 Monaco 模型，避免一次会话打开过的文件模型持续累积。
        /// </summary>
        public async Task DisposeModelAsync(string filePath) {
            if (monacoWebView.CoreWebView2 == null || !_isEditorReady) return;

            try {
                var serialized = JsonSerializer.Serialize(filePath);
                await monacoWebView.CoreWebView2.ExecuteScriptAsync($"window.disposeModel({serialized})");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        public async Task<string> GetContentAsync() {
            if (monacoWebView.CoreWebView2 == null || !_isEditorReady) {
                return _content;
            }

            try {
                var result = await monacoWebView.CoreWebView2.ExecuteScriptAsync("window.getValue()");
                return JsonSerializer.Deserialize<string>(result) ?? string.Empty;
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
                return _content;
            }
        }

        /// <summary>
        /// 一次性获取编辑器内容与模型版本号，供保存时精确标记“已保存版本”，
        /// 避免保存与继续输入交错时把尚未落盘的内容误标为已保存。
        /// </summary>
        public async Task<(string Content, int VersionId)> GetContentWithVersionAsync() {
            if (monacoWebView.CoreWebView2 == null || !_isEditorReady) {
                return (_content, 0);
            }

            try {
                var result = await monacoWebView.CoreWebView2.ExecuteScriptAsync("window.getValueWithVersion()");
                var json = JsonSerializer.Deserialize<string>(result) ?? string.Empty;
                var pair = JsonSerializer.Deserialize<(string content, int version)>(json, _jsonSerializerOptions);
                return (pair.content, pair.version);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
                return (_content, 0);
            }
        }

        public async Task ReplaceLineEndingsAsync(string targetEnding) {
            if (monacoWebView.CoreWebView2 == null || !_isEditorReady) return;

            try {
                await monacoWebView.CoreWebView2.ExecuteScriptAsync($"window.replaceLineEndings('{targetEnding}')");
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        public async Task SetEncodingAsync(string encoding) {
            _pendingEncoding = encoding;
            if (monacoWebView.CoreWebView2 == null || !_isEditorReady) return;

            try {
                var serialized = JsonSerializer.Serialize(encoding);
                await monacoWebView.CoreWebView2.ExecuteScriptAsync($"window.setEncoding({serialized})");
                _pendingEncoding = null;
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        public async Task SetIndentOptionsAsync(int tabSize, bool insertSpaces) {
            _pendingIndentOptions = (tabSize, insertSpaces);
            if (monacoWebView.CoreWebView2 == null || !_isEditorReady) return;

            try {
                var insertSpacesStr = insertSpaces ? "true" : "false";
                await monacoWebView.CoreWebView2.ExecuteScriptAsync($"window.setIndentOptions({tabSize}, {insertSpacesStr})");
                _pendingIndentOptions = null;
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private static MonacoEditorState DefaultEditorState => new(true, "LF", "UTF-8", "Spaces: 2");

        public async Task<MonacoEditorState> GetEditorStateAsync() {
            if (monacoWebView.CoreWebView2 == null || !_isEditorReady) {
                return DefaultEditorState;
            }

            try {
                var result = await monacoWebView.CoreWebView2.ExecuteScriptAsync("window.getEditorState()");
                var json = JsonSerializer.Deserialize<string>(result) ?? string.Empty;
                var state = JsonSerializer.Deserialize<MonacoEditorState>(json, _jsonSerializerOptions);
                return state ?? DefaultEditorState;
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
                return DefaultEditorState;
            }
        }

        private async Task ExecuteEditorCommandAsync(string script) {
            if (monacoWebView.CoreWebView2 == null || !_isEditorReady) {
                return;
            }

            try {
                await monacoWebView.CoreWebView2.ExecuteScriptAsync(script);
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private async Task SetContentAsync(string content) {
            if (monacoWebView.CoreWebView2 == null || !_isEditorReady) {
                _pendingContent = content;
                return;
            }
            try {
                var serialized = JsonSerializer.Serialize(content);
                var filePath = FilePath;
                if (!string.IsNullOrEmpty(filePath)) {
                    var filePathSerialized = JsonSerializer.Serialize(filePath);
                    await monacoWebView.CoreWebView2.ExecuteScriptAsync($"window.setValue({serialized}, {filePathSerialized})");
                } else {
                    await monacoWebView.CoreWebView2.ExecuteScriptAsync($"window.setValue({serialized})");
                }
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private async Task SetLanguageAsync(string language) {
            if (monacoWebView.CoreWebView2 == null || !_isEditorReady) {
                _pendingLanguage = language;
                return;
            }
            try {
                var serializedLanguage = JsonSerializer.Serialize(language);
                await monacoWebView.CoreWebView2.ExecuteScriptAsync($"window.setLanguage({serializedLanguage})");
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private void MonacoWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs e) {
            if (!e.IsSuccess) {
                ArcLog.GetLogger<MonacoEditor>().Error($"Monaco navigation failed: {e.WebErrorStatus}");
                return;
            }

            ArcLog.GetLogger<MonacoEditor>().Info("Monaco navigation completed");
        }

        private void CoreWebView2_ProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args) {
            if (args.Reason == CoreWebView2ProcessFailedReason.Unresponsive)
                return;

            ArcLog.GetLogger<MonacoEditor>().Error($"CoreWebView2 process failed: {args.Reason}");
        }

        private string _content = string.Empty;
        private string? _pendingContent;
        private string? _pendingLanguage;
        private string? _pendingEncoding;
        private (int TabSize, bool InsertSpaces)? _pendingIndentOptions;
        private bool _isEditorReady;
        private const int MaxSelectedCharacterCount = int.MaxValue / 2;
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new() {
            PropertyNameCaseInsensitive = true
        };
        private static readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
            AdditionalBrowserArguments = "--disk-cache-size=1 --autoplay-policy=no-user-gesture-required"
        };

        private string GetFallbackHtml() {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, Constants.ModuleName.WebBackdrop, "Assets", "monaco-fallback.html"))
                .Replace("{{LightBackground}}", WebBackdropThemeResource.GetString(this, WebBackdropStringRole.MonacoFallbackLightBackground))
                .Replace("{{DarkBackground}}", WebBackdropThemeResource.GetString(this, WebBackdropStringRole.MonacoFallbackDarkBackground))
                .Replace("{{LightForeground}}", WebBackdropThemeResource.GetString(this, WebBackdropStringRole.MonacoFallbackLightForeground))
                .Replace("{{DarkForeground}}", WebBackdropThemeResource.GetString(this, WebBackdropStringRole.MonacoFallbackDarkForeground));
        }
    }

    public readonly record struct MonacoCursorPosition(int LineNumber, int Column, int SelectedCharacterCount, bool IsSelectedCharacterCountOverflow);

    public sealed record MonacoEditorState(bool IsSaved, string LineEnding, string Encoding, string Indent, string? FilePath = null);

    public sealed class MonacoMarker {
        public int Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StartLineNumber { get; set; }
        public int StartColumn { get; set; }
        public int EndLineNumber { get; set; }
        public int EndColumn { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}