using System.Numerics;
using BuiltIn.InkSystem.Core.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using VirtualPaper.Shader;
using Windows.Foundation;

namespace BuiltIn.InkSystem.Core.Brushes {
    public abstract record StrokeBase {
        public BrushGenerateArgs BrushArgs { get; init; }
        public ICanvasImage? InkImage { get; set; }
        public ICanvasBrush? InkBrush { get; set; }
        public ShaderType Type { get; set; }
        public List<Vector2> Points { get; set; } = [];
        public bool ShouldRender => Points.Count > 0;
        public bool IsSinglePoint => Points.Count == 1;
        public virtual CanvasStrokeStyle Style => new() {
            StartCap = CanvasCapStyle.Round,
            EndCap = CanvasCapStyle.Round,
            LineJoin = CanvasLineJoin.Round
        };
        public virtual bool IsEraser => false;

        public StrokeMode EffectMode { get; set; }

        protected StrokeBase(BrushGenerateArgs brushArgs) {
            BrushArgs = brushArgs;
        }

        //public virtual void InitInkImage() { }

        public virtual void InitInkBrush(CanvasDevice device) { }

        public virtual void InitPixelsEffect(ShaderType type) {
            Type = type;
        }

        public Rect GetBounds() {
            if (Points.Count == 0)
                return Rect.Empty;

            float minX = Points[0].X;
            float maxX = Points[0].X;
            float minY = Points[0].Y;
            float maxY = Points[0].Y;

            for (int i = 1; i < Points.Count; i++) {
                var p = Points[i];
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }

            // 边界加上笔触厚度范围
            float half = BrushArgs.Thickness / 2f + 2f; // 适当加2像素冗余

            return new Rect(minX - half, minY - half, (maxX - minX) + half * 2, (maxY - minY) + half * 2);
        }

        public CanvasGeometry CreateStrokeGeometry(CanvasDevice device) {
            if (Points.Count == 1)
                return CanvasGeometry.CreateCircle(device, Points[0], BrushArgs.Thickness / 2);

            using var builder = new CanvasPathBuilder(device);
            builder.BeginFigure(Points[0]);
            for (int i = 1; i < Points.Count - 1; i++) {
                var mid = (Points[i] + Points[i + 1]) / 2;
                builder.AddQuadraticBezier(Points[i], mid);
            }
            builder.AddLine(Points[^1]);
            builder.EndFigure(CanvasFigureLoop.Open);

            return CanvasGeometry.CreatePath(builder);
        }

        public virtual void Render(CanvasDrawingSession dsTemp, CanvasGeometry geometry, Rect? bounds, CanvasRenderTarget? snapshotRenderTarget) { }        

        protected byte[]? PixelsEffectBytes => ShaderLoader.GetShader(Type);
    }

    public enum StrokeMode {
        Normal,
        Copy,
        AddEffect,
    }
}
