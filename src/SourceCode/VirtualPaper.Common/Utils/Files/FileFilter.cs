using VirtualPaper.Common.Utils.Files.Models;

namespace VirtualPaper.Common.Utils.Files
{
    /// <summary>
    /// OpenFileDialog helper.
    /// </summary>
    public class FileFilter
    {
        public static readonly FileData[] SupportedFormats = [
            new FileData(WallpaperType.video, [".mp4", ".webm"]),
            new FileData(WallpaperType.picture, [".jpg", ".jpeg", ".bmp", ".png", ".svg", ".webp"]),
            new FileData(WallpaperType.gif, [".gif" ,".apng"]),
            //new FileData(WallpaperType.heic, new string[] {".heic" }),//, ".heics", ".heif", ".heifs" }),
            //new FileData(WallpaperType.web, [".html"]),
            //new FileData(WallpaperType.webaudio, [".html"]),
            //new FileData(WallpaperType.app, [".exe"]),
            //new FileFilter(WallpaperType.unity,"*.exe"),
            //new FileFilter(WallpaperType.unityaudio,"Unity Audio Visualiser |*.exe"),
            //new FileData(WallpaperType.godot, [".exe"]),
            //note:  .zip is not a wallpapertype, its a filetype.
            //new FileData((WallpaperType)(100),  [".zip"])
        ];

        /// <summary>
        /// Identify  wallpaper type from file information.
        /// <br>If more than one wallpapertype has same extension, first result is selected.</br>
        /// </summary>
        /// <param name="filePath">Path to file.</param>
        /// <returns>-1 if not supported, 100 if  .zip</returns>
        public static WallpaperType GetFileType(string filePath)
        {
            //todo: Use file header(?) to verify filetype instead of extension.
            string s = Path.GetExtension(filePath);
            var item = SupportedFormats.FirstOrDefault(
                x => x.Extentions.Any(y => y.Equals(Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase)));

            return item != null ? item.Type : (WallpaperType)(-1);
        }
    }
}
