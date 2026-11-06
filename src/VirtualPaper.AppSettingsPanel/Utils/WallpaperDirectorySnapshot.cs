using System;
using System.Collections.Generic;
using System.IO;

namespace VirtualPaper.AppSettingsPanel.Utils {
    /// <summary>
    /// Captures directory entries and file metadata without reading file contents.
    /// This detects ordinary source changes, but is not a filesystem transaction.
    /// </summary>
    internal sealed class WallpaperDirectorySnapshot {
        // Compatibility bound for shell/native consumers, not the .NET path limit.
        internal const int MaxMigrationPathLength = 259;
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
        private int _longestRelativePath;
        private readonly record struct Entry(long? Length, DateTime LastWriteTimeUtc);

        internal static WallpaperDirectorySnapshot Capture(string directory) {
            var snapshot = new WallpaperDirectorySnapshot();
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", SearchOption.AllDirectories)) {
                string relative = Path.GetRelativePath(directory, entry.FullName);
                snapshot._longestRelativePath = Math.Max(snapshot._longestRelativePath, relative.Length);
                snapshot._entries.Add(relative, entry is FileInfo file
                    ? new Entry(file.Length, file.LastWriteTimeUtc) : new Entry(null, default));
            }
            return snapshot;
        }

        internal static void ValidatePathLength(string destination, int relativeLength) {
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
            int separator = relativeLength > 0 && !Path.EndsInDirectorySeparator(root) ? 1 : 0;
            if ((long)root.Length + separator + relativeLength > MaxMigrationPathLength)
                throw new PathTooLongException($"The target wallpaper path exceeds the {MaxMigrationPathLength}-character compatibility limit. Choose a shorter destination.");
        }

        internal void ValidateDestinationPathLength(string target) => ValidatePathLength(target, _longestRelativePath);

        internal void VerifyCopy(string target) {
            var copied = Capture(target);
            if (_entries.Count != copied._entries.Count)
                throw new IOException($"The copied wallpaper directory is incomplete: {target}");
            foreach (var entry in _entries) {
                if (!copied._entries.TryGetValue(entry.Key, out var copy) || entry.Value.Length != copy.Length)
                    throw new IOException($"Copied wallpaper entry is missing or has a different size: {Path.Combine(target, entry.Key)}");
            }
        }

        internal bool IsUnchanged(string source) {
            var current = Capture(source);
            if (_entries.Count != current._entries.Count) return false;
            foreach (var entry in _entries) {
                if (!current._entries.TryGetValue(entry.Key, out var value) || value != entry.Value) return false;
            }
            return true;
        }
    }
}
