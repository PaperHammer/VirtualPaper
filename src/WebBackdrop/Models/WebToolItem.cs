using System;

namespace Workloads.Creation.WebBackdrop.Models {
    public class WebToolItem : IEquatable<WebToolItem> {
        public WebToolType Type { get; set; }
        public string Glyph { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;

        public bool Equals(WebToolItem? other) => other != null && Type == other.Type;

        public override bool Equals(object? obj) => Equals(obj as WebToolItem);

        public override int GetHashCode() => Type.GetHashCode();
    }

    public enum WebToolType {
        FileTree,
        ProjectInfo,
    }
}
