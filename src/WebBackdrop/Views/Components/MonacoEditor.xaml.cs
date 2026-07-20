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
using Windows.UI;

namespace Workloads.Creation.WebBackdrop.Views.Components {
    public sealed partial class MonacoEditor : UserControl {
        public event EventHandler<string>? ContentChanged;
        public event EventHandler<MonacoCursorPosition>? CursorPositionChanged;
        public event EventHandler<IReadOnlyList<MonacoMarker>>? MarkersChanged;
        public event EventHandler<string>? ShortcutRequested;

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
                editor._content = newContent;
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

        public MonacoEditor() {
            InitializeComponent();
            ActualThemeChanged += MonacoEditor_ActualThemeChanged;
            UpdateTheme();
            _ = InitializeWebViewAsync();
        }

        private void MonacoEditor_ActualThemeChanged(FrameworkElement sender, object args) => UpdateTheme();

        private void UpdateTheme() {
            var theme = ActualTheme == ElementTheme.Light ? "vs" : "vs-dark";
            var background = ActualTheme == ElementTheme.Light
                ? Color.FromArgb(255, 255, 255, 255)
                : Color.FromArgb(255, 30, 30, 30);

            monacoWebView.DefaultBackgroundColor = background;
            if (monacoWebView.CoreWebView2 != null && _isEditorReady) {
                _ = monacoWebView.CoreWebView2.ExecuteScriptAsync($"window.setEditorTheme('{theme}')");
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
                    var hostName = "monaco.localhost";
                    var assetsPath = Path.GetDirectoryName(htmlPath)!;
                    monacoWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        hostName,
                        assetsPath,
                        CoreWebView2HostResourceAccessKind.DenyCors);
                    ArcLog.GetLogger<MonacoEditor>().Info($"Navigating to: https://{hostName}/monaco.html");
                    monacoWebView.CoreWebView2.Navigate($"https://{hostName}/monaco.html");
                } else {
                    ArcLog.GetLogger<MonacoEditor>().Warn("monaco.html not found, using fallback");
                    monacoWebView.CoreWebView2.NavigateToString(GetFallbackHtml());
                }
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e) {
            try {
                var message = e.TryGetWebMessageAsString();
                ArcLog.GetLogger<MonacoEditor>().Info($"Monaco message: {message}");
                using var json = JsonDocument.Parse(message);
                if (!json.RootElement.TryGetProperty("type", out var typeElement)) {
                    return;
                }

                var type = typeElement.GetString();
                if (type == "ready") {
                    _isEditorReady = true;
                    if (_pendingContent != null) {
                        _ = SetContentAsync(_pendingContent);
                        _pendingContent = null;
                    }
                    if (_pendingLanguage != null) {
                        _ = SetLanguageAsync(_pendingLanguage);
                        _pendingLanguage = null;
                    }
                    UpdateTheme();
                    return;
                }
                if (type == "shortcut") {
                    var command = json.RootElement.GetProperty("command").GetString();
                    if (!string.IsNullOrEmpty(command)) {
                        ShortcutRequested?.Invoke(this, command);
                    }
                    return;
                }
                if (type == "contentChange") {
                    var content = await monacoWebView.CoreWebView2.ExecuteScriptAsync("window.getValue()");
                    if (content != null) {
                        content = JsonSerializer.Deserialize<string>(content) ?? string.Empty;
                        if (_content != content) {
                            _content = content;
                            ContentChanged?.Invoke(this, content);
                        }
                    }
                    return;
                }
                if (type == "cursorPositionChange") {
                    var lineNumber = json.RootElement.GetProperty("lineNumber").GetInt32();
                    var column = json.RootElement.GetProperty("column").GetInt32();
                    var selectedCharacterCount = 0;
                    var isSelectedCharacterCountOverflow = false;
                    if (json.RootElement.TryGetProperty("selectedCharacterCount", out var selectedCharacterCountElement)) {
                        var count = selectedCharacterCountElement.GetInt64();
                        isSelectedCharacterCountOverflow = count > MaxSelectedCharacterCount;
                        selectedCharacterCount = isSelectedCharacterCountOverflow
                            ? MaxSelectedCharacterCount
                            : (int)count;
                    }
                    CursorPositionChanged?.Invoke(this, new MonacoCursorPosition(lineNumber, column, selectedCharacterCount, isSelectedCharacterCountOverflow));
                    return;
                }
                if (type == "markersChanged") {
                    var markers = JsonSerializer.Deserialize<List<MonacoMarker>>(json.RootElement.GetProperty("markers").GetRawText(), _jsonSerializerOptions) ?? [];
                    MarkersChanged?.Invoke(this, markers);
                }
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
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

        private async Task SetContentAsync(string content) {
            if (monacoWebView.CoreWebView2 == null || !_isEditorReady) {
                _pendingContent = content;
                return;
            }
            try {
                var serialized = JsonSerializer.Serialize(content);
                await monacoWebView.CoreWebView2.ExecuteScriptAsync($"window.setValue({serialized})");
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
                await monacoWebView.CoreWebView2.ExecuteScriptAsync($"window.setLanguage(\"{language}\")");
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private void MonacoWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) {
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
        private bool _isEditorReady;
        private const int MaxSelectedCharacterCount = int.MaxValue / 2;
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new() {
            PropertyNameCaseInsensitive = true
        };
        private static readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
            AdditionalBrowserArguments = "--disk-cache-size=1 --autoplay-policy=no-user-gesture-required"
        };

        private string GetFallbackHtml() {
            return """
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="UTF-8">
                <style>
                    body { margin: 0; padding: 20px; font-family: monospace; background: #1e1e1e; color: #d4d4d4; }
                    textarea { width: 100%; height: calc(100vh - 40px); background: #1e1e1e; color: #d4d4d4; border: none; font-family: monospace; font-size: 14px; resize: none; }
                </style>
            </head>
            <body>
                <textarea id="editor" placeholder="Monaco Editor loading..."></textarea>
                <script>
                    const editor = document.getElementById('editor');
                    editor.addEventListener('input', () => {
                        window.chrome.webview.postMessage(JSON.stringify({ type: 'contentChange', content: editor.value }));
                    });
                    window.setValue = (val) => { editor.value = val; };
                    window.getValue = () => editor.value;
                    window.setEditorTheme = (theme) => {
                        document.body.style.background = theme === 'vs' ? '#ffffff' : '#1e1e1e';
                        document.getElementById('editor').style.background = theme === 'vs' ? '#ffffff' : '#1e1e1e';
                        document.getElementById('editor').style.color = theme === 'vs' ? '#000000' : '#d4d4d4';
                    };
                    if (window.chrome && window.chrome.webview) {
                        window.chrome.webview.postMessage(JSON.stringify({ type: 'ready' }));
                    }
                </script>
            </body>
            </html>
            """;
        }
    }

    public readonly record struct MonacoCursorPosition(int LineNumber, int Column, int SelectedCharacterCount, bool IsSelectedCharacterCountOverflow);

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