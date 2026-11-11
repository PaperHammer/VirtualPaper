using System.Text;
using System.Text.Json;
using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.Common.Runtime.PlayerWeb;

/// <summary>User presets are independent of wallpaper folders and are published only after an atomic save.</summary>
public sealed class EffectPresetStore {
    public EffectPresetStore() : this(Path.Combine(Constants.CommonPaths.AppDataDir, "effect_presets.json")) { }
    public EffectPresetStore(string path) { _path = Path.GetFullPath(path); }

    public async Task<IReadOnlyList<EffectPreset>> LoadAsync() => [.. EffectPresets.BuiltIns(), .. await ReadUserPresetsAsync()];

    private async Task<List<EffectPreset>> ReadUserPresetsAsync() {
        if (!File.Exists(_path)) return [];
        // Preview windows may read while the manager atomically replaces the catalog.
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
        var catalog = await JsonSerializer.DeserializeAsync(stream, EffectPresetContext.Default.EffectPresetCatalog)
            ?? throw new InvalidDataException("Effect presets are empty.");
        if (catalog.Version != 1 || catalog.Items == null) throw new InvalidDataException("Unsupported effect preset file.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var preset in catalog.Items) {
            if (preset == null) throw new InvalidDataException("Empty user preset.");
            EffectPresets.Validate(preset);
            if (preset.IsBuiltIn || !Guid.TryParseExact(preset.Id, "N", out _) || !ids.Add(preset.Id))
                throw new InvalidDataException("Invalid user preset identity.");
        }
        return catalog.Items;
    }

    public async Task SaveAsync(EffectPreset preset) {
        EffectPresets.Validate(preset);
        if (preset.IsBuiltIn || !Guid.TryParseExact(preset.Id, "N", out _)) throw new InvalidOperationException("Built-in presets are read-only.");
        await Gate.WaitAsync();
        try {
            var users = await ReadUserPresetsAsync();
            if (users.Any(item => item.Id != preset.Id && item.Name.Equals(preset.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A preset with this name already exists.");
            int index = users.FindIndex(item => item.Id == preset.Id);
            var saved = preset with { Name = preset.Name.Trim() };
            if (index < 0) users.Add(saved);
            else users[index] = saved;
            await WriteAsync(users);
        }
        finally { Gate.Release(); }
    }

    public async Task DeleteAsync(string id) {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidOperationException("Built-in presets are read-only.");
        await Gate.WaitAsync();
        try {
            var users = await ReadUserPresetsAsync();
            if (users.RemoveAll(item => item.Id == id) > 0) await WriteAsync(users);
        }
        finally { Gate.Release(); }
    }

    private Task WriteAsync(List<EffectPreset> users) {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        return FileUtil.WriteAllTextAsync(_path, JsonSerializer.Serialize(new EffectPresetCatalog { Items = users },
            EffectPresetContext.Default.EffectPresetCatalog), new UTF8Encoding(false), useFileReplace: true);
    }

    private readonly string _path;
    private static readonly SemaphoreSlim Gate = new(1, 1);
}
