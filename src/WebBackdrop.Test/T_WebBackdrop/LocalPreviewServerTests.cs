using Workloads.Creation.WebBackdrop.Core.Utils;

namespace WebBackdrop.Test.T_WebBackdrop {
    [TestClass]
    public class LocalPreviewServerTests {
        [TestInitialize]
        public void Initialize() {
            _projectRoot = Path.Combine(
                Path.GetTempPath(),
                "VirtualPaper.Tests",
                $"Preview-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_projectRoot);
            File.WriteAllText(
                Path.Combine(_projectRoot, "index.html"),
                "<!doctype html><html><body>Hello</body></html>");
            File.WriteAllText(Path.Combine(_projectRoot, "site.css"), "body { color: red; }");
        }

        [TestCleanup]
        public void Cleanup() {
            if (Directory.Exists(_projectRoot)) Directory.Delete(_projectRoot, recursive: true);
        }

        [TestMethod]
        public void FilePathMapping_ResolvesProjectFileAndRejectsTraversal() {
            using var server = new LocalPreviewServer(_projectRoot);

            Assert.AreEqual(
                Path.Combine(_projectRoot, "index.html"),
                server.GetFilePath("/index.html"));
            Assert.IsNull(server.GetFilePath("/%2e%2e%2foutside.txt"));
            Assert.IsNull(server.GetFilePath("/../outside.txt"));
        }

        [TestMethod]
        public async Task StartAsync_RejectsEntryOutsideProject() {
            using var server = new LocalPreviewServer(_projectRoot);
            string outside = Path.Combine(Path.GetDirectoryName(_projectRoot)!, "outside.html");

            await Assert.ThrowsAsync<ArgumentException>(() => server.StartAsync(outside));
        }

        [TestMethod]
        public void InjectRefreshClient_InsertsScriptBeforeBodyEnd() {
            const string html = "<!doctype html><html><body>Hello</body></html>";

            string result = LocalPreviewServer.InjectRefreshClient(html);

            StringAssert.Contains(result, "/__preview_events");
            StringAssert.Contains(result, "</script></body>");
            Assert.AreEqual(1, CountOccurrences(result, "/__preview_events"));
        }

        [TestMethod]
        public void GetContentType_MapsKnownAndUnknownExtensions() {
            Assert.AreEqual("text/css; charset=utf-8", LocalPreviewServer.GetContentType("site.CSS"));
            Assert.AreEqual("text/javascript; charset=utf-8", LocalPreviewServer.GetContentType("app.js"));
            Assert.AreEqual("image/svg+xml", LocalPreviewServer.GetContentType("icon.svg"));
            Assert.AreEqual("application/octet-stream", LocalPreviewServer.GetContentType("data.bin"));
        }

        private static int CountOccurrences(string value, string text) =>
            (value.Length - value.Replace(text, string.Empty, StringComparison.Ordinal).Length) / text.Length;

        private string _projectRoot = null!;
    }
}
