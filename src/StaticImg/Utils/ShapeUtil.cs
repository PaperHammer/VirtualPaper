using System.IO;
using Workloads.Creation.StaticImg.Models.VectorShapes;

namespace Workloads.Creation.StaticImg.Utils {
    class ShapeUtil {
        //private byte[] CalculateShapeDiff(VectorShapeBase original, VectorShapeBase current) {
        //    using var stream = new MemoryStream();
        //    using var writer = new BinaryWriter(stream);

        //    if (original.Stroke != current.Stroke) {
        //        writer.Write((byte)0x01);
        //        writer.Write(current.Stroke.ToHexString());
        //    }

        //    if (original.StrokeThickness != current.StrokeThickness) {
        //        writer.Write((byte)0x02);
        //        writer.Write(current.StrokeThickness);
        //    }

        //    if (!original.GeometryData.Equals(current.GeometryData)) {
        //        writer.Write((byte)0x10);
        //        current.WriteGeometryBinary(writer);
        //    }

        //    return stream.ToArray();
        //}

        //private void ApplyShapeDiff(VectorShapeBase shape, byte[] diff) {
        //    using var stream = new MemoryStream(diff);
        //    using var reader = new BinaryReader(stream);

        //    while (stream.Position < stream.Length) {
        //        var tag = reader.ReadByte();
        //        switch (tag) {
        //            case 0x01:
        //                shape.Stroke = BrushHelper.FromHex(reader.ReadString());
        //                break;
        //            case 0x02:
        //                shape.StrokeThickness = reader.ReadDouble();
        //                break;
        //            case 0x10:
        //                shape.ReadGeometryBinary(reader);
        //                break;
        //        }
        //    }
        //}
    }
}
