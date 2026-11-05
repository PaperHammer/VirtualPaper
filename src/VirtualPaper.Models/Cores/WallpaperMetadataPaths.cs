using System.IO;

namespace VirtualPaper.Models.Cores {
    /// <summary>Wallpaper JSON paths are relative to their JSON file, never the process working directory.</summary>
    internal static class WallpaperMetadataPaths {
        internal static string DirectoryOf(string metadataPath) => Path.GetDirectoryName(Path.GetFullPath(metadataPath))!;

        internal static string Resolve(string directory, string path) =>
            string.IsNullOrEmpty(path) ? path : Path.GetFullPath(path, directory);

        // Cross-volume external references (temporary editor/preview data) cannot
        // be relative. Files inside a wallpaper folder are always made relative.
        internal static string Relative(string directory, string path) =>
            string.IsNullOrEmpty(path) ? path : Path.GetRelativePath(directory, Resolve(directory, path)).Replace('\\', '/');
    }
}
