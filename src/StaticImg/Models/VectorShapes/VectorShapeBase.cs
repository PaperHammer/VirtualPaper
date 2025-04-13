using System;
using System.Numerics;
using MessagePack;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Utils.Formatters;

namespace Workloads.Creation.StaticImg.Models.VectorShapes {
    [MessagePackObject]
    [Union(0, typeof(VectorPath))] // 注册派生类型
    [Union(1, typeof(VectorRectangle))]
    public abstract class VectorShapeBase {
        [Key(0)]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Key(1)]
        [MessagePackFormatter(typeof(BrushFormatter))]
        public Brush Stroke { get; set; }
        [Key(2)]
        [MessagePackFormatter(typeof(BrushFormatter))]
        public Brush Fill { get; set; }
        [Key(3)]
        public double StrokeThickness { get; set; } = 5;
        [Key(4)]
        [MessagePackFormatter(typeof(TransformFormatter))]
        public Transform RenderTransform { get; set; }
        [IgnoreMember]
        public abstract VectorShapeType ShapeType { get; }
        [IgnoreMember]
        public abstract Geometry GeometryData { get; }

        public abstract Shape ToXamlShape();
        public abstract bool HitTest(Vector2 point);
    }

    // 扩展方法获取PathGeometry更新时间
    public static class PathGeometryExtensions {
        public static DateTime GetLastUpdateTime(this PathGeometry geometry) {
            // TODO: 仅使用简单实现，生产环境需要更精确的跟踪
            return DateTime.Now;
        }
    }

    public enum VectorShapeType {
        Ellipse,
        Rectangle,
        Path,
        Polygon,
        Polyline,
        Line,
        Composite
    }
}
