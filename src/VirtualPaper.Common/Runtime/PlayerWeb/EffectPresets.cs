using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace VirtualPaper.Common.Runtime.PlayerWeb;

public sealed record EffectPreset {
    [JsonRequired] public string Id { get; init; } = Guid.NewGuid().ToString("N");
    [JsonRequired] public string Name { get; init; } = "";
    // RUnknown denotes reusable parameters common to all supported wallpaper types.
    [JsonRequired] public RuntimeType RuntimeType { get; init; }
    [JsonRequired] public Dictionary<string, JsonElement> Values { get; init; } = new(StringComparer.Ordinal);
    [JsonIgnore] public bool IsBuiltIn => Id.StartsWith("builtin:", StringComparison.Ordinal);
}

/// <summary>Copies only known effect values. Resource paths and control definitions never enter a preset.</summary>
public static class EffectPresets {
    public static readonly IReadOnlyList<RuntimeType> Types = Array.AsReadOnly(new[] {
        RuntimeType.RUnknown, RuntimeType.RImage, RuntimeType.RImage3D, RuntimeType.RVideo, RuntimeType.RWeb
    });

    public static bool CanApply(EffectPreset preset, RuntimeType target) =>
        Types.Contains(target) && (preset.RuntimeType == RuntimeType.RUnknown || preset.RuntimeType == target);

    // Catalog entries are validated before comparison. Names and IDs do not describe effect values.
    public static bool HaveSameParameters(EffectPreset first, EffectPreset second) =>
        first.RuntimeType == second.RuntimeType && first.Values.Count == second.Values.Count
        && first.Values.All(pair => second.Values.TryGetValue(pair.Key, out var value)
            && pair.Value.ValueKind == value.ValueKind
            && (value.ValueKind is JsonValueKind.True or JsonValueKind.False
                || value.ValueKind == JsonValueKind.Number && pair.Value.GetDouble() == value.GetDouble()));

