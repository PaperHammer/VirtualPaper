using System.Text.Json.Serialization;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;

namespace VirtualPaper.Models.Net {
    [JsonSerializable(typeof(WpBasicDataDto))]
    [JsonSerializable(typeof(List<WpBasicDataDto>))]
    [JsonSourceGenerationOptions(
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        GenerationMode = JsonSourceGenerationMode.Default
    )]
    public partial class WpBasicDataDtoContext : JsonSerializerContext { }

    public class WpBasicDataDto {
        public string AppName { get; set; } = string.Empty;
        public string AppVersion { get; set; } = string.Empty;
        public string FileVersion { get; set; } = string.Empty;
        public byte[] ThuImage { get; set; } = [];
        public byte[] Image { get; set; } = [];
        public string Uid { get; set; } = string.Empty;
        public string UserUid { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public FileType Type { get; set; }
        public string FileExtension { get; set; } = string.Empty;
        public string PublishDate { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Partition { get; set; } = string.Empty;
        public string? Tags { get; set; }
        public string? Description { get; set; }
        public WallpaperStatus Status { get; set; }
    }
}
