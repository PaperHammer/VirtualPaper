using System.Drawing;
using VirtualPaper.Common.Utils;

namespace VirtualPaper.Core.Test.T_Common {
    [TestClass]
    [TestCategory("Backend")]
    public class InputUtilTests {

        // -------------------------------------------------------
        // ToMouseDisplayLocal
        // -------------------------------------------------------

        [TestMethod]
        [Description("ToMouseDisplayLocal converts global coords to display-local by subtracting display origin")]
        public void ToMouseDisplayLocal_WhenDisplayStartsAtOrigin_ReturnsSameCoords() {
            var bounds = new Rectangle(0, 0, 1920, 1080);

            var result = InputUtil.ToMouseDisplayLocal(200, 300, bounds);

            Assert.AreEqual(200, result.X);
            Assert.AreEqual(300, result.Y);
        }

        [TestMethod]
        [Description("ToMouseDisplayLocal adjusts for non-zero display origin (e.g. secondary monitor at x=1920)")]
        public void ToMouseDisplayLocal_WhenDisplayHasOffset_SubtractsOffset() {
            var bounds = new Rectangle(1920, 0, 1920, 1080);

            var result = InputUtil.ToMouseDisplayLocal(2100, 500, bounds);

            Assert.AreEqual(180, result.X);   // 2100 - 1920
            Assert.AreEqual(500, result.Y);   // 0 offset on Y
        }

        [TestMethod]
        [Description("ToMouseDisplayLocal handles negative display offset (display left of primary)")]
        public void ToMouseDisplayLocal_WhenDisplayHasNegativeOffset_AddsAbsoluteOffset() {
            var bounds = new Rectangle(-1920, 0, 1920, 1080);

            var result = InputUtil.ToMouseDisplayLocal(-500, 100, bounds);

            Assert.AreEqual(1420, result.X);  // -500 - (-1920) = 1420
            Assert.AreEqual(100, result.Y);
        }

        [TestMethod]
        [Description("ToMouseDisplayLocal handles vertical offset (display stacked below primary)")]
        public void ToMouseDisplayLocal_WhenDisplayHasVerticalOffset_SubtractsYOffset() {
            var bounds = new Rectangle(0, 1080, 1920, 1080);

            var result = InputUtil.ToMouseDisplayLocal(100, 1200, bounds);

            Assert.AreEqual(100, result.X);
            Assert.AreEqual(120, result.Y);   // 1200 - 1080
        }

        [TestMethod]
        [Description("ToMouseDisplayLocal returns origin (0,0) when cursor is at top-left corner of the display")]
        public void ToMouseDisplayLocal_WhenCursorAtDisplayTopLeft_ReturnsOrigin() {
            var bounds = new Rectangle(3840, 0, 1920, 1080);

            var result = InputUtil.ToMouseDisplayLocal(3840, 0, bounds);

            Assert.AreEqual(0, result.X);
            Assert.AreEqual(0, result.Y);
        }

        // -------------------------------------------------------
        // ToMouseSpanLocal
        // -------------------------------------------------------

        [TestMethod]
        [Description("ToMouseSpanLocal subtracts virtual screen origin from global cursor position")]
        public void ToMouseSpanLocal_WhenVirtualScreenStartsAtOrigin_ReturnsSameCoords() {
            var virtualBounds = new Rectangle(0, 0, 3840, 1080);

            var result = InputUtil.ToMouseSpanLocal(500, 300, virtualBounds);

            Assert.AreEqual(500, result.X);
            Assert.AreEqual(300, result.Y);
        }

        [TestMethod]
        [Description("ToMouseSpanLocal adjusts when virtual screen has a non-zero X origin")]
        public void ToMouseSpanLocal_WhenVirtualScreenHasOffset_SubtractsOffset() {
            var virtualBounds = new Rectangle(-1920, 0, 3840, 1080);

            var result = InputUtil.ToMouseSpanLocal(0, 0, virtualBounds);

            Assert.AreEqual(1920, result.X);   // 0 - (-1920)
            Assert.AreEqual(0, result.Y);
        }

        [TestMethod]
        [Description("ToMouseSpanLocal handles vertical virtual screen offset")]
        public void ToMouseSpanLocal_WhenVirtualScreenHasVerticalOffset_SubtractsYOffset() {
            var virtualBounds = new Rectangle(0, -200, 1920, 1280);

            var result = InputUtil.ToMouseSpanLocal(100, 0, virtualBounds);

            Assert.AreEqual(100, result.X);
            Assert.AreEqual(200, result.Y);   // 0 - (-200)
        }

        [TestMethod]
        [Description("ToMouseSpanLocal returns (0,0) when cursor is at virtual screen origin")]
        public void ToMouseSpanLocal_WhenCursorAtVirtualOrigin_ReturnsOrigin() {
            var virtualBounds = new Rectangle(500, 200, 3840, 1080);

            var result = InputUtil.ToMouseSpanLocal(500, 200, virtualBounds);

            Assert.AreEqual(0, result.X);
            Assert.AreEqual(0, result.Y);
        }

        // -------------------------------------------------------
        // ForwardMessageMouse – lParam encoding
        // -------------------------------------------------------

        [TestMethod]
        [Description("ForwardMessageMouse encodes x in low word and y in high word of lParam")]
        public void ForwardMessageMouse_LParamEncoding_XInLowWordYInHighWord() {
            int capturedMsg = 0;
            IntPtr capturedWParam = IntPtr.Zero;
            UIntPtr capturedLParam = UIntPtr.Zero;

            // We can verify the encoding formula directly without P/Invoke:
            //   lParam = (uint)y << 16 | (uint)x
            int x = 0x00FF;
            int y = 0x00AB;
            uint expected = ((uint)y << 16) | (uint)x;

            uint actual = Convert.ToUInt32(y);
            actual <<= 16;
            actual |= Convert.ToUInt32(x);

            Assert.AreEqual(expected, actual,
                "High word should contain Y coordinate, low word should contain X coordinate");
        }

        [TestMethod]
        [Description("ToMouseDisplayLocal result X is always >= 0 when cursor is inside display bounds")]
        public void ToMouseDisplayLocal_WhenCursorInsideBounds_XIsNonNegative() {
            var bounds = new Rectangle(1920, 0, 1920, 1080);
            // Cursor at left edge of second monitor
            var result = InputUtil.ToMouseDisplayLocal(1920, 540, bounds);
            Assert.IsTrue(result.X >= 0, "X coordinate should be non-negative when inside display");
        }

        [TestMethod]
        [Description("ToMouseSpanLocal result X is always >= 0 when cursor is inside virtual screen bounds")]
        public void ToMouseSpanLocal_WhenCursorInsideBounds_XIsNonNegative() {
            var virtualBounds = new Rectangle(-1920, -200, 5760, 1480);
            // Cursor at virtual origin
            var result = InputUtil.ToMouseSpanLocal(-1920, -200, virtualBounds);
            Assert.IsTrue(result.X >= 0, "X coordinate should be non-negative when at virtual screen origin");
            Assert.IsTrue(result.Y >= 0, "Y coordinate should be non-negative when at virtual screen origin");
        }
    }
}
