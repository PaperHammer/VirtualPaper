using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using VirtualPaper.Services.Download;

namespace VirtualPaper.SmokeTest;

internal static class DownloadSmoke {
    private static readonly byte[] TestContent1 = "VirtualPaper_smoke_payload_A"u8.ToArray();
    private static readonly byte[] TestContent2 = "VirtualPaper_smoke_payload_BB"u8.ToArray();
    private static readonly byte[] TestContent3 = "VirtualPaper_smoke_payload_CCC"u8.ToArray();

    // ── 单文件下载 ────────────────────────────────────────────────
    public static bool TestSingleDownload() {
        using var server = new HttpSmokeServer(TestContent1);

        var savePath = Path.Combine(Path.GetTempPath(), $"vp_smoke_{Guid.NewGuid():N}.dat");
        try {
            using var service = new DownloadServiceScope();
            ConsumeAsync(service.Value.DownloadAsync(
                new Uri(server.Url), savePath, CancellationToken.None)).GetAwaiter().GetResult();
            var data = File.ReadAllBytes(savePath);

            var sha = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
            var expectedSha = Convert.ToHexString(SHA256.HashData(TestContent1)).ToLowerInvariant();

            bool ok = sha == expectedSha && data.SequenceEqual(TestContent1);
            Console.WriteLine(ok
                ? $"  [OK] Downloaded {data.Length} bytes, SHA256: {sha}"
                : $"  [FAIL] Content mismatch");
            return ok;
        }
        finally {
            try { File.Delete(savePath); } catch { }
        }
    }

    // ── 取消下载 ──────────────────────────────────────────────────
    public static bool TestCancelDownload() {
        // Use a large enough payload so the stream doesn't finish before we cancel.
        // 1 KB padded with zeros guarantees the read loop is still in-flight when cancel fires.
        var bigPayload = new byte[1024];
        Array.Fill(bigPayload, (byte)0xAB);

        using var server = new HttpSmokeServer(bigPayload, chunkDelayMs: 50);

        var savePath = Path.Combine(Path.GetTempPath(), $"vp_smoke_cancel_{Guid.NewGuid():N}.dat");
        try {
            using var cts = new CancellationTokenSource();
            using var service = new DownloadServiceScope();

            var task = Task.Run(async () => {
                await foreach (var progress in service.Value.DownloadAsync(
                    new Uri(server.Url), savePath, cts.Token)) {
                    if (progress.ReceivedBytes > 0) {
                        Console.WriteLine($"  Received {progress.ReceivedBytes} bytes, now cancelling...");
                        cts.Cancel();
                    }
                }
            });

            try {
                task.GetAwaiter().GetResult();
                Console.Error.WriteLine("  [FAIL] Expected OperationCanceledException but task completed normally");
                return false;
            }
            catch (AggregateException ae) when (ae.InnerException is OperationCanceledException) {
                Console.WriteLine("  [OK] Download cancelled correctly");
                return true;
            }
            catch (OperationCanceledException) {
                Console.WriteLine("  [OK] Download cancelled correctly");
                return true;
            }
        }
        finally {
            try { File.Delete(savePath); } catch { }
        }
    }

    // ── 多文件并发下载 ──────────────────────────────────────────────
    public static bool TestMultiDownload() {
        using var server = new HttpSmokeServer(TestContent1, TestContent2, TestContent3);

        var files = new[]
        {
            (server.Urls[0], TestContent1),
            (server.Urls[1], TestContent2),
            (server.Urls[2], TestContent3),
        };
        var destinations = files.Select((_, index) =>
            Path.Combine(Path.GetTempPath(), $"vp_smoke_multi_{index}_{Guid.NewGuid():N}.dat")).ToArray();

        var sw = Stopwatch.StartNew();
        try {
            using var service = new DownloadServiceScope();
            var downloads = files.Select((file, index) =>
                (new Uri(file.Item1), destinations[index]));
            ConsumeAsync(service.Value.DownloadMultipleAsync(downloads, CancellationToken.None))
                .GetAwaiter().GetResult();
            sw.Stop();

            bool allOk = files.Select((file, index) =>
                    File.Exists(destinations[index])
                    && File.ReadAllBytes(destinations[index]).SequenceEqual(file.Item2))
                .All(static value => value);
            Console.WriteLine(allOk
                ? $"  [OK] 3 files through MultiDownloadService in {sw.ElapsedMilliseconds}ms"
                : "  [FAIL] One or more product downloads failed");
            return allOk;
        }
        finally {
            foreach (var destination in destinations) {
                try { File.Delete(destination); } catch { }
            }
        }
    }

    private static async Task ConsumeAsync(IAsyncEnumerable<VirtualPaper.Services.Interfaces.DownloadProgress> source) {
        await foreach (var _ in source) { }
    }

    private sealed class DownloadServiceScope : IDisposable {
        public MultiDownloadService Value { get; } = new();
        public void Dispose() => Value.Dispose();
    }
}

// ── 内嵌 HTTP Server ──────────────────────────────────────────────
internal sealed class HttpSmokeServer : IDisposable {
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts;
    private readonly (string Path, byte[] Content)[] _routes;
    private readonly int _chunkDelayMs;

    public string[] Urls { get; }
    public string Url => Urls[0];

    public HttpSmokeServer(byte[] content, int chunkDelayMs = 0)
        : this(new[] { ("/a.dat", content) }, chunkDelayMs) { }

    public HttpSmokeServer(byte[] c1, byte[] c2, byte[] c3, int chunkDelayMs = 0)
        : this(new[] { ("/a.dat", c1), ("/b.dat", c2), ("/c.dat", c3) }, chunkDelayMs) { }

    private HttpSmokeServer((string Path, byte[] Content)[] routes, int chunkDelayMs) {
        _routes = routes;
        _chunkDelayMs = chunkDelayMs;
        _cts = new CancellationTokenSource();

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Urls = routes.Select(r => $"http://127.0.0.1:{port}{r.Path}").ToArray();

        Task.Run(() => ServeLoop(_cts.Token));
    }

    private async Task ServeLoop(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            try {
                var client = await _listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleRequest(client, token), token);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) when (token.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) { break; }
        }
    }

    private async Task HandleRequest(TcpClient client, CancellationToken token) {
        using (client)
        try {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                stream,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            string? requestLine = await reader.ReadLineAsync(token);
            string requestPath = requestLine?.Split(' ').ElementAtOrDefault(1) ?? "/";
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(token))) { }

            var route = _routes.FirstOrDefault(r =>
                requestPath.EndsWith(r.Path, StringComparison.Ordinal));
            var content = route.Content ?? _routes[0].Content;
            byte[] headers = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\n" +
                $"Content-Length: {content.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(headers, token);

            if (_chunkDelayMs > 0) {
                // Simulate slow download by sending in chunks
                int chunkSize = Math.Max(4, content.Length / 10);
                for (int offset = 0; offset < content.Length; offset += chunkSize) {
                    await Task.Delay(_chunkDelayMs, token);
                    int len = Math.Min(chunkSize, content.Length - offset);
                    await stream.WriteAsync(content.AsMemory(offset, len), token);
                    await stream.FlushAsync(token);
                }
            }
            else {
                await stream.WriteAsync(content, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (SocketException) { }
    }

    public void Dispose() {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        _cts.Dispose();
    }
}
