using MessagePack;
using MessagePack.Formatters;
using Microsoft.UI.Xaml.Media;

namespace Workloads.Creation.StaticImg.Utils.Formatters {
    public class TransformFormatter : IMessagePackFormatter<Transform> {
        public void Serialize(ref MessagePackWriter writer, Transform value, MessagePackSerializerOptions options) {
            if (value == null) {
                writer.WriteNil();
                return;
            }

            switch (value) {
                case MatrixTransform matrixTransform:
                    writer.WriteArrayHeader(2); // [类型标记, Matrix]
                    writer.WriteInt32(0); // 0 = MatrixTransform
                    options.Resolver.GetFormatterWithVerify<Matrix>().Serialize(ref writer, matrixTransform.Matrix, options);
                    break;

                case RotateTransform rotateTransform:
                    writer.WriteArrayHeader(4); // [类型标记, Angle, CenterX, CenterY]
                    writer.WriteInt32(1); // 1 = RotateTransform
                    writer.Write(rotateTransform.Angle);
                    writer.Write(rotateTransform.CenterX);
                    writer.Write(rotateTransform.CenterY);
                    break;

                case ScaleTransform scaleTransform:
                    writer.WriteArrayHeader(4); // [类型标记, ScaleX, ScaleY, CenterX, CenterY]
                    writer.WriteInt32(2); // 2 = ScaleTransform
                    writer.Write(scaleTransform.ScaleX);
                    writer.Write(scaleTransform.ScaleY);
                    writer.Write(scaleTransform.CenterX);
                    writer.Write(scaleTransform.CenterY);
                    break;

                case TranslateTransform translateTransform:
                    writer.WriteArrayHeader(3); // [类型标记, X, Y]
                    writer.WriteInt32(3); // 3 = TranslateTransform
                    writer.Write(translateTransform.X);
                    writer.Write(translateTransform.Y);
                    break;

                default:
                    break;
            }
        }

        public Transform Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) {
            if (reader.TryReadNil()) return null;

            var arrayLength = reader.ReadArrayHeader();
            if (arrayLength < 1) return null;

            var transformType = reader.ReadInt32();

            switch (transformType) {
                case 0: // MatrixTransform
                    var matrix = options.Resolver.GetFormatterWithVerify<Matrix>().Deserialize(ref reader, options);
                    return new MatrixTransform { Matrix = matrix };

                case 1: // RotateTransform
                    var angle = reader.ReadDouble();
                    var centerX = reader.ReadDouble();
                    var centerY = reader.ReadDouble();
                    return new RotateTransform { Angle = angle, CenterX = centerX, CenterY = centerY };

                case 2: // ScaleTransform
                    var scaleX = reader.ReadDouble();
                    var scaleY = reader.ReadDouble();
                    var scaleCenterX = reader.ReadDouble();
                    var scaleCenterY = reader.ReadDouble();
                    return new ScaleTransform { ScaleX = scaleX, ScaleY = scaleY, CenterX = scaleCenterX, CenterY = scaleCenterY };

                case 3: // TranslateTransform
                    var x = reader.ReadDouble();
                    var y = reader.ReadDouble();
                    return new TranslateTransform { X = x, Y = y };

                default:
                    return null;
            }
        }
    }
}
