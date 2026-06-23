using System.Text.Json.Serialization;

namespace VirtualPaper.Models.AppUpdate {
    [JsonSerializable(typeof(AppBuildInfo))]
    public partial class AppBuildInfoContext : JsonSerializerContext { }

    public class AppBuildInfo {
        [JsonPropertyName("app_build")]
        public string AppBuild { get; set; } = string.Empty;

        [JsonPropertyName("plugins")]
        public Dictionary<string, string> Plugins { get; set; } = new();
    }
}
