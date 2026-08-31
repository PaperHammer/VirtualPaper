using Windows.Foundation;
using Windows.Graphics.Imaging;
using Workloads.Creation.StaticImg.Core.Rendering;

namespace VirtualPaper.UI.Test.T_StaticImg {
    [TestClass]
    public class DynamicStrokeCacheTests {
        [TestMethod]
        public void CalculateBounds_AlignsToAllocationStep() {
            Rect result = CanvasPathDrawer.CalculateDynamicCacheBounds(
                new Rect(1200, 700, 10, 10),
                Rect.Empty,
                new BitmapSize { Width = 4096, Height = 4096 });

            Assert.AreEqual(new Rect(1024, 512, 512, 512), result);
        }

        [TestMethod]
        public void CalculateBounds_PreservesExistingContentWhenExpanding() {
            Rect existing = new(1024, 512, 512, 512);

            Rect result = CanvasPathDrawer.CalculateDynamicCacheBounds(
                new Rect(1600, 700, 10, 10),
                existing,
                new BitmapSize { Width = 4096, Height = 4096 });

            Assert.AreEqual(new Rect(1024, 512, 1024, 512), result);
        }

        [TestMethod]
        public void CalculateBounds_DoesNotReallocateForContainedRegion() {
            Rect existing = new(1024, 512, 512, 512);

            Rect result = CanvasPathDrawer.CalculateDynamicCacheBounds(
                new Rect(1200, 700, 10, 10),
                existing,
                new BitmapSize { Width = 4096, Height = 4096 });

            Assert.AreEqual(existing, result);
        }

        [TestMethod]
        public void CalculateBounds_GrowsGeometricallyAfterInitialAllocation() {
            Rect existing = new(0, 0, 1024, 512);

            Rect result = CanvasPathDrawer.CalculateDynamicCacheBounds(
                new Rect(1100, 200, 10, 10),
                existing,
                new BitmapSize { Width = 4096, Height = 4096 });

            Assert.AreEqual(new Rect(0, 0, 2048, 512), result);
        }

        [TestMethod]
        public void CalculateBounds_ClampsToCanvasEdges() {
            Rect result = CanvasPathDrawer.CalculateDynamicCacheBounds(
                new Rect(3900, 1950, 100, 40),
                Rect.Empty,
                new BitmapSize { Width = 4000, Height = 2000 });

            Assert.AreEqual(new Rect(3584, 1536, 416, 464), result);
        }
    }
}
