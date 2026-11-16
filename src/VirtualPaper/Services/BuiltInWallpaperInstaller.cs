using System.IO;
using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.Services;

/// <summary>MSIX has no install script; deploy its bundled wallpapers as writable user data.</summary>
internal static class BuiltInWallpaperInstaller {
    internal static void InstallMissing(string sourceRoot, string destinationRoot) {
        if (!Directory.Exists(sourceRoot)) return;
        Directory.CreateDirectory(destinationRoot);

        foreach (string source in Directory.EnumerateDirectories(sourceRoot)) {
            string destination = Path.Combine(destinationRoot, Path.GetFileName(source));
            // Match Inno Setup: preserve the whole existing wallpaper folder,
            // including files the user deliberately deleted from that folder.
            if (Directory.Exists(destination)) continue;

            string staging = Path.Combine(destinationRoot, $".msix-builtin-{Guid.NewGuid():N}");
            try {
                FileUtil.CopyDirectory(source, staging, copySubDirs: true, overwrite: false);
                Directory.Move(staging, destination);
            }
            finally {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
            }
        }
    }
}
