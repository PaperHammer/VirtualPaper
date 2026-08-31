using Windows.Foundation;
using Workloads.Creation.StaticImg.Views.Components;

namespace VirtualPaper.UI.Test.T_StaticImg {
    [TestClass]
    public class CanvasZoomTests {
        [TestMethod]
        public void CalculateZoomOffset_KeepsMouseAnchorAtSameViewportPosition() {
            Point offset = InkCanvas.CalculateZoomOffset(
                targetZoom: 2f,
                contentAnchor: new Point(300, 200),
                viewportAnchor: new Point(250, 150),
                contentMarginLeft: 48,
                contentMarginTop: 48);

            Assert.AreEqual(new Point(446, 346), offset);
        }

        [TestMethod]
        public void CalculateZoomOffset_IncludesContentMargin() {
            Point withoutMargin = InkCanvas.CalculateZoomOffset(
                1.5f,
                new Point(100, 80),
                new Point(60, 40),
                0,
                0);
            Point withMargin = InkCanvas.CalculateZoomOffset(
                1.5f,
                new Point(100, 80),
                new Point(60, 40),
                48,
                48);

            Assert.AreEqual(72, withMargin.X - withoutMargin.X, 0.0001);
            Assert.AreEqual(72, withMargin.Y - withoutMargin.Y, 0.0001);
        }
    }
}
