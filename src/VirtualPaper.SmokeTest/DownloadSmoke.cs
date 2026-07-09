using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

namespace VirtualPaper.SmokeTest;

internal static class DownloadSmoke
{
    private static readonly byte[] TestContent1 = "VirtualPaper_smoke_payload_A"u8.ToArray();
    private static readonly byte[] TestContent2 = "VirtualPaper_smoke_payload_BB"u8.ToArray();
    private static readonly byte[] TestContent3 = "VirtualPaper_smoke_payload_CCC"u8.ToArray();

    // ── 单文件下载 ────────────────────────────────────────────────
    public static bool TestSingleDownload()
    {
        using var server = new HttpSmokeServer(TestContent1);

        var savePath = Path.Combine(Path.GetTempPath(), $"vp_smoke_{Guid.NewGuid():N}.dat");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var data = client.GetByteArrayAsync(server.Url).GetAwaiter().GetResult();

            File.WriteAllBytes(savePath, data);

            var sha = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
            var expectedSha = Convert.ToHexString(SHA256.HashData(TestContent1)).ToLowerInvariant();

            bool ok = sha == expectedSha && data.SequenceEqual(TestContent1);
            Console.WriteLine(ok
                ? $"  [OK] Downloaded {data.Length} bytes, SHA256: {sha}"
                : $"  [FAIL] Content mismatch");
            return ok;
        }
        finally
        {
            try { File.Delete(savePath); } catch { }
        }
    }

    // ── 取消下载 ──────────────────────────────────────────────────
    public static bool TestCancelDownload()
    {
        using var server = new HttpSmokeServer(TestContent1, chunkDelayMs: 200);

        var savePath = Path.Combine(Path.GetTempPath(), $"vp_smoke_cancel_{Guid.NewGuid():N}.dat");
        try
        {
            using var cts = new CancellationTokenSource();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

            var task = Task.Run(async () =>
            {
                using var resp = await client.GetAsync(server.Url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
                await using var fs = File.Create(savePath);

                // Download 2 chunks then cancel
                var buf = new byte[4];
                int total = 0;
                for (int i = 0; i < 2 && total < TestContent1.Length; i++)
                {
                    int read = stream.Read(buf, 0, buf.Length);
                    if (read <= 0) break;
                    fs.Write(buf, 0, read);
                    total += read;
                    Console.WriteLine($"  Chunk {i + 1}: {total} bytes");
                    Thread.Sleep(100);
                }

                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();
            }, cts.Token);

            try
            {
                task.GetAwaiter().GetResult();
                Console.Error.WriteLine("  [FAIL] Expected OperationCanceledException");
                return false;
            }
            catch (AggregateException ae) when (ae.InnerException is OperationCanceledException)
            {
                Console.WriteLine("  [OK] Download cancelled correctly");
                return true;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("  [OK] Download cancelled correctly");
                return true;
            }
        }
        finally
        {
            try { File.Delete(savePath); } catch { }
        }
    }

    // ── 多文件并发下载 ──────────────────────────────────────────────
    public static bool TestMultiDownload()
    {
        using var server = new HttpSmokeServer(TestContent1, TestContent2, TestContent3);

        var files = new[]
        {
            (server.Urls[0], TestContent1),
            (server.Urls[1], TestContent2),
            (server.Urls[2], TestContent3),
        };

        var sw = Stopwatch.StartNew();
        var tasks = files.Select(f => DownloadAndVerifyAsync(f.Item1, f.Item2)).ToArray();
        Task.WaitAll(tasks, 20000);
        sw.Stop();

        bool allOk = tasks.All(t => t.Result);
        Console.WriteLine(allOk
            ? $"  [OK] 3 files concurrently in {sw.ElapsedMilliseconds}ms"
            : "  [FAIL] One or more downloads failed");
        return allOk;
    }

    private static async Task<bool> DownloadAndVerifyAsync(string url, byte[] expected)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var data = await client.GetByteArrayAsync(url);
        bool ok = data.SequenceEqual(expected);
        Console.WriteLine($"  {url.Split('/').Last()}: {data.Length}B {(ok ? "OK" : "MISMATCH")}");
        return ok;
    }
}

// ── 内嵌 HTTP Server ──────────────────────────────────────────────
internal sealed class HttpSmokeServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts;
    private readonly (string Path, byte[] Content)[] _routes;
    private readonly int _chunkDelayMs;

    public string[] Urls { get; }
    public string Url => Urls[0];

    public HttpSmokeServer(byte[] content, int chunkDelayMs = 0)
        : this(new[] { ("/a.dat", content) }, chunkDelayMs) { }

    public HttpSmokeServer(byte[] c1, byte[] c2, byte[] c3, int chunkDelayMs = 0)
        : this(new[] { ("/a.dat", c1), ("/b.dat", c2), ("/c.dat", c3) }, chunkDelayMs) { }

    private HttpSmokeServer((string Path, byte[] Content)[] routes, int chunkDelayMs)
    {
        _routes = routes;
        _chunkDelayMs = chunkDelayMs;
        _cts = new CancellationTokenSource();

        var port = GetFreePort();
        Urls = routes.Select(r => $"http://127.0.0.1:{port}{r.Path}").ToArray();

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();

        Task.Run(() => ServeLoop(_cts.Token));
    }

    private async Task ServeLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener.GetContextAsync().WaitAsync(token);
                _ = Task.Run(() => HandleRequest(ctx), token);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            var route = _routes.FirstOrDefault(r =>
                ctx.Request.Url!.AbsolutePath.EndsWith(r.Path.Split('/').Last()));
            var content = route.Content ?? _routes[0].Content;

            ctx.Response.ContentType = "application/octet-stream";
            ctx.Response.ContentLength64 = content.Length;

            if (_chunkDelayMs > 0)
            {
                // Simulate slow download by sending in chunks
                int chunkSize = Math.Max(4, content.Length / 10);
                for (int offset = 0; offset < content.Length; offset += chunkSize)
                {
                    await Task.Delay(_chunkDelayMs);
                    int len = Math.Min(chunkSize, content.Length - offset);
                    await ctx.Response.OutputStream.WriteAsync(content, offset, len);
                }
            }
            else
            {
                await ctx.Response.OutputStream.WriteAsync(content);
            }

            ctx.Response.OutputStream.Close();
        }
        catch { }
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        _listener.Close();
    }
}
