using BuiltIn.InkSystem.Core.Brushes;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation;

namespace BuiltIn.InkSystem.Extensions {
    /// <summary>
    /// 使用ICanvasBrush的线条绘制实现
    /// </summary>
    public sealed partial class LineStroke : StrokeRenderer {
        public override void Draw(CanvasDrawingSession ds) {
            if (Points.Count == 0 || InkBrush == null) return;

            //单点绘制
            if (Points.Count == 1) {
                switch (Shape) {
                    case Core.Services.BrushShape.Circle:
                        ds.FillCircle(Points[0], Thickness / 2, InkBrush);
                        break;
                    case Core.Services.BrushShape.Rectangle:
                        var rect = new Rect(
                            Points[0].X - Thickness / 2,
                            Points[0].Y - Thickness / 2,
                            Thickness,
                            Thickness);
                        ds.FillRectangle(rect, InkBrush);
                        break;
                    case Core.Services.BrushShape.RoundedRect:
                        break;
                    default:
                        break;
                }

                return;
            }

            // 构建平滑几何路径
            using (var path = new CanvasPathBuilder(ds.Device)) {
                path.BeginFigure(Points[0]);

                // 使用贝塞尔曲线连接点
                for (int i = 1; i < Points.Count; i++) {
                    if (ShouldUseBezier(i)) {
                        AddBezierSegment(path, i);
                    }
                    else {
                        path.AddLine(Points[i]);
                    }
                }

                path.EndFigure(CanvasFigureLoop.Closed);
                var geometry = CanvasGeometry.CreatePath(path);

                // 使用笔刷绘制路径
                ds.DrawGeometry(
                    geometry,
                    InkBrush,
                    Thickness,
                    GetStrokeStyle());
            }
        }
    }
}
