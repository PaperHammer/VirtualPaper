using System.Text.Json.Serialization;
using VirtualPaper.Common.Utils.Storage;
using static VirtualPaper.Common.Errors;

namespace VirtualPaper.Core.Test.T_Common {
    // ── 测试用数据类型 ────────────────────────────────────────────────

    public record TestSettings {
        public string Name { get; set; } = "";
        public int Value { get; set; }
        public List<string> Items { get; set; } = new();
    }

    [JsonSerializable(typeof(TestSettings))]
    public partial class TestSettingsContext : JsonSerializerContext { }

    [TestClass]
    public class JsonSaverTests {
        private string _tempDir = null!;

        [TestInitialize]
        public void Setup() {
            _tempDir = Path.Combine(Path.GetTempPath(), $"JsonSaverTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup() {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        // ── SaveAsync / LoadAsync 往返 ────────────────────────────────

        [TestMethod]
        public async Task SaveLoad_Roundtrip_PreservesData() {
            var original = new TestSettings {
                Name = "test",
                Value = 42,
                Items = new List<string> { "a", "b", "c" },
            };
            var path = Path.Combine(_tempDir, "settings.json");

            await JsonSaver.SaveAsync(path, original, TestSettingsContext.Default);
            var loaded = await JsonSaver.LoadAsync<TestSettings>(path, TestSettingsContext.Default);

            Assert.AreEqual(original.Name, loaded.Name);
            Assert.AreEqual(original.Value, loaded.Value);
            CollectionAssert.AreEqual(original.Items, loaded.Items);
        }

        [TestMethod]
        public async Task SaveLoad_EmptyList_Roundtrips() {
            var original = new TestSettings { Name = "empty", Items = new() };
            var path = Path.Combine(_tempDir, "empty.json");

            await JsonSaver.SaveAsync(path, original, TestSettingsContext.Default);
            var loaded = await JsonSaver.LoadAsync<TestSettings>(path, TestSettingsContext.Default);

            Assert.IsEmpty(loaded.Items);
        }

        [TestMethod]
        public async Task SaveLoad_DefaultValues_Roundtrips() {
            var original = new TestSettings();
            var path = Path.Combine(_tempDir, "default.json");

            await JsonSaver.SaveAsync(path, original, TestSettingsContext.Default);
            var loaded = await JsonSaver.LoadAsync<TestSettings>(path, TestSettingsContext.Default);

            Assert.AreEqual("", loaded.Name);
            Assert.AreEqual(0, loaded.Value);
        }

        // ── 容错：注释 ────────────────────────────────────────────────

        [TestMethod]
        public async Task Load_WithComments_Succeeds() {
            var path = Path.Combine(_tempDir, "commented.json");
            File.WriteAllText(path, """
            {
                // 这是注释
                "Name": "test",
                "Value": 1 /* 内联注释 */,
                "Items": []
            }
            """);

            var loaded = await JsonSaver.LoadAsync<TestSettings>(path, TestSettingsContext.Default);

            Assert.AreEqual("test", loaded.Name);
            Assert.AreEqual(1, loaded.Value);
        }

        // ── 容错：尾逗号 ──────────────────────────────────────────────

        [TestMethod]
        public async Task Load_WithTrailingComma_Succeeds() {
            var path = Path.Combine(_tempDir, "trailing.json");
            File.WriteAllText(path, """
            {
                "Name": "test",
                "Value": 1,
                "Items": ["a", "b",],
            }
            """);

            var loaded = await JsonSaver.LoadAsync<TestSettings>(path, TestSettingsContext.Default);

            Assert.AreEqual("test", loaded.Name);
            Assert.HasCount(2, loaded.Items);
        }

        // ── 容错：大小写不敏感 ────────────────────────────────────────

        [TestMethod]
        public async Task Load_CaseInsensitiveProperties_Succeeds() {
            var path = Path.Combine(_tempDir, "case.json");
            File.WriteAllText(path, """
            {
                "name": "test",
                "VALUE": 99,
                "items": ["x"]
            }
            """);

            var loaded = await JsonSaver.LoadAsync<TestSettings>(path, TestSettingsContext.Default);

            Assert.AreEqual("test", loaded.Name);
            Assert.AreEqual(99, loaded.Value);
        }

        // ── 写入格式 ─────────────────────────────────────────────────

        [TestMethod]
        public async Task Save_WritesIndentedJson() {
            var data = new TestSettings { Name = "test" };
            var path = Path.Combine(_tempDir, "indented.json");

            await JsonSaver.SaveAsync(path, data, TestSettingsContext.Default);

            var content = File.ReadAllText(path);
            // WriteIndented = true → 包含换行和缩进
            Assert.IsTrue(content.Contains('\n') || content.Contains("\r\n"));
        }

        // ── 错误处理 ─────────────────────────────────────────────────

        [TestMethod]
        public async Task Load_NonExistentFile_ThrowsFileAccessException() {
            var path = Path.Combine(_tempDir, "nonexistent——11.json");

            await Assert.ThrowsAsync<FileAccessException>(
                () => JsonSaver.LoadAsync<TestSettings>(path, TestSettingsContext.Default));
        }

        // ── 自动创建目录 ─────────────────────────────────────────────

        [TestMethod]
        public async Task Save_CreatesDirectoryIfNotExists() {
            var subDir = Path.Combine(_tempDir, "sub", "deep");
            var path = Path.Combine(subDir, "settings.json");
            var data = new TestSettings { Name = "test" };

            await JsonSaver.SaveAsync(path, data, TestSettingsContext.Default);

            Assert.IsTrue(File.Exists(path));
        }

        // ── 同步方法 ─────────────────────────────────────────────────

        [TestMethod]
        public void SaveLoad_Sync_Roundtrips() {
            var original = new TestSettings { Name = "sync", Value = 7 };
            var path = Path.Combine(_tempDir, "sync.json");

            JsonSaver.Save(path, original, TestSettingsContext.Default);
            var loaded = JsonSaver.Load<TestSettings>(path, TestSettingsContext.Default);

            Assert.AreEqual("sync", loaded.Name);
            Assert.AreEqual(7, loaded.Value);
        }
    }
}
