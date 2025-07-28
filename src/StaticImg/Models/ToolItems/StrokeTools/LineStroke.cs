using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Models.ToolItems.Base;

namespace Workloads.Creation.StaticImg.Models.ToolItems.StrokeTools {
    sealed class LineStroke : StrokeBase {
        // 绘制笔迹
        public override void Draw(CanvasDrawingSession ds) {
            if (Points.Count == 0) return;

            // 单点绘制模式
            if (Points.Count == 1) {
                ds.DrawImage(Brush, (float)(Points[0].X - Thickness / 2), (float)(Points[0].Y - Thickness / 2));
                return;
            }

            // 使用优化的贝塞尔曲线连接点
            for (int i = 1; i < Points.Count; i++) {
                var p0 = i > 1 ? Points[i - 2] : Points[i - 1];
                var p1 = Points[i - 1];
                var p2 = Points[i];
                var p3 = i < Points.Count - 1 ? Points[i + 1] : p2;

                // 优化的控制点计算
                var cp1 = new Point(
                    p1.X + (p2.X - p0.X) * GetDynamicTension(p0, p1, p2),
                    p1.Y + (p2.Y - p0.Y) * GetDynamicTension(p0, p1, p2));

                var cp2 = new Point(
                    p2.X - (p3.X - p1.X) * GetDynamicTension(p1, p2, p3),
                    p2.Y - (p3.Y - p1.Y) * GetDynamicTension(p1, p2, p3));

                // 更精细的曲线分段
                // 从0.1改为0.05增加细分
                for (double t = 0; t <= 1; t += 0.05) {
                    double x = Math.Pow(1 - t, 3) * p1.X +
                             3 * Math.Pow(1 - t, 2) * t * cp1.X +
                             3 * (1 - t) * t * t * cp2.X +
                             t * t * t * p2.X;

                    double y = Math.Pow(1 - t, 3) * p1.Y +
                             3 * Math.Pow(1 - t, 2) * t * cp1.Y +
                             3 * (1 - t) * t * t * cp2.Y +
                             t * t * t * p2.Y;

                    ds.DrawImage(Brush, (float)(x - Thickness / 2), (float)(y - Thickness / 2));
                }
            }
        }

        private double GetDynamicTension(Vector2 p0, Vector2 p1, Vector2 p2) {
            // 计算前后两点之间的距离
            double distancePrev = Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2));
            double distanceNext = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

            // 综合考虑前后两个距离，取平均值作为参考
            double averageDistance = (distancePrev + distanceNext) / 2;

            // 根据平均距离动态调整张力值
            return Math.Min(0.5, _baseTension * (averageDistance / 10));
        }


        /// <summary>
        /// 基础曲线张力-控制曲线平滑度（值越小越尖锐，越大越平滑）
        /// <para>推荐值：</para>
        /// <para>钢笔效果：0.15-0.25</para>
        /// <para>毛笔效果：0.25-0.35  </para>
        /// </summary>
        private readonly double _baseTension = 0.2;
    }
}
