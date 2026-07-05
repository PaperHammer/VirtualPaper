using System.Text.Json.Serialization;

namespace VirtualPaper.Cores.AppUpdate.Models {
    [JsonSerializable(typeof(UpdateManifest))]
    [JsonSerializable(typeof(PluginManifestInfo))]
    public partial class UpdateManifestContext : JsonSerializerContext { }

    public class UpdateManifest {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("app_build")]
        public string AppBuild { get; set; } = string.Empty;

        [JsonPropertyName("min_app_build")]
        public string MinAppBuild { get; set; } = string.Empty;

        [JsonPropertyName("app_plugins_info")]
        public Dictionary<string, PluginManifestInfo> AppPluginsInfo { get; set; } = [];

        [JsonPropertyName("removed_plugins")]
        public List<string> RemovedPlugins { get; set; } = [];

        public bool IsPluginsUpdate => Type.Equals("plugins", StringComparison.OrdinalIgnoreCase);
        public bool IsInstallerUpdate => !IsPluginsUpdate;

        // 兼容旧格式：获取需要更新的插件列表
        public Dictionary<string, PluginUpdateInfo> Plugins {
            get {
                var result = new Dictionary<string, PluginUpdateInfo>();
                foreach (var kv in AppPluginsInfo) {
                    if (kv.Value.PendingUpdate && !string.IsNullOrEmpty(kv.Value.Asset)) {
                        result[kv.Key] = new PluginUpdateInfo {
                            Build = kv.Value.BuildNumber,
                            Asset = kv.Value.Asset,
                            Sha256 = kv.Value.Sha256
                        };
                    }
                }
                return result;
            }
        }
    }

    public class PluginManifestInfo {
        [JsonPropertyName("pending_update")]
        public bool PendingUpdate { get; set; }

        [JsonPropertyName("build_number")]
        public string BuildNumber { get; set; } = string.Empty;

        [JsonPropertyName("asset")]
        public string? Asset { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }
    }

    // 保留旧格式用于兼容
    public class PluginUpdateInfo {
        [JsonPropertyName("build")]
        public string Build { get; set; } = string.Empty;

        [JsonPropertyName("asset")]
        public string Asset { get; set; } = string.Empty;

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;
    }
}
