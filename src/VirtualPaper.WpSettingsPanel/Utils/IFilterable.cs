using System.Collections.Generic;
using VirtualPaper.Common;

namespace VirtualPaper.WpSettingsPanel.Utils {
    /// <summary>
    /// 多维度过滤条件。新增过滤维度时在此扩展，
    /// 无需改动 <see cref="IFilterable.ApplyFilter(FilterContext)"/> 签名。
    /// </summary>
    public record FilterContext {
        public static readonly FilterContext Empty = new();

        /// <summary>按标题关键词过滤；空字符串表示不过滤。</summary>
        public string TitleKeyword { get; init; } = string.Empty;

        /// <summary>接受的文件类型集合；null 或空集合表示接受所有类型。</summary>
        public IReadOnlySet<FileType>? ActiveTypes { get; init; }
    }

    public interface IFilterable {
        FilterKey FilterKeyword { get; set; }

        /// <summary>仅标题过滤（向后兼容快捷入口）。</summary>
        void ApplyFilter(string keyword);

        /// <summary>组合过滤：携带所有激活的过滤条件。</summary>
        void ApplyFilter(FilterContext context);
    }

    public enum FilterKey {
        LibraryTitle,
    }
}
