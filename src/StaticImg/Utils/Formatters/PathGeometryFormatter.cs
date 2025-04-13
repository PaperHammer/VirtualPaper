using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Utils.Formatters {
    public class PathGeometryFormatter : IMessagePackFormatter<PathGeometry> {
        public void Serialize(ref MessagePackWriter writer, PathGeometry value, MessagePackSerializerOptions options) {
            if (value == null) {
                writer.WriteNil();
                return;
            }

            var serializable = SerializablePathGeometry.FromPathGeometry(value);
            options.Resolver.GetFormatterWithVerify<SerializablePathGeometry>().Serialize(ref writer, serializable, options);
        }

        public PathGeometry Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) {
            if (reader.TryReadNil()) return null;

            var serializable = options.Resolver.GetFormatterWithVerify<SerializablePathGeometry>().Deserialize(ref reader, options);
            return serializable.ToPathGeometry();
        }
    }

    [MessagePackObject]
    public class SerializablePathGeometry {
        [Key(0)]
        public List<SerializablePathFigure> Figures { get; set; } = new();

        [Key(1)]
        public FillRule FillRule { get; set; }

        // 转换为 WinUI 的 PathGeometry
        public PathGeometry ToPathGeometry() {
            var geometry = new PathGeometry { FillRule = FillRule };
            foreach (var figure in Figures) {
                geometry.Figures.Add(figure.ToPathFigure());
            }
            return geometry;
        }

        // 从 WinUI 的 PathGeometry 转换
        public static SerializablePathGeometry FromPathGeometry(PathGeometry geometry) {
            var serializable = new SerializablePathGeometry { FillRule = geometry.FillRule };
            foreach (var figure in geometry.Figures) {
                serializable.Figures.Add(SerializablePathFigure.FromPathFigure(figure));
            }
            return serializable;
        }
    }

    [MessagePackObject]
    public class SerializablePathFigure {
        [Key(0)]
        public ArcPoint StartPoint { get; set; }

        [Key(1)]
        public List<SerializablePathSegment> Segments { get; set; } = [];

        [Key(2)]
        public bool IsClosed { get; set; }

        [Key(3)]
        public bool IsFilled { get; set; }

        public PathFigure ToPathFigure() {
            var figure = new PathFigure {
                StartPoint = ArcPoint.ToPoint(StartPoint),
                IsClosed = IsClosed,
                IsFilled = IsFilled
            };
            foreach (var segment in Segments) {
                figure.Segments.Add(segment.ToPathSegment());
            }
            return figure;
        }

        public static SerializablePathFigure FromPathFigure(PathFigure figure) {
            var serializable = new SerializablePathFigure {
                StartPoint = ArcPoint.ToArcPoint(figure.StartPoint),
                IsClosed = figure.IsClosed,
                IsFilled = figure.IsFilled
            };
            foreach (var segment in figure.Segments) {
                serializable.Segments.Add(SerializablePathSegment.FromPathSegment(segment));
            }
            return serializable;
        }
    }

    [MessagePackObject]
    [Union(0, typeof(SerializableLineSegment))]
    [Union(1, typeof(SerializableBezierSegment))]
    public abstract class SerializablePathSegment {
        public abstract PathSegment ToPathSegment();
        public static SerializablePathSegment FromPathSegment(PathSegment segment) {
            return segment switch {
                LineSegment line => new SerializableLineSegment { Point = ArcPoint.ToArcPoint(line.Point) },
                BezierSegment bezier => new SerializableBezierSegment {
                    Point1 = ArcPoint.ToArcPoint(bezier.Point1),
                    Point2 = ArcPoint.ToArcPoint(bezier.Point2),
                    Point3 = ArcPoint.ToArcPoint(bezier.Point3)
                },
                _ => throw new NotSupportedException($"Unsupported segment type: {segment.GetType()}")
            };
        }
    }

    [MessagePackObject]
    public class SerializableLineSegment : SerializablePathSegment {
        [Key(0)]
        public ArcPoint Point { get; set; }

        public override PathSegment ToPathSegment() => new LineSegment { Point = ArcPoint.ToPoint(Point) };
    }

    [MessagePackObject]
    public class SerializableBezierSegment : SerializablePathSegment {
        [Key(0)]
        public ArcPoint Point1 { get; set; }

        [Key(1)]
        public ArcPoint Point2 { get; set; }

        [Key(2)]
        public ArcPoint Point3 { get; set; }

        public override PathSegment ToPathSegment() => new BezierSegment {
            Point1 = ArcPoint.ToPoint(Point1),
            Point2 = ArcPoint.ToPoint(Point2),
            Point3 = ArcPoint.ToPoint(Point3),
        };
    }
}
