using Windows.Foundation;
using Workloads.Creation.StaticImg;

namespace VirtualPaper.UI.Test.T_StaticImg {
    [TestClass]
    public class ArcSizeTests {
        [TestMethod]
        public void Constructor_SetsPropertiesCorrectly() {
            var size = new ArcSize(800, 600, 96, RebuildMode.None);
            Assert.AreEqual(800f, size.Width);
            Assert.AreEqual(600f, size.Height);
            Assert.AreEqual(96u, size.Dpi);
            Assert.AreEqual(RebuildMode.None, size.Rebuild);
        }

        [TestMethod]
        public void Ratio_CalculatedFromWidthAndHeight() {
            var size = new ArcSize(800, 600, 96, RebuildMode.None);
            Assert.AreEqual(800f / 600f, size.Ratio, 0.001f);
        }

        [TestMethod]
        public void Ratio_4x3_IsCorrect() {
            var size = new ArcSize(400, 300, 96, RebuildMode.None);
            Assert.AreEqual(4f / 3f, size.Ratio, 0.001f);
        }

        [TestMethod]
        public void Bound_ReturnsRectFromZero() {
            var size = new ArcSize(1920, 1080, 96, RebuildMode.None);
            var bound = size.Bound;
            Assert.AreEqual(0, bound.X);
            Assert.AreEqual(0, bound.Y);
            Assert.AreEqual(1920, bound.Width);
            Assert.AreEqual(1080, bound.Height);
        }

        [TestMethod]
        public void ToSize_ReturnsCorrectSize() {
            var size = new ArcSize(640, 480, 96, RebuildMode.None);
            var s = size.ToSize();
            Assert.AreEqual(640d, s.Width);
            Assert.AreEqual(480d, s.Height);
        }

        [TestMethod]
        public void ToRect_ReturnsRectFromZero() {
            var size = new ArcSize(320, 240, 96, RebuildMode.None);
            var r = size.ToRect();
            Assert.AreEqual(0, r.X);
            Assert.AreEqual(0, r.Y);
            Assert.AreEqual(320, r.Width);
            Assert.AreEqual(240, r.Height);
        }

        // --- Equality ---

        [TestMethod]
        public void Equals_SameValues_ReturnsTrue() {
            var a = new ArcSize(100, 200, 96, RebuildMode.None);
            var b = new ArcSize(100, 200, 96, RebuildMode.None);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
        }

        [TestMethod]
        public void Equals_DifferentWidth_ReturnsFalse() {
            var a = new ArcSize(100, 200, 96, RebuildMode.None);
            var b = new ArcSize(150, 200, 96, RebuildMode.None);
            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a != b);
        }

        [TestMethod]
        public void Equals_DifferentDpi_ReturnsFalse() {
            var a = new ArcSize(100, 200, 96, RebuildMode.None);
            var b = new ArcSize(100, 200, 72, RebuildMode.None);
            Assert.IsFalse(a.Equals(b));
        }

        [TestMethod]
        public void Equals_DifferentRebuild_ReturnsFalse() {
            var a = new ArcSize(100, 200, 96, RebuildMode.None);
            var b = new ArcSize(100, 200, 96, RebuildMode.RotateLeft);
            Assert.IsFalse(a.Equals(b));
        }

        [TestMethod]
        public void Equals_NullObject_ReturnsFalse() {
            var a = new ArcSize(100, 200, 96, RebuildMode.None);
            Assert.IsFalse(a.Equals(null));
        }

        [TestMethod]
        public void GetHashCode_SameValues_SameHash() {
            var a = new ArcSize(100, 200, 96, RebuildMode.None);
            var b = new ArcSize(100, 200, 96, RebuildMode.None);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        // --- Rebuild mode queries ---

        [TestMethod]
        [DataRow(RebuildMode.RotateLeft)]
        [DataRow(RebuildMode.RotateRight)]
        public void IsRotate_RotateModes_ReturnsTrue(RebuildMode mode) {
            var size = new ArcSize(100, 200, 96, mode);
            Assert.IsTrue(size.IsRotate());
        }

        [TestMethod]
        public void IsRotate_NoneMode_ReturnsFalse() {
            var size = new ArcSize(100, 200, 96, RebuildMode.None);
            Assert.IsFalse(size.IsRotate());
        }

        [TestMethod]
        [DataRow(RebuildMode.FlipHorizontal)]
        [DataRow(RebuildMode.FlipVertical)]
        public void IsFlip_FlipModes_ReturnsTrue(RebuildMode mode) {
            var size = new ArcSize(100, 200, 96, mode);
            Assert.IsTrue(size.IsFlip());
        }

        [TestMethod]
        public void IsFlip_NoneMode_ReturnsFalse() {
            var size = new ArcSize(100, 200, 96, RebuildMode.None);
            Assert.IsFalse(size.IsFlip());
        }

        [TestMethod]
        public void IsResizeExpand_ResizeExpandMode_ReturnsTrue() {
            var size = new ArcSize(100, 200, 96, RebuildMode.ResizeExpand);
            Assert.IsTrue(size.IsResizeExpand());
        }

        [TestMethod]
        public void IsResizeExpand_NoneMode_ReturnsFalse() {
            var size = new ArcSize(100, 200, 96, RebuildMode.None);
            Assert.IsFalse(size.IsResizeExpand());
        }

        [TestMethod]
        public void IsResizeScale_ResizeScaleMode_ReturnsTrue() {
            var size = new ArcSize(100, 200, 96, RebuildMode.ResizeScale);
            Assert.IsTrue(size.IsResizeScale());
        }

        [TestMethod]
        public void IsResizeScale_NoneMode_ReturnsFalse() {
            var size = new ArcSize(100, 200, 96, RebuildMode.None);
            Assert.IsFalse(size.IsResizeScale());
        }

        // --- Area ---

        [TestMethod]
        public void Area_CalculatesCorrectly() {
            var s = new Size(10, 20);
            Assert.AreEqual(200d, ArcSize.Area(s));
        }

        [TestMethod]
        public void Area_ZeroSize_ReturnsZero() {
            Assert.AreEqual(0d, ArcSize.Area(new Size(0, 0)));
        }
    }
}
