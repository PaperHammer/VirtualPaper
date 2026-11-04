using System.Text.Json.Serialization;

namespace VirtualPaper.Cores.AppUpdate.Models {
    [JsonSerializable(typeof(AppCompManifest))]
    [JsonSerializable(typeof(PendingUpdateManifest))]
    [JsonSerializable(typeof(PendingPluginInfo))]
    public partial class UpdateManifestContext : JsonSerializerContext { }

    /// <summary>
    /// app_comp_manifest.json — 记录 app build number 和所有插件的 build number
    /// </summary>
    public class AppCompManifest {
        [JsonPropertyName("app_build_number")]
        public string AppBuildNumber { get; set; } = string.Empty;

        [JsonPropertyName("plugins")]
        public Dictionary<string, string> Plugins { get; set; } = new();
    }

    /// <summary>
    /// pending_update_plugins_manifest.json — 记录需要更新的插件信息
    /// </summary>
    public class PendingUpdateManifest {
        [JsonPropertyName("plugins")]
        public Dictionary<string, PendingPluginInfo> Plugins { get; set; } = new();
    }

    public class PendingPluginInfo {
        [JsonPropertyName("build_number")]
        public string BuildNumber { get; set; } = string.Empty;

        [JsonPropertyName("asset")]
        public string Asset { get; set; } = string.Empty;

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;
    }
}
