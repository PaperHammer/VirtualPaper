using System.Text.Json.Serialization;

namespace VirtualPaper.Cores.AppUpdate.Models {
    [JsonSerializable(typeof(UpdateManifest))]
	[JsonSerializable(typeof(PluginUpdateInfo))]
    public partial class UpdateManifestContext : JsonSerializerContext { }

    public class UpdateManifest {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("app_build")]
        public string AppBuild { get; set; } = string.Empty;

        [JsonPropertyName("min_app_build")]
        public string MinAppBuild { get; set; } = string.Empty;

        [JsonPropertyName("plugins")]
        public Dictionary<string, PluginUpdateInfo> Plugins { get; set; } = new();

        [JsonPropertyName("removed_plugins")]
        public List<string> RemovedPlugins { get; set; } = new();

        public bool IsRestartUpdate => Type.Equals("restart", StringComparison.OrdinalIgnoreCase);
        public bool IsInstallUpdate => Type.Equals("install", StringComparison.OrdinalIgnoreCase);
    }

    public class PluginUpdateInfo {
        [JsonPropertyName("build")]
        public string Build { get; set; } = string.Empty;

        [JsonPropertyName("asset")]
        public string Asset { get; set; } = string.Empty;

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;
    }
}
