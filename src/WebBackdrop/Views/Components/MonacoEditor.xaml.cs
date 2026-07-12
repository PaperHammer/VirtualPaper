using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common.Logging;

namespace Workloads.Creation.WebBackdrop.Views.Components {
    public sealed partial class MonacoEditor : UserControl {
        public event EventHandler<string>? ContentChanged;

        public string Content {
            get => _content;
            set {
                if (_content == value) return;
                _content = value;
                SetValue(ContentProperty, value);
                _ = SetContentAsync(value);
            }
        }

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(nameof(Content), typeof(string), typeof(MonacoEditor),
                new PropertyMetadata(string.Empty, OnContentPropertyChanged));

        private static void OnContentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is MonacoEditor editor && e.NewValue is string newContent) {
                editor._content = newContent;
                _ = editor.SetContentAsync(newContent);
            }
        }

        public string Language {
            get => (string)GetValue(LanguageProperty);
            set => SetValue(LanguageProperty, value);
        }

        public static readonly DependencyProperty LanguageProperty =
            DependencyProperty.Register(nameof(Language), typeof(string), typeof(MonacoEditor),
                new PropertyMetadata("plaintext", OnLanguagePropertyChanged));

        private static void OnLanguagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is MonacoEditor editor && e.NewValue is string lang) {
                _ = editor.SetLanguageAsync(lang);
            }
        }

        public MonacoEditor() {
            InitializeComponent();
            _ = InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync() {
            try {
                await monacoWebView.EnsureCoreWebView2Async();

                var htmlPath = Path.Combine(AppContext.BaseDirectory, "Views", "Components", "monaco.html");
                if (!File.Exists(htmlPath)) {
                    htmlPath = Path.Combine(AppContext.BaseDirectory, "monaco.html");
                }

                if (File.Exists(htmlPath)) {
                    monacoWebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                } else {
                    monacoWebView.CoreWebView2.NavigateToString(GetFallbackHtml());
                }

                monacoWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e) {
            try {
                var message = e.WebMessageAsJson;
                if (message.Contains("\"type\":\"contentChange\"")) {
                    var content = await monacoWebView.CoreWebView2.ExecuteScriptAsync("editor.getValue()");
                    if (content != null) {
                        content = content.Trim('"').Replace("\\n", "\n").Replace("\\\"", "\"");
                        if (_content != content) {
                            _content = content;
                            ContentChanged?.Invoke(this, content);
                        }
                    }
                }
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private async Task SetContentAsync(string content) {
            if (monacoWebView.CoreWebView2 == null) return;
            try {
                var escaped = content.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
                await monacoWebView.CoreWebView2.ExecuteScriptAsync($"editor.setValue(\"{escaped}\")");
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private async Task SetLanguageAsync(string language) {
            if (monacoWebView.CoreWebView2 == null) return;
            try {
                await monacoWebView.CoreWebView2.ExecuteScriptAsync($"monaco.editor.setModelLanguage(editor.getModel(), \"{language}\")");
            } catch (Exception ex) {
                ArcLog.GetLogger<MonacoEditor>().Error(ex);
            }
        }

        private void MonacoWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) {
            if (!e.IsSuccess) {
                ArcLog.GetLogger<MonacoEditor>().Error($"Monaco navigation failed: {e.WebErrorStatus}");
            }
        }

        private string _content = string.Empty;

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
                </script>
            </body>
            </html>
            """;
        }
    }
}
