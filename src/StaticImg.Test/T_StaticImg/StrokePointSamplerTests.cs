using System.Numerics;
using Workloads.Creation.StaticImg.Core.Rendering;

namespace StaticImg.Test.T_StaticImg {
    [TestClass]
    public class StrokePointSamplerTests {
        [TestMethod]
        public void AddOrUpdate_EmptyList_AddsFirstPoint() {
            List<Vector2> points = [];

            bool changed = StrokePointSampler.AddOrUpdate(points, new Vector2(2, 3));

            Assert.IsTrue(changed);
            CollectionAssert.AreEqual(new[] { new Vector2(2, 3) }, points);
        }

        [TestMethod]
        public void AddOrUpdate_SubPixelMovement_IgnoresPoint() {
            List<Vector2> points = [Vector2.Zero];

            bool changed = StrokePointSampler.AddOrUpdate(points, new Vector2(0.2f, 0.2f));

            Assert.IsFalse(changed);
            Assert.HasCount(1, points);
        }

        [TestMethod]
        public void AddOrUpdate_StraightMovement_ReplacesLastPoint() {
            List<Vector2> points = [Vector2.Zero, new Vector2(10, 0)];

            bool changed = StrokePointSampler.AddOrUpdate(points, new Vector2(20, 0));

            Assert.IsTrue(changed);
            Assert.HasCount(2, points);
            Assert.AreEqual(new Vector2(20, 0), points[^1]);
        }

        [TestMethod]
        public void AddOrUpdate_LongStraightStroke_KeepsOnlyEndpoints() {
            List<Vector2> points = [Vector2.Zero];

            for (int x = 1; x <= 1000; x++)
                StrokePointSampler.AddOrUpdate(points, new Vector2(x, 0));

            Assert.HasCount(2, points);
            Assert.AreEqual(new Vector2(1000, 0), points[^1]);
        }

        [TestMethod]
        public void AddOrUpdate_VisibleCorner_PreservesTurningPoint() {
            List<Vector2> points = [Vector2.Zero, new Vector2(10, 0)];

            StrokePointSampler.AddOrUpdate(points, new Vector2(10, 10));

            Assert.HasCount(3, points);
            Assert.AreEqual(new Vector2(10, 0), points[1]);
        }

        [TestMethod]
        public void AddOrUpdate_GradualCurveAboveTolerance_PreservesCurvePoint() {
            List<Vector2> points = [Vector2.Zero, new Vector2(5, 0.5f)];

            StrokePointSampler.AddOrUpdate(points, new Vector2(10, 0));

            Assert.HasCount(3, points);
            Assert.AreEqual(new Vector2(5, 0.5f), points[1]);
        }

        [TestMethod]
        public void AddOrUpdate_SubtleCurveAboveTolerance_PreservesCurvePoint() {
            List<Vector2> points = [Vector2.Zero, new Vector2(5, 0.15f)];

            StrokePointSampler.AddOrUpdate(points, new Vector2(10, 0));

            Assert.HasCount(3, points);
            Assert.AreEqual(new Vector2(5, 0.15f), points[1]);
        }

        [TestMethod]
        public void AddOrUpdate_DirectionReversal_PreservesLastPoint() {
            List<Vector2> points = [Vector2.Zero, new Vector2(10, 0)];

            StrokePointSampler.AddOrUpdate(points, new Vector2(5, 0));

            Assert.HasCount(3, points);
        }

        [TestMethod]
        public void AddOrUpdate_InvalidCoordinate_IgnoresPoint() {
            List<Vector2> points = [Vector2.Zero];

            bool changed = StrokePointSampler.AddOrUpdate(points, new Vector2(float.NaN, 1));

            Assert.IsFalse(changed);
            Assert.HasCount(1, points);
        }
    }
}
