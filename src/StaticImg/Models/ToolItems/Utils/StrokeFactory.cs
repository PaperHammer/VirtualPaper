using System;
using Workloads.Creation.StaticImg.Models.ToolItems.Base;
using Workloads.Creation.StaticImg.Models.ToolItems.StrokeTools;

namespace Workloads.Creation.StaticImg.Models.ToolItems.Utils {
    public static class StrokeFactory {
        public static StrokeBase CreateStroke(PaintBrushType type) {
            return type switch {
                PaintBrushType.CommonBrush => new LineStroke(),
                //StrokeType.Line => new LineStroke(),
                //StrokeType.Texture => new TextureStroke(),
                //StrokeType.Pressure => new PressureStroke(),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }
    }

    //public enum StrokeType { Line, Texture, Pressure }
}
