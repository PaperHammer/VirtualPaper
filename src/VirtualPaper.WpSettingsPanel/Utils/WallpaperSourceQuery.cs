using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtualPaper.WpSettingsPanel.Utils;

// One lightweight snapshot per open picker: sort and read favorites once, then filter in memory.
internal sealed class WallpaperSourceQuery {
    private readonly WallpaperIndexEntry[] _ordered;
    private readonly HashSet<string> _favorites;

    internal WallpaperSourceQuery(IEnumerable<WallpaperIndexEntry> entries, Func<string, bool> isFavorite) {
        _ordered = entries.OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.Uid, StringComparer.OrdinalIgnoreCase).ToArray();
        _favorites = _ordered.Where(entry => isFavorite(entry.Uid)).Select(entry => entry.Uid)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    internal IReadOnlyList<WallpaperIndexEntry> Search(string keyword, bool favoritesOnly) {
        keyword = keyword.Trim();
        if (keyword.Length == 0 && !favoritesOnly) return _ordered;
        return _ordered.Where(entry => (keyword.Length == 0 || Contains(entry.DisplayName, keyword) || Contains(entry.Author, keyword))
            && (!favoritesOnly || _favorites.Contains(entry.Uid))).ToArray();
    }

    private static bool Contains(string? value, string keyword) => value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
}
