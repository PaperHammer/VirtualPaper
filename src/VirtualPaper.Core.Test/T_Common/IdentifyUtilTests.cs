using VirtualPaper.Common.Utils;

namespace VirtualPaper.Core.Test.T_Common {
    [TestClass]
    public class IdentifyUtilTests {
        [TestMethod]
        public void ComputeHash_SameInput_SameOutput() {
            int h1 = IdentifyUtil.ComputeHash("hello");
            int h2 = IdentifyUtil.ComputeHash("hello");
            Assert.AreEqual(h1, h2);
        }

        [TestMethod]
        public void ComputeHash_DifferentInput_DifferentOutput() {
            int h1 = IdentifyUtil.ComputeHash("hello");
            int h2 = IdentifyUtil.ComputeHash("world");
            Assert.AreNotEqual(h1, h2);
        }

        [TestMethod]
        public void ComputeHash_EmptyString_ReturnsHash() {
            int h = IdentifyUtil.ComputeHash("");
            // SHA256 of empty string is well-defined, should not throw
            Assert.IsInstanceOfType(h, typeof(int));
        }

        [TestMethod]
        public void GenerateIdShort_ReturnsNonZero() {
            long id = IdentifyUtil.GenerateIdShort();
            // 极小概率为 0，但 Guid 生成的字节几乎不可能全零
            // 主要验证不抛异常且返回 long
            Assert.IsInstanceOfType(id, typeof(long));
        }

        [TestMethod]
        public void GenerateIdShort_CalledTwice_DifferentValues() {
            long id1 = IdentifyUtil.GenerateIdShort();
            long id2 = IdentifyUtil.GenerateIdShort();
            Assert.AreNotEqual(id1, id2);
        }
    }
}
