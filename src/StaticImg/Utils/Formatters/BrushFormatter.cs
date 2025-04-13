using MessagePack;
using MessagePack.Formatters;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Utils.Formatters {
    public class BrushFormatter : IMessagePackFormatter<Brush> {
        public void Serialize(ref MessagePackWriter writer, Brush value, MessagePackSerializerOptions options) {
            if (value == null) {
                writer.WriteNil();
                return;
            }

            switch (value) {
                case SolidColorBrush solidBrush:
                    // 存储为 [类型标记, uint颜色值]
                    writer.WriteArrayHeader(2);
                    writer.WriteInt32(0); // 0 = SolidColorBrush
                    writer.Write( solidBrush.Color.ToUInt32()); // 直接写入 uint
                    break;

                case LinearGradientBrush gradientBrush:
                    // 存储为 [类型标记, GradientStop列表, StartPoint, EndPoint]
                    writer.WriteArrayHeader(4);
                    writer.WriteInt32(1); // 1 = LinearGradientBrush

                    // 序列化 GradientStop 列表（每个Stop存储为 [uint颜色, Offset]）
                    writer.WriteArrayHeader(gradientBrush.GradientStops.Count);
                    foreach (var stop in gradientBrush.GradientStops) {
                        writer.WriteArrayHeader(2);
                        writer.Write(stop.Color.ToUInt32()); // uint 颜色
                        writer.Write(stop.Offset);
                    }

                    writer.Write(gradientBrush.StartPoint.X);
                    writer.Write(gradientBrush.StartPoint.Y);
                    writer.Write(gradientBrush.EndPoint.X);
                    writer.Write(gradientBrush.EndPoint.Y);
                    break;

                default:
                    writer.WriteNil();
                    break;
            }
        }

        public Brush Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) {
            if (reader.TryReadNil()) return null;

            var arrayLength = reader.ReadArrayHeader();
            if (arrayLength < 1) return null;

            var brushType = reader.ReadInt32();

            switch (brushType) {
                case 0: // SolidColorBrush
                    uint colorArgb = reader.ReadUInt32();
                    return new SolidColorBrush(colorArgb.ToColor()); // uint -> Color

                case 1: // LinearGradientBrush
                        // 读取 GradientStop 列表
                    var stopCount = reader.ReadArrayHeader();
                    var gradientStops = new GradientStopCollection();

                    for (int i = 0; i < stopCount; i++) {
                        reader.ReadArrayHeader(); // 每个Stop是 [uint, double]
                        uint stopColorArgb = reader.ReadUInt32();
                        double offset = reader.ReadDouble();
                        gradientStops.Add(new GradientStop {
                            Color = stopColorArgb.ToColor(),
                            Offset = offset
                        });
                    }

                    // 读取 StartPoint 和 EndPoint
                    double startX = reader.ReadDouble();
                    double startY = reader.ReadDouble();
                    double endX = reader.ReadDouble();
                    double endY = reader.ReadDouble();

                    return new LinearGradientBrush {
                        GradientStops = gradientStops,
                        StartPoint = new Point(startX, startY),
                        EndPoint = new Point(endX, endY)
                    };

                default:
                    return null;
            }
        }
    }
}
