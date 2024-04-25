namespace VirtualPaper.Common.Utils.Files.Models
{
    public class FileData(WallpaperType type, string[] extensions)
    {
        public WallpaperType Type { get; set; } = type;
        public string[] Extentions { get; set; } = extensions;
    }
}
