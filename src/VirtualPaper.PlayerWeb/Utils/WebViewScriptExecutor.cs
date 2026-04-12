using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.ThreadContext;

namespace VirtualPaper.PlayerWeb.Utils {
    public sealed partial class WebViewScriptExecutor : IDisposable {
        public WebViewScriptExecutor(WebView2 webView) {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            StartLoops();
        }

        /// <summary>
        /// 用于「状态型」脚本（MouseMove / Parallax 等）
        /// 同一个 key 只保留最后一次
        /// </summary>
        public void EnqueueState(string key, string functionName, params object?[] args) {
            _stateScripts[key] = BuildScript(functionName, args);
        }

        /// <summary>
        /// 用于「事件型」脚本（Slider / Checkbox / Button 等）
        /// 不会丢，严格顺序
        /// </summary>
        public void EnqueueEvent(string functionName, params object?[] args) {
            _eventQueue.Enqueue(BuildScript(functionName, args));
        }

        private void StartLoops() {
            RunStateLoop();
            RunEventLoop();
        }

        private void RunStateLoop() {
            Task.Run(async () => {
                try {
                    while (!_cts.IsCancellationRequested) {
                        if (!_stateScripts.IsEmpty) {
                            foreach (var kv in _stateScripts) {
                                Dispatch(kv.Value);
                            }
                            _stateScripts.Clear();
                        }

                        await Task.Delay(StateInterval, _cts.Token);
                    }
                }
                catch (OperationCanceledException) { }
            });
        }

        private void RunEventLoop() {
            Task.Run(async () => {
                try {
                    while (!_cts.IsCancellationRequested) {
                        if (_eventQueue.TryDequeue(out var script)) {
                            Dispatch(script);
                        }
                        else {
                            await Task.Delay(EventIdleDelay, _cts.Token);
                        }
                    }
                }
                catch (OperationCanceledException) { }
            });
        }

        private void Dispatch(string script) {
            CrossThreadInvoker.InvokeOnUIThread(async () => {
                try {
                    await EnsureCoreAsync();
                    await _webView.ExecuteScriptAsync(script);
                }
                catch (Exception ex) {
                    ArcLog.GetLogger<WebViewScriptExecutor>().Error(ex);
                }
            });
        }

        private async Task EnsureCoreAsync() {
            if (_coreReady) return;

            if (_webView.CoreWebView2 == null) {
                await _webView.EnsureCoreWebView2Async();
            }

            _coreReady = true;
        }

        private static string BuildScript(string functionName, object?[] parameters) {
            var sb = new StringBuilder();
            sb.Append(functionName).Append('(');

            for (int i = 0; i < parameters.Length; i++) {
                if (parameters[i] == null) continue;
                sb.Append(JsonSerializer.Serialize(parameters[i]));
                if (i < parameters.Length - 1)
                    sb.Append(", ");
            }

            sb.Append(");");
            return sb.ToString();
        }

        public void Dispose() {
            GC.SuppressFinalize(this);
            _cts.Cancel();
            _cts.Dispose();
            _stateScripts.Clear();
            while (_eventQueue.TryDequeue(out _)) { }
        }

        // ===== 可调参数 =====
        private static readonly TimeSpan StateInterval = TimeSpan.FromMilliseconds(33); // ~30FPS
        private static readonly TimeSpan EventIdleDelay = TimeSpan.FromMilliseconds(5);

        // ===== 内部状态 =====
        private readonly ConcurrentDictionary<string, string> _stateScripts = new();
        private readonly ConcurrentQueue<string> _eventQueue = new();

        private readonly CancellationTokenSource _cts = new();

        private readonly WebView2 _webView;
        private bool _coreReady;
    }
}
