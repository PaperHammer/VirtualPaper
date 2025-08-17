using System;
using BuiltIn.InkSystem.Core.Services;
using Microsoft.UI.Xaml.Shapes;

namespace Workloads.Creation.StaticImg.Models {
    public class PaintBrushItem : IEquatable<PaintBrushItem> {
        public string Name { get; set; } = string.Empty;
        public BrushType Type { get; set; }
        public Path? Example { get; set; }
        public string ConfigKey { get; set; } = string.Empty;

        public bool Equals(PaintBrushItem? other) {
            return other != null && this.Type == other.Type;
        }

        public override bool Equals(object? obj) {
            return Equals(obj as PaintBrushItem);
        }

        public override int GetHashCode() {
            return Type.GetHashCode();
        }
    }
}
