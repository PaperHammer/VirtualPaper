using System;

namespace VirtualPaper.IntelligentPanel.Models {
    public class StyleOptionItem : IEquatable<StyleOptionItem> {
        public string? Name { get; set; }
        public string? ThumbnailResourceKey { get; set; }
        public string? ImagePath { get; set; }
        public string? FileSize { get; internal set; }
        public string? FileExt { get; internal set; }
        public bool IsCustom { get; internal set; }

        public bool Equals(StyleOptionItem? other) {
            return other != null && other.ImagePath == this.ImagePath;
        }

        public override bool Equals(object? obj) {
            return Equals(obj as StyleOptionItem);
        }

        public override int GetHashCode() {
            return ImagePath?.GetHashCode() ?? 0;
        }
    }
}
