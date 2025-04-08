using System;
using Microsoft.UI.Xaml.Shapes;

namespace Workloads.Creation.StaticImg.Models {
    class PaintBrushItem : IEquatable<PaintBrushItem> {
        public string Name { get; set; } = string.Empty;
        public PaintBrushType Type { get; set; }
        public Path Example { get; set; }
        public string ConfigKey { get; set; } = string.Empty;

        public bool Equals(PaintBrushItem other) {
            return this.Type == other.Type;
        }

        public override bool Equals(object obj) {
            return Equals(obj as PaintBrushItem);
        }

        public override int GetHashCode() {
            return Type.GetHashCode();
        }
    }
}
