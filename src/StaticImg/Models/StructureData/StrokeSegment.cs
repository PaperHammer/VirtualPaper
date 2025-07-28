using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.Extensions;
using Workloads.Creation.StaticImg.Models.ToolItems.Utils;

namespace Workloads.Creation.StaticImg.Models.StructureData {
    //public partial class StrokeSegment {
    //    public List<StrokePoint> Points { get; } = [];
    //    public float Thickness { get; }

    //    public StrokeSegment(StrokePoint point, float thickness) {
    //        Points.Add(point);
    //        Thickness = thickness;
    //    }

    //    public void Draw(CanvasDrawingSession ds, BrushShape brushType, DirectXPixelFormat format, CanvasAlphaMode alphaMode) {
    //        if (Points.Count == 0) return;

    //        //ds.Blend = _canvasBlend;
    //        // 单点绘制模式
    //        if (Points.Count == 1) {
    //            ds.DrawImage(
    //                BrushManager.GetBrush(Points[0].Color.ToColor(), Thickness, brushType, format, alphaMode),
    //                (float)(Points[0].StartPoint.X - Thickness / 2),
    //                (float)(Points[0].StartPoint.Y - Thickness / 2));
    //            return;
    //        }

    //        // 使用优化的贝塞尔曲线连接点
    //        for (int i = 1; i < Points.Count; i++) {
    //            var p0 = i > 1 ? Points[i - 2].StartPoint : Points[i - 1].StartPoint;
    //            var p1 = Points[i - 1].StartPoint;
    //            var p2 = Points[i].StartPoint;
    //            var p3 = i < Points.Count - 1 ? Points[i + 1].StartPoint : p2;

    //            // 优化的控制点计算
    //            var cp1 = new Point(
    //                p1.X + (p2.X - p0.X) * GetDynamicTension(p0, p1, p2),
    //                p1.Y + (p2.Y - p0.Y) * GetDynamicTension(p0, p1, p2));

    //            var cp2 = new Point(
    //                p2.X - (p3.X - p1.X) * GetDynamicTension(p1, p2, p3),
    //                p2.Y - (p3.Y - p1.Y) * GetDynamicTension(p1, p2, p3));

    //            // 更精细的曲线分段
    //            // 从0.1改为0.05增加细分
    //            for (double t = 0; t <= 1; t += 0.05) {
    //                double x = Math.Pow(1 - t, 3) * p1.X +
    //                         3 * Math.Pow(1 - t, 2) * t * cp1.X +
    //                         3 * (1 - t) * t * t * cp2.X +
    //                         t * t * t * p2.X;

    //                double y = Math.Pow(1 - t, 3) * p1.Y +
    //                         3 * Math.Pow(1 - t, 2) * t * cp1.Y +
    //                         3 * (1 - t) * t * t * cp2.Y +
    //                         t * t * t * p2.Y;

    //                Color color = LerpColor(
    //                    Points[i - 1].Color.ToColor(),
    //                    Points[i].Color.ToColor(),
    //                    (float)t);

    //                var brush = BrushManager.GetBrush(color, Thickness, brushType, format, alphaMode);
    //                ds.DrawImage(brush, (float)(x - Thickness / 2), (float)(y - Thickness / 2));
    //            }
    //        }
    //    }

    //    // 颜色插值方法
    //    private static Color LerpColor(Color a, Color b, float t) {
    //        return Color.FromArgb(
    //            (byte)(a.A + (b.A - a.A) * t),
    //            (byte)(a.R + (b.R - a.R) * t),
    //            (byte)(a.G + (b.G - a.G) * t),
    //            (byte)(a.B + (b.B - a.B) * t));
    //    }

    //    private double GetDynamicTension(Point p0, Point p1, Point p2) {
    //        // 计算前后两点之间的距离
    //        double distancePrev = Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2));
    //        double distanceNext = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

    //        // 综合考虑前后两个距离，取平均值作为参考
    //        double averageDistance = (distancePrev + distanceNext) / 2;

    //        // 根据平均距离动态调整张力值
    //        return Math.Min(0.5, _baseTension * (averageDistance / 10));
    //    }

    //    // SIMD优化的区域计算
    //    public Rect CalculateSegmentBoundsSIMD() {
    //        if (Points.Count == 0) return Rect.Empty;

    //        // 向量化计算
    //        var min = new Vector2(float.MaxValue);
    //        var max = new Vector2(float.MinValue);

    //        foreach (var point in Points) {
    //            var v = new Vector2((float)point.StartPoint.X, (float)point.StartPoint.Y);
    //            min = Vector2.Min(min, v);
    //            max = Vector2.Max(max, v);
    //        }

    //        // 考虑笔刷大小扩展区域
    //        var brushSize = new Vector2(Thickness);
    //        return new Rect(
    //            min.X - brushSize.X,
    //            min.Y - brushSize.Y,
    //            max.X - min.X + 2 * brushSize.X,
    //            max.Y - min.Y + 2 * brushSize.Y);
    //    }

    //    /// <summary>
    //    /// 基础曲线张力-控制曲线平滑度（值越小越尖锐，越大越平滑）
    //    /// <para>推荐值：</para>
    //    /// <para>钢笔效果：0.15-0.25</para>
    //    /// <para>毛笔效果：0.25-0.35  </para>
    //    /// </summary>
    //    protected readonly double _baseTension = 0.2;
    //}

    //public readonly struct StrokePoint(Point startPoint, byte[] color) {
    //    public Point StartPoint { get; } = startPoint;
    //    public byte[] Color { get; } = color;
    //}
}
