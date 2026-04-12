using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace VirtualPaper.Common.Runtime.PlayerWeb {
    [JsonSerializable(typeof(StartArgsWeb))]
    public partial class StartArgsWebContext : JsonSerializerContext { }

    public class StartArgsWeb {
        [JsonPropertyName("isPreview")]
        public bool IsPreview { get; set; }

        [JsonPropertyName("filePath")]
        public string? FilePath { get; set; }

        [JsonPropertyName("depthFilePath")]
        public string? DepthFilePath { get; set; }

        [JsonPropertyName("wpBasicDataFilePath")]
        public string? WpBasicDataFilePath { get; set; }

        [JsonPropertyName("wpEffectFilePathUsing")]
        public string? WpEffectFilePathUsing { get; set; }

        [JsonPropertyName("wpEffectFilePathTemporary")]
        public string? WpEffectFilePathTemporary { get; set; }

        [JsonPropertyName("wpEffectFilePathTemplate")]
        public string? WpEffectFilePathTemplate { get; set; }

        [JsonPropertyName("runtimeType")]
        public string? RuntimeType { get; set; }

        [JsonPropertyName("systemBackdrop")]
        public AppSystemBackdrop SystemBackdrop { get; set; }

        [JsonPropertyName("applicationTheme")]
        public AppTheme ApplicationTheme { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("extra")]
        public string? Extra { get; set; }
        
        [JsonPropertyName("isDebug")]
        public bool IsDebug { get; set; }
    }

    public static class StartArgsWebExtension {
        public static bool TryGetDataFromExtra<T>(this StartArgsWeb args, string key, out T? res) {
            res = default;
            if (string.IsNullOrEmpty(args.Extra)) return false;

            try {
                var node = JsonNode.Parse(args.Extra);
                var data = node?[key];
                res = JsonSerializer.Deserialize<T>(data?.ToJsonString() ?? string.Empty);
                
                return res != null;
            }
            catch (Exception) { }

            return false;
        }
    }
}
