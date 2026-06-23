using VirtualPaper.Common.Extensions;

namespace VirtualPaper.Core.Test.T_Common {
    [TestClass]
    public class ByteExtensionsTests {
        [TestMethod]
        public void CompressPixels_EmptyArray_ReturnsEmpty() {
            var data = Array.Empty<byte>();
            var result = data.CompressPixels();
            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void DecompressPixels_EmptyArray_ReturnsEmpty() {
            var data = Array.Empty<byte>();
            var result = data.DecompressPixels();
            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void CompressDecompress_Roundtrip_PreservesData() {
            var original = new byte[] { 255, 0, 128, 64, 32, 16, 8, 4, 2, 1 };

            var compressed = original.CompressPixels();
            var decompressed = compressed.DecompressPixels();

            CollectionAssert.AreEqual(original, decompressed);
        }

        [TestMethod]
        public void CompressDecompress_LargeData_Roundtrip() {
            var original = new byte[100_000];
            new Random(42).NextBytes(original);

            var compressed = original.CompressPixels();
            var decompressed = compressed.DecompressPixels();

            CollectionAssert.AreEqual(original, decompressed);
        }

        [TestMethod]
        public void CompressPixels_RepeatingData_CompressesSmaller() {
            // 全零数据高度可压缩
            var original = new byte[10_000];

            var compressed = original.CompressPixels();

            Assert.IsLessThan(original.Length, compressed.Length, $"Compressed ({compressed.Length}) should be smaller than original ({original.Length})");
        }

        [TestMethod]
        public void CompressPixels_RandomData_ProducesNonEmpty() {
            var original = new byte[256];
            new Random(1).NextBytes(original);

            var compressed = original.CompressPixels();

            Assert.IsNotEmpty(compressed);
        }

        [TestMethod]
        public void CompressDecompress_SingleByte_Roundtrip() {
            var original = new byte[] { 42 };

            var compressed = original.CompressPixels();
            var decompressed = compressed.DecompressPixels();

            CollectionAssert.AreEqual(original, decompressed);
        }

        [TestMethod]
        public void CompressDecompress_AllZeros_Roundtrip() {
            var original = new byte[1024];

            var compressed = original.CompressPixels();
            var decompressed = compressed.DecompressPixels();

            CollectionAssert.AreEqual(original, decompressed);
        }

        [TestMethod]
        public void CompressDecompress_AllOnes_Roundtrip() {
            var original = Enumerable.Repeat((byte)0xFF, 1024).ToArray();

            var compressed = original.CompressPixels();
            var decompressed = compressed.DecompressPixels();

            CollectionAssert.AreEqual(original, decompressed);
        }
    }
}
