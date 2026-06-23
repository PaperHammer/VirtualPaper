using VirtualPaper.Common;

namespace VirtualPaper.Core.Test.T_Common {
    [TestClass]
    public class SliderPropertyTests {
        // ── SliderProperty<T>.Value 越界回退 ──────────────────────────

        [TestMethod]
        public void Saturation_ValueInRange_Accepted() {
            var prop = new Saturation();
            prop.Value = 5;
            Assert.AreEqual(5d, prop.Value);
        }

        [TestMethod]
        public void Saturation_ValueBelowMin_ResetsToDefault() {
            var prop = new Saturation();
            prop.Value = -1; // < Min(0)
            Assert.AreEqual(prop.DefaultValue, prop.Value);
        }

        [TestMethod]
        public void Saturation_ValueAboveMax_ResetsToDefault() {
            var prop = new Saturation();
            prop.Value = 11; // > Max(10)
            Assert.AreEqual(prop.DefaultValue, prop.Value);
        }

        [TestMethod]
        public void Brightness_ValueAtBoundary_Accepted() {
            var prop = new Brightness();
            prop.Value = 0;
            Assert.AreEqual(0d, prop.Value);

            prop.Value = 2;
            Assert.AreEqual(2d, prop.Value);
        }

        [TestMethod]
        public void Speed_ValueBelowMin_ResetsToDefault() {
            var prop = new Speed();
            prop.Value = 0.1; // < Min(0.25)
            Assert.AreEqual(prop.DefaultValue, prop.Value);
        }

        [TestMethod]
        public void Volume_ValueAboveMax_ResetsToDefault() {
            var prop = new Volume();
            prop.Value = 1.5; // > Max(1)
            Assert.AreEqual(prop.DefaultValue, prop.Value);
        }

        [TestMethod]
        public void Hue_IntValueInRange_Accepted() {
            var prop = new Hue();
            prop.Value = 180;
            Assert.AreEqual(180, prop.Value);
        }

        [TestMethod]
        public void Hue_IntValueOutOfRange_ResetsToDefault() {
            var prop = new Hue();
            prop.Value = 400; // > Max(359)
            Assert.AreEqual(prop.DefaultValue, prop.Value);
        }

        [TestMethod]
        public void SliderProperty_DefaultValue_IsInitialValue() {
            Assert.AreEqual(1d, new Saturation().Value);
            Assert.AreEqual(1d, new Brightness().Value);
            Assert.AreEqual(1d, new Contrast().Value);
            Assert.AreEqual(1d, new Speed().Value);
            Assert.AreEqual(0.8d, new Volume().Value);
            Assert.AreEqual(0, new Hue().Value);
        }

        // ── Scaling.Value clamp ───────────────────────────────────────

        [TestMethod]
        public void Scaling_ValueClamped_0To4() {
            var s = new Scaling();

            s.Value = -1;
            Assert.AreEqual(0, s.Value);

            s.Value = 10;
            Assert.AreEqual(4, s.Value);

            s.Value = 2;
            Assert.AreEqual(2, s.Value);
        }

        [TestMethod]
        public void Scaling_Has5Items() {
            var s = new Scaling();
            Assert.HasCount(5, s.Items);
        }
    }
}
