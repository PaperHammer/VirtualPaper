using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.DraftPanel.Model.Runtime;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace VirtualPaper.DraftPanel.Utils {
    static class CanvasUtil {
        // 辅助方法：将byte[]转换为ImageSource
        internal static BitmapImage ByteArrayToImageSource(byte[] imageData) {
            using InMemoryRandomAccessStream stream = new();
            using (DataWriter writer = new(stream.GetOutputStreamAt(0))) {
                writer.WriteBytes(imageData);
                writer.StoreAsync().GetResults();
            }
            BitmapImage image = new();
            image.SetSource(stream);

            return image;
        }

        // 辅助方法：根据点数组创建PathGeometry
        internal static PathGeometry CreatePathGeometry(List<PointF> points) {
            PathFigure figure = new() { StartPoint = new Point(points[0].X, points[0].Y) };
            for (int i = 1; i < points.Count; i++) {
                figure.Segments.Add(new LineSegment { Point = new Point(points[i].X, points[i].Y) });
            }
            PathGeometry geometry = new();
            geometry.Figures.Add(figure);

            return geometry;
        }

        internal static float FormatFloat(double value, int digit) {
            return (float)Math.Round(value, digit);
        }

        internal static PointF? FormatPoint(Point? point, int digit) {
            return point == null ? null : new PointF(FormatFloat(point.Value.X, digit), FormatFloat(point.Value.Y, digit));
        }
    }
}
