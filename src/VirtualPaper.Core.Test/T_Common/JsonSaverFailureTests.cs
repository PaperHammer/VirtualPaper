using System.Text.Json.Serialization;
using VirtualPaper.Common.Utils.Storage;
using static VirtualPaper.Common.Errors;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class JsonSaverFailureTests {
    [TestInitialize]
    public void Initialize() => _root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "VirtualPaper.JsonWrite", Guid.NewGuid().ToString("N"))).FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SerializationFailure_PreservesExistingFileAndAllowsRetry(bool asynchronous) {
        string path = Path.Combine(_root, "settings.json");
        await SaveAsync(path, new JsonWriteDocument { Name = "Original" }, asynchronous);
        byte[] before = await File.ReadAllBytesAsync(path);

        var error = await Assert.ThrowsExactlyAsync<FileAccessException>(() =>
            SaveAsync(path, new JsonWriteDocument { Name = "Incomplete", FailOnSerialization = true }, asynchronous));

        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path));
        Assert.IsInstanceOfType<InvalidOperationException>(error.InnerException);
        Assert.AreEqual("write json", error.Operation);
        Assert.AreEqual(path, error.FilePath);
        Assert.HasCount(1, Directory.GetFileSystemEntries(_root));
        Assert.AreEqual("Original", JsonSaver.Load<JsonWriteDocument>(path, JsonWriteContext.Default).Name);

        await SaveAsync(path, new JsonWriteDocument { Name = "Recovered", Content = "complete" }, asynchronous);
        var recovered = await JsonSaver.LoadAsync<JsonWriteDocument>(path, JsonWriteContext.Default);
        Assert.IsNotNull(recovered);
        Assert.AreEqual("Recovered", recovered.Name);
        Assert.AreEqual("complete", recovered.Content);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FirstSaveFailure_DoesNotPublishPartialFileAndAllowsRetry(bool asynchronous) {
        string path = Path.Combine(_root, "settings.json");

        var error = await Assert.ThrowsExactlyAsync<FileAccessException>(() =>
            SaveAsync(path, new JsonWriteDocument { FailOnSerialization = true }, asynchronous));

        Assert.IsFalse(File.Exists(path));
        Assert.IsEmpty(Directory.GetFileSystemEntries(_root));
        Assert.AreEqual("write json", error.Operation);
        await SaveAsync(path, new JsonWriteDocument { Name = "Recovered" }, asynchronous);
        Assert.AreEqual("Recovered", JsonSaver.Load<JsonWriteDocument>(path, JsonWriteContext.Default).Name);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReplacementDenied_PreservesExistingFileAndRemovesTemporaryOutput(bool asynchronous) {
        string path = Path.Combine(_root, "settings.json");
        await SaveAsync(path, new JsonWriteDocument { Name = "Original" }, asynchronous);
        byte[] before = await File.ReadAllBytesAsync(path);
        using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            var error = await Assert.ThrowsExactlyAsync<FileAccessException>(() =>
                SaveAsync(path, new JsonWriteDocument { Name = "Changed" }, asynchronous));
            Assert.IsTrue(error.InnerException is IOException or UnauthorizedAccessException);
            Assert.AreEqual("write json", error.Operation);
        }

        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path));
        Assert.HasCount(1, Directory.GetFileSystemEntries(_root));
        await SaveAsync(path, new JsonWriteDocument { Name = "Changed" }, asynchronous);
        Assert.AreEqual("Changed", JsonSaver.Load<JsonWriteDocument>(path, JsonWriteContext.Default).Name);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Replacement_WithDeleteSharing_PreservesOpenReadersSnapshot(bool asynchronous) {
        string path = Path.Combine(_root, "settings.json");
        await SaveAsync(path, new JsonWriteDocument { Name = "Original" }, asynchronous);
        byte[] before = await File.ReadAllBytesAsync(path);
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) {
            await SaveAsync(path, new JsonWriteDocument { Name = "Changed" }, asynchronous);
            using var snapshot = new MemoryStream();
            await stream.CopyToAsync(snapshot);
            CollectionAssert.AreEqual(before, snapshot.ToArray());
        }

        Assert.AreEqual("Changed", JsonSaver.Load<JsonWriteDocument>(path, JsonWriteContext.Default).Name);
        Assert.HasCount(1, Directory.GetFileSystemEntries(_root));
    }

    private static Task SaveAsync(string path, JsonWriteDocument document, bool asynchronous) {
        if (asynchronous) return JsonSaver.SaveAsync(path, document, JsonWriteContext.Default);
        JsonSaver.Save(path, document, JsonWriteContext.Default);
        return Task.CompletedTask;
    }

    private string _root = null!;
}

internal sealed class JsonWriteDocument {
    public string Name { get; set; } = "";
    public string Content {
        get => FailOnSerialization ? throw new InvalidOperationException("Serialization failed") : _content;
        set => _content = value;
    }
    [JsonIgnore]
    public bool FailOnSerialization { get; set; }

    private string _content = "Original content";
}

[JsonSerializable(typeof(JsonWriteDocument))]
internal partial class JsonWriteContext : JsonSerializerContext { }
