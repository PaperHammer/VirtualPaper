using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.ThreadContext;

namespace VirtualPaper.PlayerWeb.Core.Utils {
    public sealed partial class WebViewScriptExecutor : IDisposable {
        public WebViewScriptExecutor(WebView2 webView)
            : this(webView ?? throw new ArgumentNullException(nameof(webView)), null) { }

        internal WebViewScriptExecutor(Func<string, Task> dispatch)
            : this(null, dispatch ?? throw new ArgumentNullException(nameof(dispatch))) { }

        private WebViewScriptExecutor(WebView2? webView, Func<string, Task>? dispatch) {
            _webView = webView;
            _dispatch = dispatch ?? DispatchToWebViewAsync;
            _stopToken = _cts.Token;
            _completion = Task.WhenAll(Task.Run(RunStateLoopAsync), Task.Run(RunEventLoopAsync));
        }

        internal Task Completion => _completion;

        /// <summary>
        /// 用于「状态型」脚本（MouseMove / Parallax 等）
        /// 同一个 key 只保留最后一次
        /// </summary>
        public void EnqueueState(string key, string functionName, params object?[] args) {
            if (Volatile.Read(ref _disposed) != 0) return;
            string script = BuildScript(functionName, args);
            lock (_queueLock) {
                // Disposal may have started while the script was being serialized.
                if (_disposed != 0) return;
                _stateScripts[key] = script;
                if (_stateSignal.CurrentCount == 0) _stateSignal.Release();
            }
        }

        /// <summary>
        /// 用于「事件型」脚本（加载 / 播放 / 停止等命令）
        /// 运行期间不合并，按入队顺序执行
        /// </summary>
        public void EnqueueEvent(string functionName, params object?[] args) {
            EnqueueRawScript(BuildScript(functionName, args));
        }

        public void EnqueueRawScript(string script) {
            lock (_queueLock) {
                if (_disposed != 0) return;
                _eventQueue.Enqueue(script);
                _eventSignal.Release();
            }
        }

        private async Task RunStateLoopAsync() {
            try {
                while (!_stopToken.IsCancellationRequested) {
                    await _stateSignal.WaitAsync(_stopToken);
                    await Task.Delay(StateInterval, _stopToken);
                    System.Collections.Generic.KeyValuePair<string, string>[] snapshot;
                    lock (_queueLock) {
                        snapshot = _stateScripts.ToArray();
                        _stateScripts.Clear();
                    }
                    foreach (var kv in snapshot) await DispatchAsync(kv.Value);
                }
            }
            catch (OperationCanceledException) when (_stopToken.IsCancellationRequested) { }
        }

        private async Task RunEventLoopAsync() {
            try {
                while (!_stopToken.IsCancellationRequested) {
                    await _eventSignal.WaitAsync(_stopToken);
                    if (_eventQueue.TryDequeue(out var script)) await DispatchAsync(script);
                }
            }
            catch (OperationCanceledException) when (_stopToken.IsCancellationRequested) { }
        }

        private async Task DispatchAsync(string script) {
            _stopToken.ThrowIfCancellationRequested();
            try {
                // A queued UI callback may never run after its window has closed.
                await _dispatch(script).WaitAsync(_stopToken);
            }
            catch (OperationCanceledException) when (_stopToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { ArcLog.GetLogger<WebViewScriptExecutor>().Error(ex); }
        }

        private Task DispatchToWebViewAsync(string script) {
            return CrossThreadInvoker.InvokeOnUIThreadAsync(async () => {
                try {
                    _stopToken.ThrowIfCancellationRequested();
                    await EnsureCoreAsync();
                    _stopToken.ThrowIfCancellationRequested();
                    await _webView!.ExecuteScriptAsync(script);
                }
                catch (Exception) when (_stopToken.IsCancellationRequested) { }
            });
        }

        //private void Dispatch(string script) {
        //    CrossThreadInvoker.InvokeOnUIThread(async () => {
        //        try {
        //            await EnsureCoreAsync();
        //            await _webView.ExecuteScriptAsync(script);
        //        }
        //        catch (Exception ex) {
        //            ArcLog.GetLogger<WebViewScriptExecutor>().Error(ex);
        //        }
        //    });
        //}

        private readonly SemaphoreSlim _coreInitLock = new(1, 1);

        private async Task EnsureCoreAsync() {
            if (_coreReady) return;

            await _coreInitLock.WaitAsync(_stopToken);
            try {
                if (_coreReady) return; // double-check
                if (_webView!.CoreWebView2 == null) {
                    await _webView.EnsureCoreWebView2Async();
                }
                _stopToken.ThrowIfCancellationRequested();
                _coreReady = true;
            }
            finally {
                _coreInitLock.Release();
            }
        }

        //private async Task EnsureCoreAsync() {
        //    if (_coreReady) return;

        //    if (_webView.CoreWebView2 == null) {
        //        await _webView.EnsureCoreWebView2Async();
        //    }

        //    _coreReady = true;
        //}

        private static string BuildScript(string functionName, object?[] parameters) {
            var sb = new StringBuilder();
            sb.Append(functionName).Append('(');

            var filtered = parameters.Where(p => p != null).ToArray();
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
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            GC.SuppressFinalize(this);
            _cts.Cancel();
            lock (_queueLock) {
                _stateScripts.Clear();
                while (_eventQueue.TryDequeue(out _)) { }
            }
            _ = ReleaseCancellationAsync();
        }

        private async Task ReleaseCancellationAsync() {
            try { await _completion.ConfigureAwait(false); }
            catch (Exception ex) { ArcLog.GetLogger<WebViewScriptExecutor>().Error(ex); }
            finally {
                _stateSignal.Dispose();
                _eventSignal.Dispose();
                _cts.Dispose();
            }
        }

        // ===== 可调参数 =====
        private static readonly TimeSpan StateInterval = TimeSpan.FromMilliseconds(33); // ~30FPS

        // ===== 内部状态 =====
        private readonly ConcurrentDictionary<string, string> _stateScripts = new();
        private readonly ConcurrentQueue<string> _eventQueue = new();

        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _stateSignal = new(0, 1);
        private readonly SemaphoreSlim _eventSignal = new(0);
        private readonly CancellationToken _stopToken;
        private readonly Task _completion;
        private readonly Func<string, Task> _dispatch;
        private readonly object _queueLock = new();
        private int _disposed;

        private readonly WebView2? _webView;
        private bool _coreReady;
    }
}
