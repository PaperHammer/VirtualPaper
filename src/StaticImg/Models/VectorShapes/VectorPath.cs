using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using MessagePack;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Utils.Formatters;

namespace Workloads.Creation.StaticImg.Models.VectorShapes {
    // 最小化内存分配
    // 使用SIMD友好的Vector2类型
    // 内联关键转换方法
    // 支持所有WinUI路径线段类型
    [MessagePackObject]
    public partial class VectorPath : VectorShapeBase {
        [Key(5)]
        public PenLineJoin StrokeLineJoin { get; set; } = PenLineJoin.Round;
        [Key(6)]
        public PenLineCap StrokeStartLineCap { get; set; } = PenLineCap.Round;
        [Key(7)]
        public PenLineCap StrokeEndLineCap { get; set; } = PenLineCap.Round;
        [Key(8)]
        [MessagePackFormatter(typeof(PathGeometryFormatter))]
        public PathGeometry PathData { get; set; }
        [IgnoreMember]
        public override VectorShapeType ShapeType => VectorShapeType.Path;
        [IgnoreMember]
        public override Geometry GeometryData => PathData;

        public override Shape ToXamlShape() => new Path {
            Data = PathData,
            Fill = Fill,
            Stroke = Stroke,
            StrokeThickness = StrokeThickness,
            StrokeLineJoin = StrokeLineJoin,
            StrokeStartLineCap = StrokeStartLineCap,
            StrokeEndLineCap = StrokeEndLineCap,
            RenderTransform = RenderTransform
        };

        public override bool HitTest(Vector2 vectorPoint) {
            Point point = ToPoint(vectorPoint);

            // 1. 快速边界检查
            if (!GetCurrentBounds().Contains(point))
                return false;

            // 2. 精确命中检测
            try {
                var geometry = GetCachedGeometry();

                // 使用Vector2版本提高性能
                var win2DPoint = new Vector2(vectorPoint.X, vectorPoint.Y);

                // 填充检测
                if (Fill != null && geometry.FillContainsPoint(win2DPoint))
                    return true;

                // 描边检测
                if (Stroke != null) {
                    return geometry.StrokeContainsPoint(
                        win2DPoint,
                        (float)StrokeThickness,
                        GetCachedStrokeStyle());
                }

                return false;
            }
            catch (Exception ex) {
                Debug.WriteLine($"HitTest failed: {ex.Message}");
                return false;
            }
        }

        private Rect GetCurrentBounds() {
            if (_cachedBounds.IsEmpty && PathData != null) {
                _cachedBounds = PathData.Bounds;
            }
            return _cachedBounds;
        }

        private CanvasGeometry GetCachedGeometry() {
            // 检查是否需要更新缓存
            if (_cachedGeometry == null ||
                (PathData != null && _lastGeometryUpdate < PathData.GetLastUpdateTime())) {
                _cachedGeometry?.Dispose();
                _cachedGeometry = CreateCanvasGeometry();
                _cachedBounds = PathData?.Bounds ?? Rect.Empty;
                _lastGeometryUpdate = DateTime.Now;
            }
            return _cachedGeometry;
        }

        private CanvasGeometry CreateCanvasGeometry() {
            if (PathData == null) return null;

            using (var pathBuilder = new CanvasPathBuilder(CanvasDevice.GetSharedDevice())) {
                foreach (var figure in PathData.Figures) {
                    pathBuilder.BeginFigure((float)figure.StartPoint.X, (float)figure.StartPoint.Y);

                    foreach (var segment in figure.Segments) {
                        switch (segment) {
                            case LineSegment line:
                                pathBuilder.AddLine((float)line.Point.X, (float)line.Point.Y);
                                break;

                            case BezierSegment bezier:
                                var bezierPoint1 = ToVector2(bezier.Point1);
                                var bezierPoint2 = ToVector2(bezier.Point2);
                                var bezierPoint3 = ToVector2(bezier.Point3);
                                pathBuilder.AddCubicBezier(bezierPoint1, bezierPoint2, bezierPoint3);
                                break;

                            case QuadraticBezierSegment quad:
                                var quadPoint1 = ToVector2(quad.Point1);
                                var quadPoint2 = ToVector2(quad.Point2);
                                pathBuilder.AddQuadraticBezier(quadPoint1, quadPoint2);
                                break;

                            case ArcSegment arc:
                                pathBuilder.AddArc(
                                    ToVector2(arc.Point),
                                    (float)arc.Size.Width,
                                    (float)arc.Size.Height,
                                    (float)arc.RotationAngle,
                                    arc.SweepDirection == SweepDirection.Clockwise
                                        ? CanvasSweepDirection.Clockwise
                                        : CanvasSweepDirection.CounterClockwise,
                                    arc.IsLargeArc ? CanvasArcSize.Large : CanvasArcSize.Small);
                                break;
                        }
                    }

                    pathBuilder.EndFigure(figure.IsClosed ?
                        CanvasFigureLoop.Closed : CanvasFigureLoop.Open);
                }

                return CanvasGeometry.CreatePath(pathBuilder);
            }
        }

        private static CanvasStrokeStyle GetCachedStrokeStyle() {
            // 静态缓存常用笔刷样式
            return new CanvasStrokeStyle {
                StartCap = CanvasCapStyle.Round,
                EndCap = CanvasCapStyle.Round,
                LineJoin = CanvasLineJoin.Round,
                DashStyle = CanvasDashStyle.Solid
            };
        }

        public void InvalidateCache() {
            _cachedGeometry?.Dispose();
            _cachedGeometry = null;
            _cachedBounds = Rect.Empty;
        }

        ~VectorPath() {
            InvalidateCache();
        }

        /*
         * [MethodImpl(MethodImplOptions.AggressiveInlining)] 是 .NET 中的一个重要性能优化特性，它的作用和工作原理如下：

            核心作用
            告诉 JIT（即时编译器）尽可能将该方法内联到调用处，消除方法调用的开销。这是 .NET 中最直接的微观性能优化手段之一。

            关键效果
            消除调用开销：
                省去方法调用的 call 指令（约2-6时钟周期）
                避免栈帧创建/销毁（约5-10周期）

            启用进一步优化：
                内联后编译器能进行常量传播、死代码消除等优化
                对小型方法可提升20-50%性能
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 ToVector2(Point point) {
            return new Vector2((float)point.X, (float)point.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Point ToPoint(Vector2 vector) {
            return new Point(vector.X, vector.Y);
        }

        private CanvasGeometry _cachedGeometry;
        private Rect _cachedBounds;
        private DateTime _lastGeometryUpdate = DateTime.MinValue;
    }
}
