using VirtualPaper.Common;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    public sealed record WallpaperDirectoryDefinition(
        string FolderName,
        Func<ISettings, string> GetPath,
        Action<ISettings, string> SetPath);

    /// <summary>
    /// Library roots scanned together and moved by the single directory picker.
    /// Register additional roots here to include them in scanning and migration.
    /// Each folder name is a single path component beneath the selected root.
    /// </summary>
    public static class WallpaperLibraryDirectories {
        public static IReadOnlyList<WallpaperDirectoryDefinition> All { get; } =
            Array.AsReadOnly<WallpaperDirectoryDefinition>([
                new(Constants.FolderName.WpStoreFolderName,
                    settings => settings.WallpaperDir,
                    (settings, path) => settings.WallpaperDir = path),
                new(Constants.FolderName.BuiltInWallpapersFolderName,
                    settings => settings.BuiltInWallpapersDir,
                    (settings, path) => settings.BuiltInWallpapersDir = path)
            ]);
    }
}
