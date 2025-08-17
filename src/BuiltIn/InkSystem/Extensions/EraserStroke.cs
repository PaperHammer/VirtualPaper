using System.Numerics;
using BuiltIn.InkSystem.Core.Brushes;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Windows.Foundation;

namespace BuiltIn.InkSystem.Extensions {
    public sealed partial class EraserStroke : StrokeRenderer {
        public override void Draw(CanvasDrawingSession ds) {
            if (Points.Count == 0) return;

            // 使用AlphaMaskEffect实现擦除
            using (var cmdList = new CanvasCommandList(ds.Device))
            using (var maskSession = cmdList.CreateDrawingSession()) {
                // 在蒙版上绘制白色方形擦除区域
                maskSession.Clear(Colors.Transparent);

                if (Points.Count > 1) {
                    // 绘制连接的方形线段
                    for (int i = 1; i < Points.Count; i++) {
                        DrawSquareSegment(maskSession, Points[i - 1], Points[i]);
                    }
                }
                else {
                    // 单点方形
                    DrawSingleSquare(maskSession, Points[0]);
                }

                // 应用擦除效果
                var eraseEffect = new AlphaMaskEffect {
                    Source = new ColorSourceEffect { Color = Colors.Transparent },
                    AlphaMask = cmdList
                };

                ds.DrawImage(eraseEffect);
            }
        }

        private void DrawSingleSquare(CanvasDrawingSession ds, Vector2 center) {
            var rect = new Rect(
                center.X - Thickness / 2,
                center.Y - Thickness / 2,
                Thickness,
                Thickness);

            ds.FillRectangle(rect, Colors.White);
        }

        private void DrawSquareSegment(CanvasDrawingSession ds, Vector2 start, Vector2 end) {
            // 计算线段方向向量
            var direction = Vector2.Normalize(end - start);
            var perpendicular = new Vector2(-direction.Y, direction.X) * Thickness / 2;

            // 创建方形路径
            using (var path = new CanvasPathBuilder(ds.Device)) {
                path.BeginFigure(start + perpendicular);
                path.AddLine(start - perpendicular);
                path.AddLine(end - perpendicular);
                path.AddLine(end + perpendicular);
                path.EndFigure(CanvasFigureLoop.Closed);

                using (var geometry = CanvasGeometry.CreatePath(path)) {
                    ds.FillGeometry(geometry, Colors.White);
                }
            }
        }
    }
}
