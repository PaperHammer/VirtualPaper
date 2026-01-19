using System;
using System.Text.Json.Serialization;

namespace VirtualPaper.WpSettingsPanel.Utils {
    [JsonSerializable(typeof(WallpaperIndexEntry))]
    public partial class WallpaperIndexEntryContext : JsonSerializerContext { }

    public record WallpaperIndexEntry {
        public string Uid { get; set; } = default!;
        public string FolderPath { get; set; } = default!;
        public string JsonPath { get; set; } = default!; // WpBasicData 的 json 路径
        public DateTime CreateTime { get; set; } // 用于排序/筛选
        public string Title { get; set; } = ""; // 用于模糊匹配
        public string? Author { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
    }
}
