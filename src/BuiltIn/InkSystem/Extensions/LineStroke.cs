using BuiltIn.InkSystem.Core.Brushes;
using BuiltIn.InkSystem.Core.Services;
using Microsoft.Graphics.Canvas;

namespace BuiltIn.InkSystem.Extensions {
    public sealed partial record LineStroke : StrokeBase {
        public LineStroke(BrushGenerateArgs args) : base(args) { }

        public override void InitInkBrush(CanvasDevice device) {
            if (BrushArgs == null) return;
            InkBrush = BrushManager.GetBrush(BrushArgs, device);
        }
    }
}
