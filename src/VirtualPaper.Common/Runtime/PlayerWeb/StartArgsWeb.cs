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
    }
}
