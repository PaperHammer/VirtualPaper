using System;

namespace Workloads.Creation.StaticImg.Models {
    class ToolItem {
        public string Glyph { get; set; } // 图标字符（FontIcon 的 Glyph）
        public string ImageSourceKey { get; set; } // 图片路径（ImageIcon 的 Source）
        public string ToolName { get; set; } // 功能名称或标识符
        public Action OnSelected { get; set; } // 选中时的触发逻辑
        public Action OnDeselected { get; set; } // 取消选中时的触发逻辑（清理工具影响）
    }
}
