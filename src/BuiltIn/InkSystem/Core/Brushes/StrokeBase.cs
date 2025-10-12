using System.Numerics;
using BuiltIn.InkSystem.Core.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation;

namespace BuiltIn.InkSystem.Core.Brushes {
    public abstract record StrokeBase {
        public BrushGenerateArgs BrushArgs { get; init; }
        public ICanvasImage? InkImage { get; set; }
        public ICanvasBrush? InkBrush { get; set; }
        public ICanvasBrush? EffectBrush { get; set; }
        public virtual CanvasStrokeStyle Style => new() {
            StartCap = CanvasCapStyle.Round,
            EndCap = CanvasCapStyle.Round,
            LineJoin = CanvasLineJoin.Round
        };
        public virtual bool IsEraser => false;

        //public bool IsAddEffectWithCopy { get; set; }  // 是否以 Copy 形式附加额外效果
        //public bool IsAddEffect { get; set; }  // 是否附加额外效果
        //public bool IsOnlyEffect { get; set; } // 是否只渲染效果层
        public StrokeMode EffectMode { get; set; }

        protected StrokeBase(BrushGenerateArgs brushArgs) {
            BrushArgs = brushArgs;
        }

        public Rect GetBounds(IReadOnlyList<Vector2> points) {
            if (points == null || points.Count == 0)
                return Rect.Empty;

            float minX = points[0].X;
            float maxX = points[0].X;
            float minY = points[0].Y;
            float maxY = points[0].Y;

            for (int i = 1; i < points.Count; i++) {
                var p = points[i];
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }

            // 边界加上笔触厚度范围
            float half = BrushArgs.Thickness / 2f + 2f; // 适当加2像素冗余

            return new Rect(minX - half, minY - half, (maxX - minX) + half * 2, (maxY - minY) + half * 2);
        }

        public virtual void InitInkImage() { }

        public virtual void InitInkBrush(CanvasDevice device) { }

        /// <summary>
        /// 绘制主笔迹
        /// </summary>
        public virtual void DrawStroke(CanvasDrawingSession ds, CanvasGeometry geometry, bool isSinglePoint = false) {
            if (InkBrush == null) return;
            
            ds.Blend = CanvasBlend.SourceOver;
            if (isSinglePoint) {
                ds.FillGeometry(geometry, InkBrush);
            }
            else {
                ds.DrawGeometry(geometry, InkBrush, BrushArgs.Thickness, Style);
            }
        }

        /// <summary>
        /// 应用额外效果（擦除、淡化、发光等）
        /// </summary>
        public virtual void ApplyEffect(CanvasDrawingSession ds, CanvasGeometry geometry, Rect dirty, bool isSinglePoint = false) { }

        /// <summary>
        /// 应用额外效果（擦除、淡化、发光等）需要从快照中获取内容
        /// </summary>
        public virtual void ApplyEffectWithCopy(CanvasRenderTarget source, CanvasDrawingSession ds, CanvasGeometry geometry, Rect dirty, bool isSinglePoint = false) { }
    }

    [Flags]
    public enum StrokeMode {
        /// <summary>
        /// 正常笔触（DrawStroke）
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 仅渲染效果层，不绘制笔迹
        /// </summary>
        OnlyEffect = 1 << 0,

        /// <summary>
        /// 附加额外效果（ApplyEffect）
        /// </summary>
        AddEffect = 1 << 1,

        /// <summary>
        /// 以 Copy 的方式覆盖目标（ApplyEffectWithCopy）
        /// </summary>
        AddEffectWithCopy = 1 << 2,
    }
}
