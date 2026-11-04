using System.Text.Json.Serialization;

namespace VirtualPaper.Models.AppUpdate {
    [JsonSerializable(typeof(PluginsUpdateFlag))]
    [JsonSerializable(typeof(InstallerUpdateFlag))]
    [JsonSerializable(typeof(PluginFlagInfo))]
    [JsonSerializable(typeof(FileHashInfo))]
    public partial class UpdateFlagContext : JsonSerializerContext { }

    public enum UpdateStatus {
        Pending,
        InProgress,
        Completed
    }

    public class PluginsUpdateFlag {
        [JsonPropertyName("status")]
        public UpdateStatus Status { get; set; }

        [JsonPropertyName("app_build_number")]
        public string AppBuildNumber { get; set; } = string.Empty;

        [JsonPropertyName("plugins")]
        public Dictionary<string, PluginFlagInfo> Plugins { get; set; } = new();

        [JsonPropertyName("removed_plugins")]
        public List<string> RemovedPlugins { get; set; } = new();
    }

    public class InstallerUpdateFlag {
        [JsonPropertyName("status")]
        public UpdateStatus Status { get; set; }

        [JsonPropertyName("installer_path")]
        public string InstallerPath { get; set; } = string.Empty;

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;
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
