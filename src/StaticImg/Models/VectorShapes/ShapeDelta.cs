using System;

namespace Workloads.Creation.StaticImg.Models.VectorShapes {
    public abstract record ShapeDelta(DateTime Timestamp);
    public record AddShapeDelta(VectorShapeBase Shape, DateTime Timestamp) : ShapeDelta(Timestamp);
    public record RemoveShapeDelta(Guid ShapeId, DateTime Timestamp) : ShapeDelta(Timestamp);
    public record ModifyShapeDelta(Guid ShapeId, byte[] BinaryDiff, DateTime Timestamp) : ShapeDelta(Timestamp);
}
