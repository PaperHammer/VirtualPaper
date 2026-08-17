using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Workloads.Creation.WebBackdrop.Core.Utils {
    public sealed partial class LocalPreviewServer : IDisposable {
        public LocalPreviewServer(string projectRoot) {
            _projectRoot = Path.GetFullPath(projectRoot);
        }

        public Task<string> StartAsync(string entryFilePath, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGetProjectRelativePath(entryFilePath, out _)) {
                throw new ArgumentException("The entry file must be inside the project directory.", nameof(entryFilePath));
            }

            if (_listener != null && _listener.IsListening)
                return Task.FromResult(CreateShellUrl(entryFilePath));

            var port = GetAvailablePort();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _port = port;

            _watcher = new FileSystemWatcher(_projectRoot) {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;

            _ = AcceptAsync(_listener, CancellationToken.None);
            return Task.FromResult(CreateShellUrl(entryFilePath));
        }

        public void Stop() {
            var listener = _listener;
            _listener = null;
            _watcher?.Dispose();
            _watcher = null;
            lock (_changeLock) {
                _changeDebounceCancellation?.Cancel();
                _changeDebounceCancellation?.Dispose();
                _changeDebounceCancellation = null;
            }

            foreach (var socket in _sockets.Values) {
                try { socket.Abort(); } catch { }
            }
            _sockets.Clear();

            if (listener != null) {
                try { listener.Stop(); } catch { }
                listener.Close();
            }
        }

        public void Dispose() {
            Stop();
            GC.SuppressFinalize(this);
        }

        private async Task AcceptAsync(HttpListener listener, CancellationToken cancellationToken) {
            while (!cancellationToken.IsCancellationRequested && listener.IsListening) {
                try {
                    var context = await listener.GetContextAsync();
                    _ = HandleAsync(context, cancellationToken);
                }
                catch (HttpListenerException) when (!listener.IsListening) { }
                catch (ObjectDisposedException) { }
            }
        }

        private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken) {
            try {
                // 预览入口：返回包裹实际页面的壳页，用于接收刷新消息并重载 iframe。
                if (context.Request.Url?.AbsolutePath == "/__preview_shell") {
                    var entry = context.Request.QueryString["entry"];
                    if (entry == null || GetFilePath("/" + entry) == null) {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        context.Response.Close();
                        return;
                    }
                    await WriteTextAsync(context.Response, CreateShellHtml(entry));
                    return;
                }

                // 热更新通道：浏览器通过 WebSocket 连接，等待文件变更通知。
                if (context.Request.Url?.AbsolutePath == "/__preview_events") {
                    await HandleWebSocketAsync(context, cancellationToken);
                    return;
                }

                // 其余请求映射到项目根目录下的静态文件；GetFilePath 会拦截目录穿越。
                var filePath = GetFilePath(context.Request.Url?.AbsolutePath ?? "/");
                if (filePath == null || !File.Exists(filePath)) {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    return;
                }

                // HTML 响应注入热更新客户端，使文件变更可刷新页面或样式。
                if (Path.GetExtension(filePath).Equals(".html", StringComparison.OrdinalIgnoreCase)) {
                    var html = await File.ReadAllTextAsync(filePath, cancellationToken);
                    await WriteTextAsync(context.Response, InjectRefreshClient(html), "text/html; charset=utf-8");
                    return;
                }

                // 非 HTML 文件按类型原样返回，例如 CSS、JavaScript、图片和字体。
                context.Response.ContentType = GetContentType(filePath);
                await using var stream = File.OpenRead(filePath);
                context.Response.ContentLength64 = stream.Length;
                await stream.CopyToAsync(context.Response.OutputStream, cancellationToken);
                context.Response.Close();
            }
            catch (OperationCanceledException) {
                // 请求取消时中断连接，避免继续向已关闭的客户端写入数据。
                TryAbort(context.Response);
            }
            catch {
                // 未处理异常统一返回 500，避免监听循环因单个请求中断。
                // 连接可能已中断，Close 本身也可能抛异常，做兜底防御。
                try {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.Close();
                }
                catch { /* 连接已中断，忽略 */ }
            }
        }

        private static void TryAbort(HttpListenerResponse response) {
            try {
                response.Abort();
            }
            catch { /* 连接已中断，忽略 */ }
        }

        private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken cancellationToken) {
            if (!context.Request.IsWebSocketRequest) {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.Close();
                return;
            }

            var socketContext = await context.AcceptWebSocketAsync(null);
            var id = Guid.NewGuid();
            _sockets[id] = socketContext.WebSocket;
            var buffer = new byte[1];
            try {
                while (socketContext.WebSocket.State == WebSocketState.Open)
                    await socketContext.WebSocket.ReceiveAsync(buffer, cancellationToken);
            }
            catch (WebSocketException) { }
            catch (OperationCanceledException) { }
            finally {
                _sockets.TryRemove(id, out _);
                try { socketContext.WebSocket.Dispose(); }
                catch { /* 忽略释放异常 */ }
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e) => ScheduleBroadcast(e.FullPath);
        private void OnFileRenamed(object sender, RenamedEventArgs e) => ScheduleBroadcast(e.FullPath);

        private void ScheduleBroadcast(string path) {
            var refreshKind = Path.GetExtension(path).Equals(".css", StringComparison.OrdinalIgnoreCase)
                ? PreviewRefreshKind.CssOnly
                : PreviewRefreshKind.Reload;
            CancellationToken token;
            lock (_changeLock) {
                _pendingRefreshKind = (PreviewRefreshKind)Math.Max((int)_pendingRefreshKind, (int)refreshKind);
                _changeDebounceCancellation?.Cancel();
                _changeDebounceCancellation?.Dispose();
                _changeDebounceCancellation = new CancellationTokenSource();
                token = _changeDebounceCancellation.Token;
            }
            _ = BroadcastPendingChangeAsync(token);
        }

        private async Task BroadcastPendingChangeAsync(CancellationToken cancellationToken) {
            try {
                await Task.Delay(150, cancellationToken);
                PreviewRefreshKind refreshKind;
                lock (_changeLock) {
                    refreshKind = _pendingRefreshKind;
                    _pendingRefreshKind = PreviewRefreshKind.CssOnly;
                }
                BroadcastChange(refreshKind);
            }
            catch (OperationCanceledException) { }
        }

        private void BroadcastChange(PreviewRefreshKind refreshKind) {
            var message = refreshKind == PreviewRefreshKind.CssOnly ? "css" : "reload";
            var data = Encoding.UTF8.GetBytes(message);
            foreach (var pair in _sockets) {
                if (pair.Value.State != WebSocketState.Open)
                    continue;
                _ = SendSafelyAsync(pair.Value, data);
            }
        }

        /// <summary>
        /// 发送并观察异常：客户端断开时 SendAsync 会抛异常，避免产生未观察的 Task 异常。
        /// </summary>
        private static async Task SendSafelyAsync(WebSocket socket, byte[] data) {
            try {
                await socket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (WebSocketException) { }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }
        }

        private string CreateShellUrl(string entryFilePath) {
            if (!TryGetProjectRelativePath(entryFilePath, out var relativePath)) {
                throw new ArgumentException("The entry file must be inside the project directory.", nameof(entryFilePath));
            }
            return $"http://127.0.0.1:{_port}/__preview_shell?entry={Uri.EscapeDataString(relativePath)}";
        }

        private bool TryGetProjectRelativePath(string path, out string relativePath) {
            relativePath = Path.GetRelativePath(_projectRoot, Path.GetFullPath(path));
            if (relativePath == ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relativePath)) {
                return false;
            }

            relativePath = relativePath.Replace('\\', '/');
            return true;
        }

        private string? GetFilePath(string requestPath) {
            string relativePath;
            try {
                relativePath = Uri.UnescapeDataString(requestPath.TrimStart('/')).Replace('/', Path.DirectorySeparatorChar);
            }
            catch (UriFormatException) {
                return null;
            }
            var path = Path.GetFullPath(Path.Combine(_projectRoot, relativePath));
            return TryGetProjectRelativePath(path, out _) ? path : null;
        }

        private static async Task WriteTextAsync(HttpListenerResponse response, string text, string contentType = "text/html; charset=utf-8") {
            var data = Encoding.UTF8.GetBytes(text);
            response.ContentType = contentType;
            response.ContentLength64 = data.Length;
            await response.OutputStream.WriteAsync(data);
            response.Close();
        }

        private static string InjectRefreshClient(string html) {
            const string script = "<script>(() => { const ws = new WebSocket(`${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}/__preview_events`); ws.onmessage = e => { if (e.data === 'css') { document.querySelectorAll('link[rel=\\\"stylesheet\\\"]').forEach(link => { const url = new URL(link.href); url.searchParams.set('__preview', Date.now()); link.href = url; }); } else { window.parent.postMessage('__preview_reload', '*'); } }; })();</script>";
            return html.Contains("</body>", StringComparison.OrdinalIgnoreCase)
                ? html.Replace("</body>", script + "</body>", StringComparison.OrdinalIgnoreCase)
                : html + script;
        }

        private static string CreateShellHtml(string entry) {
            var escapedEntry = WebUtility.HtmlEncode(entry);
            return $"<!doctype html><html><head><meta charset=\"utf-8\"><style>html,body,iframe{{width:100%;height:100%;margin:0;border:0;background:#111}}iframe{{opacity:1;transition:opacity .18s ease}}</style></head><body><iframe id=\"preview\" src=\"/{escapedEntry}\"></iframe><script>const frame=document.getElementById('preview');window.addEventListener('message',e=>{{if(e.data!=='__preview_reload')return;frame.style.opacity='0';setTimeout(()=>{{const u=new URL(frame.src);u.searchParams.set('__preview',Date.now());frame.onload=()=>frame.style.opacity='1';frame.src=u}},180)}});</script></body></html>";
        }

        private static int GetAvailablePort() {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        private static string GetContentType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch {
            ".css" => "text/css; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream",
        };

        private enum PreviewRefreshKind {
            CssOnly,
            Reload,
        }

        private readonly string _projectRoot;
        private readonly ConcurrentDictionary<Guid, WebSocket> _sockets = new();
        private readonly object _changeLock = new();
        private HttpListener? _listener;
        private CancellationTokenSource? _changeDebounceCancellation;
        private PreviewRefreshKind _pendingRefreshKind;
        private int _port;
        private FileSystemWatcher? _watcher;
    }
}
