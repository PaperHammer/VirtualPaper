using MessagePack;
using MessagePack.Formatters;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Workloads.Creation.StaticImg.Utils.Formatters {
    public class GradientStopFormatter : IMessagePackFormatter<GradientStop> {
        public void Serialize(ref MessagePackWriter writer, GradientStop value, MessagePackSerializerOptions options) {
            if (value == null) {
                writer.WriteNil();
                return;
            }

            // 先转换成可序列化的结构
            var serializableStop = SerializableGradientStop.FromGradientStop(value);
            options.Resolver.GetFormatterWithVerify<SerializableGradientStop>().Serialize(ref writer, serializableStop, options);
        }

        public GradientStop Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) {
            if (reader.TryReadNil())
                return null;

            // 反序列化成 SerializableGradientStop，再转回 GradientStop
            var serializableStop = options.Resolver.GetFormatterWithVerify<SerializableGradientStop>().Deserialize(ref reader, options);
            return serializableStop.ToGradientStop();
        }
    }

    [MessagePackObject]
    public struct SerializableGradientStop {
        [Key(0)]
        public uint ColorArgb { get; set; }

        [Key(1)]
        public double Offset { get; set; }

        [IgnoreMember]
        public Color Color {
            readonly get => ColorArgb.ToColor();
            set => ColorArgb = value.ToUInt32();
        }

        public readonly GradientStop ToGradientStop() => new() { Color = Color, Offset = Offset };
        public static SerializableGradientStop FromGradientStop(GradientStop stop) => new() { Color = stop.Color, Offset = stop.Offset };
    }
}