    public static JsonObject Defaults(RuntimeType type) {
        JsonObject properties = type switch {
            RuntimeType.RUnknown or RuntimeType.RWeb => JsonSerializer.SerializeToNode(new WebCostumize(), WebCostumizeContext.Default.WebCostumize)!.AsObject(),
            RuntimeType.RImage => JsonSerializer.SerializeToNode(new PictureAndGifCostumise(), PictureAndGifCostumiseContext.Default.PictureAndGifCostumise)!.AsObject(),
            RuntimeType.RImage3D => JsonSerializer.SerializeToNode(new Picture3DCostumize(), Picture3DCostumizeContext.Default.Picture3DCostumize)!.AsObject(),
            RuntimeType.RVideo => JsonSerializer.SerializeToNode(new VideoCostumize(), VideoCostumizeContext.Default.VideoCostumize)!.AsObject(),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        AtmosphereProperties.EnsureAtmosphereProperties(properties);
        if (type == RuntimeType.RImage3D) ParallaxProperties.EnsureDepthProperties(properties);
        return properties;
    }

    public static EffectPreset Capture(string name, RuntimeType type, JsonObject properties, string? id = null) {
        var schema = Defaults(type);
        properties = (JsonObject)properties.DeepClone();
        AtmosphereProperties.EnsureAtmosphereProperties(properties);
        NormalizeStyle(properties);
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, _) in schema) {
            if (properties[key] is JsonObject property && property["Value"] is { } value)
                values[key] = JsonSerializer.SerializeToElement(value);
        }
        var preset = new EffectPreset { Id = id ?? Guid.NewGuid().ToString("N"), Name = name.Trim(), RuntimeType = type, Values = values };
        Validate(preset);
        return preset;
    }

    public static void Validate(EffectPreset preset) {
        if (!Types.Contains(preset.RuntimeType) || string.IsNullOrWhiteSpace(preset.Id)
            || string.IsNullOrWhiteSpace(preset.Name) || preset.Name.Length > 80 || preset.Values == null || preset.Values.Count == 0)
            throw new InvalidDataException("Invalid effect preset.");
        var schema = Defaults(preset.RuntimeType);
        // Atmosphere style choices depend on the selected effect.
        if (preset.Values.TryGetValue(nameof(UniverseCostumise.Atmosphere), out var atmosphere)) {
            ValidateValue(schema[nameof(UniverseCostumise.Atmosphere)]!.AsObject(), atmosphere);
            schema[nameof(UniverseCostumise.Atmosphere)]!["Value"] = JsonNode.Parse(atmosphere.GetRawText());
            AtmosphereProperties.UpdateStyleItems(schema);
        }
        foreach (var (key, value) in preset.Values) {
            if (schema[key] is not JsonObject property) throw new InvalidDataException("Unknown preset parameter: " + key);
            ValidateValue(property, value);
        }
    }

    private static void ValidateValue(JsonObject property, JsonElement value) {
        string type = property["Type"]!.ToString();
        if (type.Equals("Checkbox", StringComparison.OrdinalIgnoreCase)) {
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new InvalidDataException("Invalid preset switch.");
            return;
        }
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
            throw new InvalidDataException("Preset parameters must be numbers or switches.");
        double min = double.Parse(property["Min"]?.ToString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        double max = double.Parse(property["Max"]?.ToString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        if (type.Equals("Dropdown", StringComparison.OrdinalIgnoreCase)) {
            max = Math.Max(0, property["Items"]!.AsArray().Count - 1);
            // The parameter controls read JSON integers directly; 1.0 and 1e0 cannot be read as Int32.
            if (!value.TryGetInt32(out _)) throw new InvalidDataException("Invalid preset choice.");
        }
        if (number < min || number > max) throw new InvalidDataException("Preset parameter is outside its allowed range.");
    }

    public static JsonObject Apply(JsonObject properties, EffectPreset preset, RuntimeType target) {
        Validate(preset);
        if (!CanApply(preset, target)) throw new InvalidOperationException("This preset does not support this wallpaper type.");

        var result = (JsonObject)properties.DeepClone();
        AtmosphereProperties.EnsureAtmosphereProperties(result);
        if (target == RuntimeType.RImage3D) ParallaxProperties.EnsureDepthProperties(result);
        var schema = Defaults(target);
        foreach (var (key, value) in preset.Values) {
            if (!result.ContainsKey(key)) result[key] = schema[key]!.DeepClone();
            result[key]!["Value"] = JsonNode.Parse(value.GetRawText());
        }
        AtmosphereProperties.UpdateStyleItems(result);
        NormalizeStyle(result);
        return result;
    }

    private static void NormalizeStyle(JsonObject properties) {
        var style = properties[nameof(UniverseCostumise.AtmosphereStyle)]!.AsObject();
        int count = style["Items"]!.AsArray().Count;
        int.TryParse(style["Value"]?.ToString(), out int value);
        style["Value"] = Math.Clamp(value, 0, Math.Max(0, count - 1));
    }

    public static IReadOnlyList<EffectPreset> BuiltIns() {
        var result = new List<EffectPreset>();
        foreach (var (id, effect, intensity, saturation) in new[] {
            ("Natural", AtmosphereEffect.None, 50, 1.0),
            ("Soft", AtmosphereEffect.None, 50, 0.8),
            ("Snow", AtmosphereEffect.Snow, 25, 0.95),
            ("Stardust", AtmosphereEffect.Stardust, 30, 1.0)
        }) {
            var properties = Defaults(RuntimeType.RUnknown);
            properties[nameof(UniverseCostumise.Atmosphere)]!["Value"] = (int)effect;
            properties[nameof(UniverseCostumise.AtmosphereIntensity)]!["Value"] = intensity;
            properties[nameof(UniverseCostumise.Saturation)]!["Value"] = saturation;
            AtmosphereProperties.UpdateStyleItems(properties);
            result.Add(Capture("EffectPreset_" + id, RuntimeType.RUnknown, properties, "builtin:" + id));
        }
        return result;
    }
}

internal sealed class EffectPresetCatalog {
    [JsonRequired] public int Version { get; set; } = 1;
    [JsonRequired] public List<EffectPreset> Items { get; set; } = [];
}

[JsonSerializable(typeof(EffectPresetCatalog))]
internal partial class EffectPresetContext : JsonSerializerContext { }
