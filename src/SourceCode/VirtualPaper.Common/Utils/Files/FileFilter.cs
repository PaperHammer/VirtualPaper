using VirtualPaper.Common.Utils.Files.Models;

namespace VirtualPaper.Common.Utils.Files
{
    /// <summary>
    /// OpenFileDialog helper.
    /// </summary>
    public class FileFilter
    {
        public static readonly FileData[] SupportedFormats = [
            new FileData(WallpaperType.video, [".wmv", ".avi", ".flv", ".m4v",
                    ".mkv", ".mov", ".mp4", ".mp4v", ".mpeg4",
                    ".mpg", ".webm", ".ogm", ".ogv", ".ogx" ]),
            new FileData(WallpaperType.picture, [".jpg", ".jpeg", ".png",
                    ".bmp", ".tif", ".tiff", ".webp", ".jfif" ]),
            new FileData(WallpaperType.gif, [".gif"]),
            //new FileData(WallpaperType.heic, new string[] {".heic" }),//, ".heics", ".heif", ".heifs" }),
            new FileData(WallpaperType.web, [".html"]),
            new FileData(WallpaperType.webaudio, [".html"]),
            new FileData(WallpaperType.app, [".exe"]),
            //new FileFilter(WallpaperType.unity,"*.exe"),
            //new FileFilter(WallpaperType.unityaudio,"Unity Audio Visualiser |*.exe"),
            new FileData(WallpaperType.godot, [".exe"]),
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
            var item = SupportedFormats.FirstOrDefault(
                x => x.Extentions.Any(y => y.Equals(Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase)));

            return item != null ? item.Type : (WallpaperType)(-1);
        }
    }
}
