using System.Numerics;
using BuiltIn.InkSystem.Core.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation;

namespace BuiltIn.InkSystem.Core.Brushes {
    /// <summary>
    /// 绘制方式基类
    /// </summary>
    public abstract class StrokeRenderer : IDisposable {
        public float Thickness { get; set; }
        public BrushShape Shape { get; set; }
        public ICanvasImage? InkImage { get; set; }
        public ICanvasBrush? InkBrush { get; set; }
        public IList<Vector2> Points { get; } = [];

        // 绘制
        public abstract void Draw(CanvasDrawingSession ds);

        // 扩展点
        public virtual void UpdatePressure(IReadOnlyList<float> pressureData) { }
        public virtual void UpdateTilt(Vector2 tilt) { }

        public virtual void Reset(float thickness, BrushShape shape) {
            Thickness = thickness;
            Shape = shape;
            Points.Clear();
        }

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

        protected bool ShouldUseBezier(int index) =>
            index > 1 && index < Points.Count - 1;

        protected void AddBezierSegment(CanvasPathBuilder path, int currentIndex) {
            Vector2 p0 = Points[currentIndex - 2];
            Vector2 p1 = Points[currentIndex - 1];
            Vector2 p2 = Points[currentIndex];
            Vector2 p3 = Points[currentIndex + 1];

            float tension = GetDynamicTension(p0, p1, p2, p3);
            Vector2 cp1 = p1 + (p2 - p0) * tension;
            Vector2 cp2 = p2 - (p3 - p1) * tension;

            path.AddCubicBezier(cp1, cp2, p2);
        }

        protected static CanvasStrokeStyle GetStrokeStyle() => new() {
            StartCap = CanvasCapStyle.Round,
            EndCap = CanvasCapStyle.Round,
            LineJoin = CanvasLineJoin.Round,
            DashCap = CanvasCapStyle.Round
        };

        private static float GetDynamicTension(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3) {
            float distancePrev = Vector2.Distance(p1, p0);
            float distanceNext = Vector2.Distance(p2, p1);
            float avgDistance = (distancePrev + distanceNext) / 2;

            return Math.Min(0.5f, (float)BaseTension * (avgDistance / 10f));
        }

        public virtual void Dispose() {
            GC.SuppressFinalize(this);
            InkBrush?.Dispose();
        }

        protected const double BaseTension = 0.2;
    }
}
