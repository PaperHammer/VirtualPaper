using System.Text.Json.Serialization;
using VirtualPaper.Cores.AppUpdate.Models;

namespace VirtualPaper.Models.AppUpdate {
    [JsonSerializable(typeof(UpdateFlag))]
    [JsonSerializable(typeof(PluginFlagInfo))]
    [JsonSerializable(typeof(FileHashInfo))]
    [JsonSerializable(typeof(PluginManifestInfo))]
    public partial class UpdateFlagContext : JsonSerializerContext { }

    public class UpdateFlag {
        [JsonPropertyName("status")]
        public string Status { get; set; } = UpdateStatusPending;

        [JsonPropertyName("app_build")]
        public string AppBuild { get; set; } = string.Empty;

        [JsonPropertyName("plugins")]
        public Dictionary<string, PluginFlagInfo> Plugins { get; set; } = new();

        [JsonPropertyName("app_plugins_info")]
        public Dictionary<string, PluginManifestInfo> AppPluginsInfo { get; set; } = new();

        [JsonPropertyName("removed_plugins")]
        public List<string> RemovedPlugins { get; set; } = new();

        public const string UpdateStatusPending = "pending";
        public const string UpdateStatusInProgress = "in_progress";
        public const string UpdateStatusCompleted = "completed";
    }

    public class PluginFlagInfo {
        [JsonPropertyName("target")]
        public string Target { get; set; } = string.Empty;

        [JsonPropertyName("build")]
        public string Build { get; set; } = string.Empty;

        [JsonPropertyName("files")]
        public List<FileHashInfo> Files { get; set; } = new();
    }

    public class FileHashInfo {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;
    }
}
