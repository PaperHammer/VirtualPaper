using System.Text.Json;
using System.Text.Json.Nodes;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.PlayerWeb;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class EffectPresetStoreTests {
    private string _root = null!, _path = null!;
    private EffectPresetStore _store = null!;
    [TestInitialize]
    public void Initialize() {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.Presets", Guid.NewGuid().ToString("N"))).FullName;
        _path = Path.Combine(_root, "presets.json");
        _store = new EffectPresetStore(_path);
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(_root, true);
    private static EffectPreset New(string name) => EffectPresets.Capture(name, RuntimeType.RImage, EffectPresets.Defaults(RuntimeType.RImage));

    [TestMethod]
    [DoNotParallelize]
    public async Task DefaultConstructor_UsesAppDataDirectoryAcrossStoreInstances() {
        string previousRoot = Constants.CommonPaths.TestRootDir;
        bool previousTestMode = Constants.IsTestMode;
        try {
            Constants.IsTestMode = true;
            Constants.CommonPaths.TestRootDir = _root;
            // Check isolation before invoking any file operations through the default path.
            Assert.AreEqual(_root, Constants.CommonPaths.AppDataDir);
            string defaultPath = Path.Combine(_root, "effect_presets.json");
            var preset = New("Default directory");

            await new EffectPresetStore().SaveAsync(preset);

            Assert.IsTrue(File.Exists(defaultPath));
            var saved = (await new EffectPresetStore(defaultPath).LoadAsync()).Single(item => !item.IsBuiltIn);
            Assert.AreEqual(preset.Id, saved.Id);
            Assert.AreEqual(preset.Name, saved.Name);
            Assert.AreEqual(preset.Id, (await new EffectPresetStore().LoadAsync()).Single(item => !item.IsBuiltIn).Id);
            await new EffectPresetStore().DeleteAsync(preset.Id);
            Assert.IsEmpty((await new EffectPresetStore(defaultPath).LoadAsync()).Where(item => !item.IsBuiltIn));
        }
        finally {
            Constants.CommonPaths.TestRootDir = previousRoot;
            Constants.IsTestMode = previousTestMode;
        }
    }

    [TestMethod]
    public async Task MissingCatalog_LoadAndDeleteDoNotCreateFiles() {
        var loaded = await _store.LoadAsync();
        await _store.DeleteAsync(Guid.NewGuid().ToString("N"));

        Assert.HasCount(EffectPresets.BuiltIns().Count, loaded);
        Assert.IsTrue(loaded.All(item => item.IsBuiltIn));
        Assert.IsFalse(File.Exists(_path));
        Assert.IsEmpty(Directory.GetFileSystemEntries(_root));
    }

    [TestMethod]
    public async Task SaveRenameCopyDelete_PersistAcrossRestartWithoutTouchingBuiltIns() {
        var original = New("Original");
        await _store.SaveAsync(original);
        var renamed = original with { Name = "Renamed" };
        await _store.SaveAsync(renamed);
        var copy = renamed with { Id = Guid.NewGuid().ToString("N"), Name = "Copy" };
        await _store.SaveAsync(copy);
        var restarted = new EffectPresetStore(_path);
        var loaded = await restarted.LoadAsync();
        Assert.HasCount(2, loaded.Where(item => !item.IsBuiltIn).ToArray());
        Assert.AreEqual("Renamed", loaded.Single(item => item.Id == original.Id).Name);
        await restarted.DeleteAsync(original.Id);
        loaded = await new EffectPresetStore(_path).LoadAsync();
        Assert.AreEqual(copy.Id, loaded.Single(item => !item.IsBuiltIn).Id);
        Assert.HasCount(EffectPresets.BuiltIns().Count, loaded.Where(item => item.IsBuiltIn).ToArray());
        string saved = await File.ReadAllTextAsync(_path);
        Assert.IsFalse(saved.Contains("FolderPath"));
        Assert.IsFalse(saved.Contains("\"Type\""));
        Assert.IsFalse(saved.Contains("\"Max\"", StringComparison.Ordinal)); // No control definitions inside Values.
    }

    [TestMethod]
    [DataRow(RuntimeType.RUnknown)]
    [DataRow(RuntimeType.RImage)]
    [DataRow(RuntimeType.RImage3D)]
    [DataRow(RuntimeType.RVideo)]
    [DataRow(RuntimeType.RWeb)]
    public async Task Rename_KeepsIdentityTypeAndEveryEffectAcrossRestart(RuntimeType type) {
        var properties = EffectPresets.Defaults(type);
        properties[nameof(UniverseCostumise.Saturation)]!["Value"] = 0.73;
        var original = EffectPresets.Capture("Original", type, properties);
        await _store.SaveAsync(original);

        await _store.SaveAsync(original with { Name = " Renamed " });

        var saved = (await new EffectPresetStore(_path).LoadAsync()).Single(item => !item.IsBuiltIn);
        Assert.AreEqual("Renamed", saved.Name);
        Assert.AreEqual(original.Id, saved.Id);
        Assert.AreEqual(original.RuntimeType, saved.RuntimeType);
        CollectionAssert.AreEquivalent(original.Values.Keys.ToArray(), saved.Values.Keys.ToArray());
        foreach (var (key, value) in original.Values) Assert.AreEqual(value.GetRawText(), saved.Values[key].GetRawText(), key);
        Assert.AreEqual("Original", original.Name);
    }

    [TestMethod]
    public async Task BuiltInChanges_AreRejectedAndDoNotCreateAUserFile() {
        var preset = EffectPresets.BuiltIns()[0];
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.SaveAsync(preset));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.DeleteAsync(preset.Id));
        Assert.IsFalse(File.Exists(_path));
    }

    [TestMethod]
    public async Task BuiltInCopy_IsEditableAndPersistsWithoutChangingItsTemplate() {
        foreach (var template in EffectPresets.BuiltIns()) {
            string before = JsonSerializer.Serialize(template);
            var properties = EffectPresets.Apply(EffectPresets.Defaults(template.RuntimeType), template, template.RuntimeType);
            var copy = EffectPresets.Capture(template.Name + " (copy)", template.RuntimeType, properties);

            Assert.IsFalse(copy.IsBuiltIn);
            Assert.AreNotEqual(template.Id, copy.Id);
            Assert.AreEqual(template.RuntimeType, copy.RuntimeType);
            foreach (var (key, value) in template.Values) Assert.AreEqual(value.GetRawText(), copy.Values[key].GetRawText(), key);

            // Editing the new draft must not write through to the source template.
            properties[nameof(UniverseCostumise.Saturation)]!["Value"] = 0.73;
            copy = EffectPresets.Capture(copy.Name, copy.RuntimeType, properties, copy.Id);
            await _store.SaveAsync(copy);

            var saved = (await new EffectPresetStore(_path).LoadAsync()).Single(item => item.Id == copy.Id);
            Assert.AreEqual(0.73, saved.Values[nameof(UniverseCostumise.Saturation)].GetDouble());
            Assert.AreEqual(before, JsonSerializer.Serialize(template));
        }
    }

    [TestMethod]
    public async Task ConcurrentStoreInstances_PreserveAllSavedPresets() {
        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => new EffectPresetStore(_path).SaveAsync(New("Preset " + i))));
        var users = (await _store.LoadAsync()).Where(item => !item.IsBuiltIn).ToArray();
        Assert.HasCount(10, users);
        Assert.HasCount(10, users.Select(item => item.Id).Distinct().ToArray());
    }

    [TestMethod]
    public async Task DuplicateName_DoesNotOverwriteExistingData() {
        await _store.SaveAsync(New("Snow"));
        string before = await File.ReadAllTextAsync(_path);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.SaveAsync(New(" snow ")));
        Assert.AreEqual(before, await File.ReadAllTextAsync(_path));
    }

    [TestMethod]
    public async Task ConflictingRename_IsRejectedAndCanBeRetried() {
        var first = New("Snow");
        var second = New("Forest");
        await _store.SaveAsync(first);
        await _store.SaveAsync(second);
        string before = await File.ReadAllTextAsync(_path);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _store.SaveAsync(first with { Name = " FOREST " }));
        Assert.AreEqual(before, await File.ReadAllTextAsync(_path));
        await _store.SaveAsync(first with { Name = " SNOW " });

        var users = (await new EffectPresetStore(_path).LoadAsync()).Where(item => !item.IsBuiltIn).ToArray();
        CollectionAssert.AreEqual(new[] { first.Id, second.Id }, users.Select(item => item.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "SNOW", "Forest" }, users.Select(item => item.Name).ToArray());
    }

    [TestMethod]
    public async Task EditingSelectedPreset_CanBeDetectedAndReappliedAfterCatalogReload() {
        var original = New("Selected");
        await _store.SaveAsync(original);
        var selected = (await _store.LoadAsync()).Single(item => item.Id == original.Id);
        var live = EffectPresets.Apply(EffectPresets.Defaults(RuntimeType.RImage), selected, RuntimeType.RImage);
        string before = live.ToJsonString();
        var revised = New("Renamed") with { Id = selected.Id };
        revised.Values[nameof(UniverseCostumise.Saturation)] = JsonSerializer.SerializeToElement(0.7);
        await new EffectPresetStore(_path).SaveAsync(revised);

        var refreshed = (await _store.LoadAsync()).Single(item => item.Id == selected.Id);

        Assert.IsFalse(EffectPresets.HaveSameParameters(selected, refreshed));
        Assert.AreEqual(before, live.ToJsonString(), "Refreshing a catalog must not automatically change a live trial.");
        Assert.AreEqual(1.0, selected.Values[nameof(UniverseCostumise.Saturation)].GetDouble());
        var reapplied = EffectPresets.Apply(live, refreshed, RuntimeType.RImage);
        Assert.AreEqual(0.7, (double)reapplied[nameof(UniverseCostumise.Saturation)]!["Value"]!);
        Assert.AreEqual(before, live.ToJsonString());
    }

    [TestMethod]
    public async Task DeleteUnknownAndLastPreset_LeavesAReadableCatalog() {
        var preset = New("Only user preset");
        await _store.SaveAsync(preset);
        string before = await File.ReadAllTextAsync(_path);

        await _store.DeleteAsync(Guid.NewGuid().ToString("N"));
        Assert.AreEqual(before, await File.ReadAllTextAsync(_path));
        await _store.DeleteAsync(preset.Id);

        Assert.IsTrue(File.Exists(_path));
        Assert.IsEmpty((await new EffectPresetStore(_path).LoadAsync()).Where(item => !item.IsBuiltIn));
        await _store.SaveAsync(New("After delete"));
        Assert.AreEqual("After delete", (await _store.LoadAsync()).Single(item => !item.IsBuiltIn).Name);
    }

    [TestMethod]
    public async Task ConcurrentSavesRenamesAndDeletes_PreserveUnrelatedChanges() {
        var originals = Enumerable.Range(0, 8).Select(i => New("Original " + i)).ToArray();
        foreach (var preset in originals) await _store.SaveAsync(preset);
        var additions = Enumerable.Range(0, 4).Select(i => New("New " + i)).ToArray();

        await Task.WhenAll(originals.Take(4).Select(item => new EffectPresetStore(_path).DeleteAsync(item.Id))
            .Concat(originals.Skip(4).Select(item => new EffectPresetStore(_path).SaveAsync(item with { Name = "Renamed " + item.Name })))
            .Concat(additions.Select(item => new EffectPresetStore(_path).SaveAsync(item))));

        var users = (await new EffectPresetStore(_path).LoadAsync()).Where(item => !item.IsBuiltIn).ToArray();
        CollectionAssert.AreEquivalent(originals.Skip(4).Concat(additions).Select(item => item.Id).ToArray(),
            users.Select(item => item.Id).ToArray());
        foreach (var original in originals.Skip(4))
            Assert.AreEqual("Renamed " + original.Name, users.Single(item => item.Id == original.Id).Name);
        Assert.IsEmpty(Directory.GetFiles(_root, ".*.tmp"));
    }

    [TestMethod]
    public async Task CorruptFile_IsPreservedRatherThanReplacedWithEmptyPreferences() {
        const string broken = "{broken";
        await File.WriteAllTextAsync(_path, broken);
        await Assert.ThrowsAsync<JsonException>(() => _store.LoadAsync());
        await Assert.ThrowsAsync<JsonException>(() => _store.SaveAsync(New("Safe")));
        await Assert.ThrowsAsync<JsonException>(() => _store.DeleteAsync(Guid.NewGuid().ToString("N")));
        Assert.AreEqual(broken, await File.ReadAllTextAsync(_path));
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"Version\":1}")]
    [DataRow("{\"Version\":1,\"Items\":[{\"Name\":\"Incomplete\"}]}")]
    public async Task MissingRequiredData_IsPreserved(string content) {
        await File.WriteAllTextAsync(_path, content);
        await Assert.ThrowsAsync<JsonException>(() => _store.LoadAsync());
        await Assert.ThrowsAsync<JsonException>(() => _store.SaveAsync(New("Safe")));
        await Assert.ThrowsAsync<JsonException>(() => _store.DeleteAsync(Guid.NewGuid().ToString("N")));
        Assert.AreEqual(content, await File.ReadAllTextAsync(_path));
    }

    [TestMethod]
    public async Task FutureVersion_IsPreserved() {
        const string future = "{\"Version\":2,\"Items\":[]}";
        await File.WriteAllTextAsync(_path, future);
        await Assert.ThrowsAsync<InvalidDataException>(() => _store.LoadAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() => _store.SaveAsync(New("Safe")));
        await Assert.ThrowsAsync<InvalidDataException>(() => _store.DeleteAsync(Guid.NewGuid().ToString("N")));
        Assert.AreEqual(future, await File.ReadAllTextAsync(_path));
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("{\"Version\":1,\"Items\":null}")]
    [DataRow("{\"Version\":1,\"Items\":[null]}")]
    public async Task NullCatalogEntries_ArePreserved(string content) {
        await File.WriteAllTextAsync(_path, content);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => _store.LoadAsync());
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => _store.SaveAsync(New("Safe")));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => _store.DeleteAsync(Guid.NewGuid().ToString("N")));
        Assert.AreEqual(content, await File.ReadAllTextAsync(_path));
    }

    [TestMethod]
    [DataRow("invalid-id")]
    [DataRow("builtin-id")]
    [DataRow("duplicate-id")]
    [DataRow("invalid-values")]
    public async Task InvalidUserPreset_RejectsAllOperationsWithoutChangingTheFile(string invalidData) {
        var preset = JsonSerializer.SerializeToNode(New("Existing"))!.AsObject();
        if (invalidData == "invalid-id") preset["Id"] = "not-a-guid";
        if (invalidData == "builtin-id") preset["Id"] = "builtin:Natural";
        if (invalidData == "invalid-values") preset["Values"]![nameof(UniverseCostumise.Brightness)] = "not-a-number";
        var items = new JsonArray(preset);
        if (invalidData == "duplicate-id") items.Add(preset.DeepClone());
        string content = new JsonObject { ["Version"] = 1, ["Items"] = items }.ToJsonString();
        await File.WriteAllTextAsync(_path, content);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => _store.LoadAsync());
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => _store.SaveAsync(New("Safe")));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => _store.DeleteAsync(Guid.NewGuid().ToString("N")));
        Assert.AreEqual(content, await File.ReadAllTextAsync(_path));
        Assert.IsEmpty(Directory.GetFiles(_root, ".*.tmp"));
    }

    [TestMethod]
    public async Task AtomicReplacement_PreservesAnOpenReadersSnapshot() {
        var original = New("Original");
        await _store.SaveAsync(original);
        using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) {
            await _store.SaveAsync(original with { Name = "Changed" });
            using var reader = new StreamReader(stream);
            var old = JsonNode.Parse(await reader.ReadToEndAsync())!;
            Assert.AreEqual("Original", old["Items"]![0]!["Name"]!.ToString());
        }
        Assert.AreEqual("Changed", (await _store.LoadAsync()).Single(item => !item.IsBuiltIn).Name);
    }

    [TestMethod]
    public async Task FailedReplacement_KeepsThePreviouslySavedFileAndRemovesTemporaryFiles() {
        var original = New("Original");
        await _store.SaveAsync(original);
        string before = await File.ReadAllTextAsync(_path);
        using (var locked = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            try {
                await _store.SaveAsync(original with { Name = "Changed" });
                Assert.Fail("Replacing a file whose reader denies delete sharing must fail.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        Assert.AreEqual(before, await File.ReadAllTextAsync(_path));
        Assert.IsEmpty(Directory.GetFiles(_root, ".*.tmp"));
        // Failed writes must release the store gate, so the same edit can be retried.
        await _store.SaveAsync(original with { Name = "Changed" });
        Assert.AreEqual("Changed", (await new EffectPresetStore(_path).LoadAsync()).Single(item => !item.IsBuiltIn).Name);
    }
}
