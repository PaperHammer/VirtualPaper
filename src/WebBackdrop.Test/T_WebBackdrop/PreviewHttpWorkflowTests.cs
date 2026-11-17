using System.Net;
using System.Net.WebSockets;
using System.Text;
using Workloads.Creation.WebBackdrop.Core.Utils;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
public class PreviewHttpWorkflowTests {
    private string _root = null!;
    private string Entry => Path.Combine(_root, "index.html");
    [TestInitialize]
    public void Initialize() {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.PreviewHttp-" + Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Entry, "<html><body>owned preview content</body></html>");
        File.WriteAllText(Path.Combine(_root, "style.css"), "body { color: red; }");
        File.WriteAllBytes(Path.Combine(_root, "asset.bin"), [1, 2, 3, 0, 255]);
    }
    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public async Task RealHttp_ServesShellHtmlAndBinaryFiles_AndRejectsMissingOrInvalidRequests() {
        using var server = new LocalPreviewServer(_root);
        string shell = await server.StartAsync(Entry);
        using var client = Client(shell);
        StringAssert.Contains(await client.GetStringAsync(shell), "__preview_reload");
        StringAssert.Contains(await client.GetStringAsync("index.html"), "owned preview content");
        StringAssert.Contains(await client.GetStringAsync("index.html"), "__preview_events");
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 0, 255 }, await client.GetByteArrayAsync("asset.bin"));
        using var css = await client.GetAsync("style.css");
        Assert.AreEqual("text/css", css.Content.Headers.ContentType!.MediaType);
        Assert.AreEqual("body { color: red; }", await css.Content.ReadAsStringAsync());
        foreach (string path in new[] { "missing.html", "__preview_events", "__preview_shell" }) {
            using var response = await client.GetAsync(path);
            Assert.AreEqual(path == "missing.html" ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.AreEqual(shell, await server.StartAsync(Entry), "Starting twice must reuse the active server.");
    }

    [TestMethod]
    public async Task FailedRequest_DoesNotStopAcceptingRequests_AndStopAllowsRestart() {
        using var server = new LocalPreviewServer(_root);
        string shell = await server.StartAsync(Entry);
        using var client = Client(shell);
        using (var locked = new FileStream(Entry, FileMode.Open, FileAccess.Read, FileShare.None)) {
            using var response = await client.GetAsync("index.html");
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        StringAssert.Contains(await client.GetStringAsync("index.html"), "owned preview content");
        server.Stop(); server.Stop();
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStringAsync("index.html"));
        string reopened = await server.StartAsync(Entry);
        using var restarted = Client(reopened);
        StringAssert.Contains(await restarted.GetStringAsync("index.html"), "owned preview content");
    }

    [TestMethod]
    public async Task FileChanges_NotifyRealWebSocketClients_AndStopClosesConnections() {
        using var server = new LocalPreviewServer(_root);
        var shell = new Uri(await server.StartAsync(Entry));
        using var socket = new ClientWebSocket();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await socket.ConnectAsync(new UriBuilder(shell) { Scheme = "ws", Path = "/__preview_events", Query = "" }.Uri, timeout.Token);
        File.WriteAllText(Path.Combine(_root, "style.css"), "body { color: blue; }");
        Assert.AreEqual("css", await ReceiveAsync(socket, timeout.Token));
        File.WriteAllText(Entry, "<html><body>changed</body></html>");
        File.WriteAllText(Path.Combine(_root, "style.css"), "body { color: green; }");
        Assert.AreEqual("reload", await ReceiveAsync(socket, timeout.Token));
        server.Stop();
        try {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[64]), timeout.Token);
            Assert.AreEqual(WebSocketMessageType.Close, result.MessageType);
        }
        catch (WebSocketException) { } // Abort is the server's documented disposal path.
    }

    private static HttpClient Client(string shell) => new() {
        BaseAddress = new Uri(new Uri(shell).GetLeftPart(UriPartial.Authority) + "/"), Timeout = TimeSpan.FromSeconds(5)
    };
    private static async Task<string> ReceiveAsync(ClientWebSocket socket, CancellationToken token) {
        byte[] data = new byte[256];
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(data), token);
        Assert.AreEqual(WebSocketMessageType.Text, result.MessageType); Assert.IsTrue(result.EndOfMessage);
        return Encoding.UTF8.GetString(data, 0, result.Count);
    }
}
