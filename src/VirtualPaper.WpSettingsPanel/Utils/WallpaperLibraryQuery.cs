using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtualPaper.WpSettingsPanel.Utils {
    internal static class WallpaperLibraryQuery {
        // Shared by the full index and the already-loaded UI cache.
        internal static IEnumerable<WallpaperIndexEntry> Apply(IEnumerable<WallpaperIndexEntry> entries,
            FilterContext filter, Func<string, WallpaperOrganization> organization) {
            string keyword = filter.TitleKeyword.Trim();
            string tagKeyword = filter.Tag.Trim();
            var matching = entries.Where(entry => {
                var personal = organization(entry.Uid);
                var tags = entry.Tags.Concat(personal.Tags);
                return (!filter.FavoritesOnly || personal.Favorite)
                    && (filter.ActiveTypes == null || filter.ActiveTypes.Count == 0 || filter.ActiveTypes.Contains(entry.FType))
                    && (tagKeyword.Length == 0 || tags.Any(tag => Contains(tag, tagKeyword)))
                    && (keyword.Length == 0 || Contains(entry.Title, keyword) || Contains(entry.Description, keyword)
                        || Contains(entry.Author, keyword) || tags.Any(tag => Contains(tag, keyword)));
            });
            IOrderedEnumerable<WallpaperIndexEntry> sorted = filter.SortOrder switch {
                WallpaperSortOrder.Oldest => matching.OrderBy(entry => entry.CreateTime),
                WallpaperSortOrder.Title => matching.OrderBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase),
                WallpaperSortOrder.Author => matching.OrderBy(entry => entry.Author, StringComparer.CurrentCultureIgnoreCase),
                _ => matching.OrderByDescending(entry => entry.CreateTime)
            };
            return sorted.ThenBy(entry => entry.Uid, StringComparer.OrdinalIgnoreCase);
        }

        private static bool Contains(string? value, string keyword) =>
            value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }
}
