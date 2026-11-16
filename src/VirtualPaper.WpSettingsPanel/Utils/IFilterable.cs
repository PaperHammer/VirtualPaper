using System.Collections.Generic;
using VirtualPaper.Common;

namespace VirtualPaper.WpSettingsPanel.Utils {
    /// <summary>
    /// 多维度过滤条件。新增过滤维度时在此扩展，
    /// 无需改动 <see cref="IFilterable.ApplyFilter(FilterContext)"/> 签名。
    /// </summary>
    public record FilterContext {
        public static readonly FilterContext Empty = new();

        /// <summary>Searches title, description, author and tags; empty means all.</summary>
        public string TitleKeyword { get; init; } = string.Empty;

        /// <summary>接受的文件类型集合；null 或空集合表示接受所有类型。</summary>
        public IReadOnlySet<FileType>? ActiveTypes { get; init; }
        /// <summary>Case-insensitive substring of a source or personal tag; empty means all.</summary>
        public string Tag { get; init; } = string.Empty;
        public bool FavoritesOnly { get; init; }
        public WallpaperSortOrder SortOrder { get; init; } = WallpaperSortOrder.Newest;
    }

    public enum WallpaperSortOrder { Newest, Oldest, Title, Author }

    public interface IFilterable {
        FilterKey FilterKeyword { get; set; }

        /// <summary>Keyword search (keeps the existing quick-search entry point).</summary>
        void ApplyFilter(string keyword);

        /// <summary>组合过滤：携带所有激活的过滤条件。</summary>
        void ApplyFilter(FilterContext context);
    }

    public enum FilterKey {
        LibraryTitle,
    }
}
