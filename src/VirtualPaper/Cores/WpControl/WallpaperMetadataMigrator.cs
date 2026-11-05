using System.Globalization;
using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Cores.WpControl;

/// <summary>Runs wallpaper format migrations in version order without reimporting installed media.</summary>
internal static class WallpaperMetadataMigrator {
    internal static async Task<IWpBasicData> MigrateAsync(
        string folderPath,
        Func<WpBasicData, IWpBasicData> rebuildLegacy,
        CancellationToken token = default) {
        token.ThrowIfCancellationRequested();
        var data = await WpBasicData.LoadAsync(Path.Combine(folderPath, Constants.Field.WpBasicDataFileName))
            ?? throw new InvalidDataException("Wallpaper metadata is empty.");
        int version = ReadVersion(data.AppInfo.FileVersion);
        int currentVersion = ReadVersion(Constants.CoreField.FileVersion);
        if (version > currentVersion)
            throw new NotSupportedException("Wallpaper metadata was created by a newer version.");

        token.ThrowIfCancellationRequested();
        if (version < 18)
            return rebuildLegacy(data);

        while (version < currentVersion) {
            token.ThrowIfCancellationRequested();
            // Add each new migration here; never skip an unknown format change.
            switch (version) {
                case 18:
                    await Migrate18To19Async(folderPath, data, token);
                    version = 19;
                    break;
                default:
                    throw new NotSupportedException($"No wallpaper migration is available from version {version}.");
            }
        }
        return data;
    }

    private static async Task Migrate18To19Async(string folderPath, WpBasicData data, CancellationToken token) {
        // Load and validate every runtime before writing. A retry may find runtimes
        // already at 19, while the basic metadata still records 18.
        List<WpRuntimeData> runtimes = [];
        foreach (string path in Directory.EnumerateFiles(folderPath, Constants.Field.WpRuntimeDataFileName, SearchOption.AllDirectories)) {
            token.ThrowIfCancellationRequested();
            var runtime = await WpRuntimeData.LoadAsync(path);
            if (ReadVersion(runtime.AppInfo.FileVersion) > 19)
                throw new NotSupportedException("Runtime metadata is newer than the migration target.");
            runtimes.Add(runtime);
        }
        foreach (var runtime in runtimes) {
            token.ThrowIfCancellationRequested();
            runtime.Save("19");
        }
        // Commit the basic version last. Keep identity, media, effects and Web manifests.
        token.ThrowIfCancellationRequested();
        await data.SaveMetadataAsync("19");
    }

    private static int ReadVersion(string? value) {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int version) || version < 1)
            throw new InvalidDataException($"Invalid wallpaper metadata version: '{value}'.");
        return version;
    }
}
