using VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Server;

namespace WebBackdrop.Test.T_PlayerWeb;

[TestClass]
[DoNotParallelize]
public class PreviewServerTests {
    private string _root = null!;

    [TestInitialize]
    public void Initialize() {
        _root = Path.Combine(Path.GetTempPath(), $"vp_preview_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup() {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task StartAsync_ServesProjectFilesAndStopIsIdempotent() {
        const string expected = "<html><body>preview</body></html>";
        await File.WriteAllTextAsync(Path.Combine(_root, "index.html"), expected);
        var server = new PreviewServer();

        try {
            await server.StartAsync(_root);
            using var client = new HttpClient();

            var actual = await client.GetStringAsync(server.GetUrl("index.html"));

            Assert.AreEqual(expected, actual);
            Assert.IsTrue(server.IsRunning);
            Assert.IsGreaterThan(0, server.Port);
        }
        finally {
            await server.StopAsync();
            await server.StopAsync();
        }

        Assert.IsFalse(server.IsRunning);
    }

    [TestMethod]
    public void GetUrl_NormalizesDirectorySeparators() {
        var server = new PreviewServer();

        var url = server.GetUrl("assets\\index.html");

        Assert.AreEqual("http://127.0.0.1:0/assets/index.html", url);
    }

    [TestMethod]
    public async Task StartAsync_WithAllowedOrigin_AddsCorsHeaderToStaticResources() {
        await File.WriteAllBytesAsync(Path.Combine(_root, "texture.png"), [1, 2, 3]);
        const string shellOrigin = "http://127.0.0.1:43210";
        var server = new PreviewServer();

        try {
            await server.StartAsync(_root, new PreviewServerOptions {
                AllowedOrigin = shellOrigin
            });
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, server.GetUrl("texture.png"));
            request.Headers.Add("Origin", shellOrigin);

            using var response = await client.SendAsync(request);

            response.EnsureSuccessStatusCode();
            Assert.AreEqual(
                shellOrigin,
                response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        }
        finally {
            await server.StopAsync();
        }
    }

}
