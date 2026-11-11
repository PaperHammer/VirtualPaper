using System;
using System.Text.Json.Serialization;
using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.WpSettingsPanel.Utils {
    [JsonSerializable(typeof(WallpaperIndexEntry))]
    public partial class WallpaperIndexEntryContext : JsonSerializerContext { }

    public record WallpaperIndexEntry {
        public string Uid { get; set; } = default!;
        public string FolderPath { get; set; } = default!;
        public string JsonPath { get; set; } = default!; // WpBasicData 的 json 路径
        public DateTime CreateTime { get; set; } // 用于排序/筛选
        public string Title { get; set; } = ""; // 用于模糊匹配
        public string FileName { get; set; } = "";
        public string ThumbnailPath { get; set; } = "";
        // A display fallback must not become a saved title or require another metadata read.
        [JsonIgnore]
        public string DisplayName {
            get {
                if (!string.IsNullOrWhiteSpace(Title)) return Title;
                if (!string.IsNullOrWhiteSpace(FileName)) return FileName;
                return Uid;
            }
        }
        public string? Author { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string Description { get; set; } = "";
        public FileType FType { get; set; }

        internal static WallpaperIndexEntry FromData(IWpBasicData data) => new() {
            Uid = data.WallpaperUid,
            FolderPath = data.FolderPath,
            JsonPath = Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName),
            CreateTime = data.CreatedTime,
            Title = data.Title ?? "",
            FileName = Path.GetFileName(data.FilePath) ?? "",
            ThumbnailPath = data.ThumbnailPath ?? "",
            Description = data.Desc ?? "",
            Author = data.Authors,
            Tags = WallpaperOrganization.NormalizeTags([data.Tags]),
            FType = data.FType
        };
    }
}
