using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace VirtualPaper.Common.Utils.Storage {
    public static class AtmosphereProperties {
        // Keep user values while adding missing fields and refreshing dropdown options.
        // The existing save/apply action persists them with the other settings.
        public static void EnsureAtmosphereProperties(JsonObject properties) {
            if (!properties.ContainsKey(nameof(UniverseCostumise.Atmosphere))) {
                properties[nameof(UniverseCostumise.Atmosphere)] = JsonSerializer.SerializeToNode(
                    new AtmospherePreset(), AtmospherePropertiesContext.Default.AtmospherePreset);
            }
            if (!properties.ContainsKey(nameof(UniverseCostumise.AtmosphereIntensity))) {
                properties[nameof(UniverseCostumise.AtmosphereIntensity)] = JsonSerializer.SerializeToNode(
                    new AtmosphereIntensity(), AtmospherePropertiesContext.Default.AtmosphereIntensity);
            }
            if (!properties.ContainsKey(nameof(UniverseCostumise.AtmosphereStyle))) {
                properties[nameof(UniverseCostumise.AtmosphereStyle)] = JsonSerializer.SerializeToNode(
                    new AtmosphereStyleProperty(), AtmospherePropertiesContext.Default.AtmosphereStyleProperty);
            }
            // Refresh our dropdown metadata so saved configs can select newly added effects.
            var defaults = JsonSerializer.SerializeToNode(new AtmospherePreset(), AtmospherePropertiesContext.Default.AtmospherePreset)!;
            properties[nameof(UniverseCostumise.Atmosphere)]!["Items"] = defaults["Items"]!.DeepClone();
            properties[nameof(UniverseCostumise.Atmosphere)]!["Max"] = (int)AtmosphereEffect.Bokeh;
            UpdateStyleItems(properties);
        }

        public static void UpdateStyleItems(JsonObject properties) {
            int.TryParse(properties[nameof(UniverseCostumise.Atmosphere)]?["Value"]?.ToString(), out var effect);
            var items = new JsonArray();
            foreach (var item in AtmosphereStyleProperty.GetItems((AtmosphereEffect)effect)) items.Add(item);
            properties[nameof(UniverseCostumise.AtmosphereStyle)]!["Items"] = items;
        }
    }

    [JsonSerializable(typeof(AtmospherePreset))]
    [JsonSerializable(typeof(AtmosphereIntensity))]
    [JsonSerializable(typeof(AtmosphereStyleProperty))]
    internal partial class AtmospherePropertiesContext : JsonSerializerContext { }
}
