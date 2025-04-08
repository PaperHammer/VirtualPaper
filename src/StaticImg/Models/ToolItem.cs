using System;
using Microsoft.UI.Input;

namespace Workloads.Creation.StaticImg.Models {
    class ToolItem : IEquatable<ToolItem> {
        public ToolType Type { get; set; }
        public InputSystemCursor Cursor { get; set; }
        public string Glyph { get; set; } // 图标字符（FontIcon 的 Glyph）
        public string ImageSourceKey { get; set; } // 图片路径（ImageIcon 的 Source）
        public string ToolName { get; set; } // 功能名称或标识符
        public Action OnSelected { get; set; } // 选中时的触发逻辑
        public Action OnDeselected { get; set; } // 取消选中时的触发逻辑（清理工具影响）

        public string TypeString => Type.ToString();

        public bool Equals(ToolItem other) {
            return this.Type == other.Type;
        }

        public override bool Equals(object obj) {
            return Equals(obj as ToolItem);
        }

        public override int GetHashCode() {
            return Type.GetHashCode();
        }
    }
}
