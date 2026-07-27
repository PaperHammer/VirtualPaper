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
using VirtualPaper.PlayerWeb.Core.Utils;
using Workloads.Creation.WebBackdrop.Core.Theme;

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
            var backgroundRole = ActualTheme == ElementTheme.Light
                ? WebBackdropColorRole.WebViewLightBackground
                : WebBackdropColorRole.WebViewDarkBackground;

            monacoWebView.DefaultBackgroundColor = WebBackdropThemeResource.GetColor(this, backgroundRole);
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

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e) {
            try {
                var message = e.TryGetWebMessageAsString();
                ArcLog.GetLogger<MonacoEditor>().Info($"Monaco message: {message}");
                using var json = JsonDocument.Parse(message);
                var type = json.RootElement.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : null;

                await (type switch {
                    "ready" => HandleEditorReadyAsync(),
                    "shortcut" => HandleShortcutAsync(json.RootElement),
                    "contentChange" => HandleContentChangeAsync(),
                    "cursorPositionChange" => HandleCursorPositionChangeAsync(json.RootElement),
                    "markersChanged" => HandleMarkersChangedAsync(json.RootElement),
                    _ => Task.CompletedTask
                });
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private Task HandleEditorReadyAsync() {
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
            return Task.CompletedTask;
        }

        private Task HandleShortcutAsync(JsonElement rootElement) {
            var command = rootElement.GetProperty("command").GetString();
            if (!string.IsNullOrEmpty(command)) {
                ShortcutRequested?.Invoke(this, command);
            }
            return Task.CompletedTask;
        }

        private async Task HandleContentChangeAsync() {
            var content = await monacoWebView.CoreWebView2.ExecuteScriptAsync("window.getValue()");
            if (content != null) {
                content = JsonSerializer.Deserialize<string>(content) ?? string.Empty;
                if (_content != content) {
                    _content = content;
                    ContentChanged?.Invoke(this, content);
                }
            }
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