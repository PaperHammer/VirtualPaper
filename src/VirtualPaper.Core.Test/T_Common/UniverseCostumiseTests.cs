using VirtualPaper.Common;

namespace VirtualPaper.Core.Test.T_Common {
    [TestClass]
    public class UniverseCostumiseTests {
        // ── ModifyPropertyValue 正常路径 ──────────────────────────────

        [TestMethod]
        public void ModifyPropertyValue_ValidNameAndValue_SetsValue() {
            var ctx = new PictureAndGifCostumise();

            ctx.ModifyPropertyValue("Saturation", 5.0);

            Assert.AreEqual(5.0, ctx.Saturation.Value);
        }

        [TestMethod]
        public void ModifyPropertyValue_BoolProperty_SetsWithoutRangeCheck() {
            var ctx = new PictureAndGifCostumise();

            ctx.ModifyPropertyValue("Parallax", true);

            Assert.IsTrue(ctx.Parallax.Value);
        }

        [TestMethod]
        public void ModifyPropertyValue_VideoSpeed_SetsValue() {
            var ctx = new VideoCostumize();

            ctx.ModifyPropertyValue("Speed", 2.0);

            Assert.AreEqual(2.0, ctx.Speed.Value);
        }

        // ── ModifyPropertyValue 异常路径 ──────────────────────────────

        [TestMethod]
        public void ModifyPropertyValue_InvalidPropertyName_ThrowsArgumentException() {
            var ctx = new PictureAndGifCostumise();

            Assert.Throws<ArgumentException>(() =>
                ctx.ModifyPropertyValue("NonExistent", 1.0));
        }

        [TestMethod]
        public void ModifyPropertyValue_ValueOutOfRange_ThrowsArgumentOutOfRange() {
            var ctx = new PictureAndGifCostumise();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ctx.ModifyPropertyValue("Saturation", 999.0)); // Max=10
        }

        [TestMethod]
        public void ModifyPropertyValue_ValueBelowMin_ThrowsArgumentOutOfRange() {
            var ctx = new VideoCostumize();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ctx.ModifyPropertyValue("Speed", 0.01)); // Min=0.25
        }

        // ── 子类属性注册 ──────────────────────────────────────────────

        [TestMethod]
        public void PictureAndGifCostumise_HasScalingAndParallax() {
            var ctx = new PictureAndGifCostumise();

            ctx.ModifyPropertyValue("Scaling", 2);
            Assert.AreEqual(2, ctx.Scaling.Value);

            ctx.ModifyPropertyValue("Parallax", true);
            Assert.IsTrue(ctx.Parallax.Value);
        }

        [TestMethod]
        public void VideoCostumize_HasSpeedVolumeScalingParallax() {
            var ctx = new VideoCostumize();

            ctx.ModifyPropertyValue("Speed", 1.5);
            ctx.ModifyPropertyValue("Volume", 0.5);
            ctx.ModifyPropertyValue("Scaling", 1);
            ctx.ModifyPropertyValue("Parallax", true);

            Assert.AreEqual(1.5, ctx.Speed.Value);
            Assert.AreEqual(0.5, ctx.Volume.Value);
            Assert.AreEqual(1, ctx.Scaling.Value);
            Assert.IsTrue(ctx.Parallax.Value);
        }

        [TestMethod]
        public void Picture3DCostumise_HasScalingAndParallax() {
            var ctx = new Picture3DCostumize();

            ctx.ModifyPropertyValue("Scaling", 3);
            Assert.AreEqual(3, ctx.Scaling.Value);
        }

        [TestMethod]
        public void WebCostumize_OnlyBaseProperties() {
            var ctx = new WebCostumize();

            ctx.ModifyPropertyValue("Saturation", 2.0);
            Assert.AreEqual(2.0, ctx.Saturation.Value);

            Assert.Throws<ArgumentException>(() =>
                ctx.ModifyPropertyValue("Scaling", 1)); // Web 无 Scaling
        }
    }
}
