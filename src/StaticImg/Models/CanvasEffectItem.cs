using System.Collections.ObjectModel;

namespace Workloads.Creation.StaticImg.Models {
    /// <summary>
    /// 单个画布效果项
    /// </summary>
    public class CanvasEffectItem {
        /// <summary>效果唯一标识符</summary>
        public string EffectId { get; set; } = string.Empty;

        /// <summary>效果显示名称（i18n key）</summary>
        public string NameKey { get; set; } = string.Empty;

        /// <summary>效果预览图路径（ms-appx:// 或 ms-appdata://）</summary>
        public string PreviewImagePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// 效果分组（对应 Accordion 中的一个 Expander）
    /// </summary>
    public class CanvasEffectGroup {
        /// <summary>分组标题（i18n key）</summary>
        public string GroupNameKey { get; set; } = string.Empty;

        /// <summary>该分组下的效果列表</summary>
        public ObservableCollection<CanvasEffectItem> Items { get; set; } = [];
    }
}
