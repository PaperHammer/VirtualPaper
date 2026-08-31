using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using VirtualPaper.Shader;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Core.Utils;

namespace Workloads.Creation.StaticImg.Core.Brushes {
    public abstract record StrokeBase {
        public BrushGenerateArgs BrushArgs { get; init; }
        public ICanvasImage? InkImage { get; set; }
        public ICanvasBrush? InkBrush { get; set; }
        public ShaderType Type { get; set; }
        public List<Vector2> Points { get; set; } = [];
        public bool ShouldRender => Points.Count > 0;
        public bool IsSinglePoint => Points.Count == 1;
        public virtual CanvasStrokeStyle Style { get; } = new() {
            StartCap = CanvasCapStyle.Round,
            EndCap = CanvasCapStyle.Round,
            LineJoin = CanvasLineJoin.Round
        };
        public virtual bool IsEraser => false;

        public StrokeMode EffectMode { get; set; }

        protected StrokeBase(BrushGenerateArgs brushArgs) {
            BrushArgs = brushArgs;
        }

        public virtual void InitInkBrush(CanvasDevice device) { }

        public virtual void InitPixelsEffect(ShaderType type) {
            Type = type;
        }

        public Rect GetBounds() => GetBounds(Points);

        public Rect GetBounds(IReadOnlyList<Vector2> points) {
            if (points.Count == 0)
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

            // 基础半径
            float halfStroke = BrushArgs.Thickness / 2f;

            // 安全边距 (Padding)
            // 建议增加到 5.0f 或直接使用 BrushArgs.Thickness (如果存在锐角连接)
            // 抗锯齿通常需要额外 1px，Miter Join 可能需要更多。
            float padding = 5.0f;

            // 计算浮点边界
            float left = minX - halfStroke - padding;
            float top = minY - halfStroke - padding;
            float right = maxX + halfStroke + padding;
            float bottom = maxY + halfStroke + padding;

            // 像素对齐（向外取整）
            // 左上角向下取整 (Floor)，右下角向上取整 (Ceiling)
            // 这样保证 Rect 包含所有涉及的子像素
            double alignedLeft = Math.Floor(left);
            double alignedTop = Math.Floor(top);
            double alignedRight = Math.Ceiling(right);
            double alignedBottom = Math.Ceiling(bottom);

            return new Rect(
                alignedLeft,
                alignedTop,
                alignedRight - alignedLeft, // Width
                alignedBottom - alignedTop  // Height
            );
        }

        public CanvasGeometry CreateStrokeGeometry(CanvasDevice device) {
            return CreateStrokeGeometry(device, Points);
        }

        public CanvasGeometry CreateStrokeGeometry(CanvasDevice device, IReadOnlyList<Vector2> points) {
            return CreateStrokeGeometry(device, points, false, true);
        }

        /// <summary>
        /// 根据采样点创建平滑笔画几何，并支持构建分块笔画的中间段。
        /// 分块时，相邻块在两个采样点的中点处衔接，以保持二次贝塞尔曲线连续。
        /// </summary>
        /// <param name="device">用于创建几何资源的 Win2D 设备。</param>
        /// <param name="points">参与当前几何段构建的有序采样点。</param>
        /// <param name="startsAtLeadingMidpoint">
        /// 是否从前两个采样点的中点开始。首个笔画块传 <see langword="false"/>，
        /// 后续块传 <see langword="true"/>，从上一块的结束中点继续绘制。
        /// </param>
        /// <param name="includesFinalSegment">
        /// 是否从最后一个贝塞尔中点连接到末尾采样点。活动块或完整笔画传
        /// <see langword="true"/>；提交到稳定缓存的中间块传 <see langword="false"/>，
        /// 避免生成一条下一块还会重新计算的末尾直线。
        /// </param>
        /// <returns>调用方负责释放的笔画几何。</returns>
        public CanvasGeometry CreateStrokeGeometry(
            CanvasDevice device,
            IReadOnlyList<Vector2> points,
            bool startsAtLeadingMidpoint,
            bool includesFinalSegment) {
            if (points.Count == 1)
                return CanvasGeometry.CreateCircle(device, points[0], BrushArgs.Thickness / 2);

            using var builder = new CanvasPathBuilder(device);
            builder.BeginFigure(startsAtLeadingMidpoint
                ? (points[0] + points[1]) / 2
                : points[0]);
            for (int i = 1; i < points.Count - 1; i++) {
                var mid = (points[i] + points[i + 1]) / 2;
                builder.AddQuadraticBezier(points[i], mid);
            }
            if (includesFinalSegment)
                builder.AddLine(points[^1]);
            builder.EndFigure(CanvasFigureLoop.Open);

            return CanvasGeometry.CreatePath(builder);
        }

        // 增量生成 (负责将笔触几何体绘制到 dsTemp)
        // 这是所有笔刷和擦除工具必须实现的
        public abstract void RenderIncrement(
            CanvasDrawingSession dsTemp,
            CanvasGeometry geometry
        );

        // 图像混合/合成 (负责混合 TempRT 和 SnapshotRT)
        // 这只在工具需要复杂混合时才使用 (如擦除)
        public abstract ICanvasImage MergeImages(
            ICanvasImage foreground,
            ICanvasImage background,
            CanvasDevice device
        );

        protected byte[]? PixelsEffectBytes => ShaderLoader.GetShader(Type);
    }

    public enum StrokeMode {
        Normal,
        Copy,
        AddEffect,
    }
}
