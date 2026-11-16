using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VirtualPaper.Core.Test.Infrastructure;

// Real loopback HTTP, with no URL reservation, external network or shared port.
internal sealed class DownloadTestServer : IAsyncDisposable {
    internal sealed record Response(byte[] Bytes, int Status = 200, int DelayPerChunk = 0);
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentBag<Task> _connections = [];
    private readonly Func<string, Response> _respond;
    private readonly Task _accept;
    public ConcurrentQueue<string> Requests { get; } = [];
    public Uri Address { get; }

    public DownloadTestServer(Func<string, Response> respond) {
        _respond = respond;
        _listener.Start();
        Address = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
        _accept = AcceptAsync();
    }

    private async Task AcceptAsync() {
        try {
            while (!_stop.IsCancellationRequested) {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                _connections.Add(ServeAsync(client));
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (SocketException) when (_stop.IsCancellationRequested) { }
    }

    private async Task ServeAsync(TcpClient client) {
        using (client) {
            try {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                string? first = await reader.ReadLineAsync(_stop.Token);
                if (first == null) return;
                string[] request = first.Split(' ');
                string? range = null;
                while (await reader.ReadLineAsync(_stop.Token) is { Length: > 0 } header) {
                    if (header.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)) range = header[6..].Trim();
                }
                Requests.Enqueue(request[1]);
                var response = _respond(new Uri(Address, request[1]).AbsolutePath);
                int start = 0, end = response.Bytes.Length - 1;
                bool partial = response.Status == 200 && range?.StartsWith("bytes=") == true;
                if (partial) {
                    var parts = range![6..].Split('-');
                    start = int.Parse(parts[0]);
                    if (parts.Length > 1 && parts[1].Length > 0) end = Math.Min(end, int.Parse(parts[1]));
                }
                int count = Math.Max(0, end - start + 1);
                string status = partial ? "206 Partial Content" : response.Status == 200 ? "200 OK" : "404 Not Found";
                string headers = $"HTTP/1.1 {status}\r\nContent-Length: {count}\r\nAccept-Ranges: bytes\r\nConnection: close\r\n";
                if (partial) headers += $"Content-Range: bytes {start}-{end}/{response.Bytes.Length}\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(headers + "\r\n"), _stop.Token);
                if (request[0] == "HEAD") return;
                for (int offset = start; offset <= end; offset += 4096) {
                    await stream.WriteAsync(response.Bytes.AsMemory(offset, Math.Min(4096, end - offset + 1)), _stop.Token);
                    if (response.DelayPerChunk > 0) await Task.Delay(response.DelayPerChunk, _stop.Token);
                }
            }
            // A cancelled/failed downloader closes its connection before the response ends.
            catch (IOException) { }
            catch (SocketException) { }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }
    }

    public async ValueTask DisposeAsync() {
        _stop.Cancel();
        _listener.Stop();
        await _accept;
        await Task.WhenAll(_connections);
        _stop.Dispose();
    }
}
