using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common.Extensions;
using Windows.Foundation;
using Windows.UI;

namespace Workloads.Creation.StaticImg.Models.ToolItems.Base {
    public abstract class StrokeBase {
        public List<Vector2> Points { get; } = [];
        public Color Color { get; set; }
        public float Thickness { get; set; }
        public ICanvasImage? Brush { get; set; }

        public abstract void Draw(CanvasDrawingSession Ds);

        // 计算当前 Stroke 受影响的区域 (包围盒)
        public virtual Rect GetAffectedArea() {
            if (Points.Count == 0) return Rect.Empty;

            float padding = Thickness / 2;
            if (Points.Count == 1) {
                // 对于单点情况，计算点的影响区域
                return new Rect(Points[0].X - padding, Points[0].Y - padding, Thickness, Thickness);
            }

            var minX = Points[0].X;
            var minY = Points[0].Y;
            var maxX = minX;
            var maxY = minY;

            foreach (var point in Points) {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            // 笔刷厚度的影响
            return new Rect(minX - padding, minY - padding, maxX - minX + 2 * padding, maxY - minY + 2 * padding);
        }
    }
}
