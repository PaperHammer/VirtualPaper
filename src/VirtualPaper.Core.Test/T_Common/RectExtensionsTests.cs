using Windows.Foundation;
using VirtualPaper.Common.Extensions;

namespace VirtualPaper.Core.Test.T_Common {
    [TestClass]
    public class RectExtensionsTests {
        // ── IntersectRect ─────────────────────────────────────────────

        [TestMethod]
        public void IntersectRect_Overlapping_ReturnsIntersection() {
            var a = new Rect(0, 0, 10, 10);
            var b = new Rect(5, 5, 10, 10);

            var result = a.IntersectRect(b);

            Assert.AreEqual(5, result.X);
            Assert.AreEqual(5, result.Y);
            Assert.AreEqual(5, result.Width);
            Assert.AreEqual(5, result.Height);
        }

        [TestMethod]
        public void IntersectRect_NoOverlap_ReturnsEmpty() {
            var a = new Rect(0, 0, 5, 5);
            var b = new Rect(10, 10, 5, 5);

            var result = a.IntersectRect(b);

            Assert.IsTrue(result.IsEmpty);
        }

        [TestMethod]
        public void IntersectRect_OneEmpty_ReturnsEmpty() {
            var a = new Rect(0, 0, 10, 10);

            Assert.IsTrue(a.IntersectRect(Rect.Empty).IsEmpty);
            Assert.IsTrue(Rect.Empty.IntersectRect(a).IsEmpty);
        }

        [TestMethod]
        public void IntersectRect_Contained_ReturnsInner() {
            var outer = new Rect(0, 0, 100, 100);
            var inner = new Rect(10, 10, 20, 20);

            var result = outer.IntersectRect(inner);

            Assert.AreEqual(10, result.X);
            Assert.AreEqual(10, result.Y);
            Assert.AreEqual(20, result.Width);
            Assert.AreEqual(20, result.Height);
        }

        [TestMethod]
        public void IntersectRect_TouchingEdge_ReturnsEmpty() {
            var a = new Rect(0, 0, 10, 10);
            var b = new Rect(10, 0, 10, 10);

            var result = a.IntersectRect(b);

            Assert.IsTrue(result.IsEmpty);
        }

        // ── UnionRect ─────────────────────────────────────────────────

        [TestMethod]
        public void UnionRect_Overlapping_ReturnsBounding() {
            var a = new Rect(0, 0, 10, 10);
            var b = new Rect(5, 5, 10, 10);

            var result = a.UnionRect(b);

            Assert.AreEqual(0, result.X);
            Assert.AreEqual(0, result.Y);
            Assert.AreEqual(15, result.Width);
            Assert.AreEqual(15, result.Height);
        }

        [TestMethod]
        public void UnionRect_OneEmpty_ReturnsOther() {
            var rect = new Rect(5, 5, 10, 10);

            var r1 = rect.UnionRect(Rect.Empty);
            var r2 = Rect.Empty.UnionRect(rect);

            Assert.AreEqual(rect, r1);
            Assert.AreEqual(rect, r2);
        }

        [TestMethod]
        public void UnionRect_BothEmpty_ReturnsEmpty() {
            var result = Rect.Empty.UnionRect(Rect.Empty);
            Assert.IsTrue(result.IsEmpty);
        }

        [TestMethod]
        public void UnionRect_Disjoint_ReturnsFullBounding() {
            var a = new Rect(0, 0, 5, 5);
            var b = new Rect(10, 10, 5, 5);

            var result = a.UnionRect(b);

            Assert.AreEqual(0, result.X);
            Assert.AreEqual(0, result.Y);
            Assert.AreEqual(15, result.Width);
            Assert.AreEqual(15, result.Height);
        }

        // ── RoundOutwardAsInt ─────────────────────────────────────────

        [TestMethod]
        public void RoundOutwardAsInt_FractionalCoords_Expands() {
            var rect = new Rect(1.2, 2.3, 5.6, 7.8);

            var result = rect.RoundOutwardAsInt();

            Assert.AreEqual(1, result.X);     // Floor(1.2)
            Assert.AreEqual(2, result.Y);     // Floor(2.3)
            Assert.AreEqual(6, result.Width); // Ceil(1.2+5.6) - Floor(1.2) = Ceil(6.8) - 1 = 7 - 1 = 6
            Assert.AreEqual(9, result.Height); // Ceil(2.3+7.8) - Floor(2.3) = Ceil(10.1) - 2 = 11 - 2 = 9
        }

        [TestMethod]
        public void RoundOutwardAsInt_IntegerCoords_Unchanged() {
            var rect = new Rect(1, 2, 5, 7);

            var result = rect.RoundOutwardAsInt();

            Assert.AreEqual(1, result.X);
            Assert.AreEqual(2, result.Y);
            Assert.AreEqual(5, result.Width);
            Assert.AreEqual(7, result.Height);
        }

        [TestMethod]
        public void RoundOutwardAsInt_NegativeCoords_FloorsCorrectly() {
            var rect = new Rect(-1.5, -2.5, 10, 10);

            var result = rect.RoundOutwardAsInt();

            Assert.AreEqual(-2, result.X);    // Floor(-1.5)
            Assert.AreEqual(-3, result.Y);    // Floor(-2.5)
        }

        // ── GetSize ───────────────────────────────────────────────────

        [TestMethod]
        public void GetSize_ReturnsWidthAndHeight() {
            var rect = new Rect(10, 20, 100, 200);
            var size = rect.GetSize();

            Assert.AreEqual(100, size.Width);
            Assert.AreEqual(200, size.Height);
        }
    }
}
