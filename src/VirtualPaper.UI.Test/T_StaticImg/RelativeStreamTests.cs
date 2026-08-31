using Workloads.Creation.StaticImg.Core.Utils;

namespace VirtualPaper.UI.Test.T_StaticImg {
    [TestClass]
    public class RelativeStreamTests {
        [TestMethod]
        public async Task FixedSegment_ReadsOnlyConfiguredRange() {
            byte[] source = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();
            using var baseStream = new MemoryStream(source);
            using var segment = new RelativeStream(baseStream, origin: 5, fixedLength: 10);
            byte[] result = new byte[20];

            int read = await segment.ReadAsync(result);
            int endRead = await segment.ReadAsync(result);

            Assert.AreEqual(10, read);
            Assert.AreEqual(0, endRead);
            CollectionAssert.AreEqual(source[5..15], result[..10]);
            Assert.AreEqual(10, segment.Position);
        }

        [TestMethod]
        public void FixedSegment_SeekUsesRelativeCoordinates() {
            using var baseStream = new MemoryStream(new byte[32]);
            using var segment = new RelativeStream(baseStream, origin: 8, fixedLength: 12);

            Assert.AreEqual(4, segment.Seek(4, SeekOrigin.Begin));
            Assert.AreEqual(12, baseStream.Position);
            Assert.AreEqual(10, segment.Seek(-2, SeekOrigin.End));
            Assert.AreEqual(18, baseStream.Position);
        }

        [TestMethod]
        public void FixedSegment_RejectsSeekOutsideRange() {
            using var baseStream = new MemoryStream(new byte[32]);
            using var segment = new RelativeStream(baseStream, origin: 8, fixedLength: 12);

            Assert.Throws<IOException>(() => segment.Seek(13, SeekOrigin.Begin));
            Assert.Throws<IOException>(() => segment.Seek(-1, SeekOrigin.Begin));
        }

        [TestMethod]
        public void WritableSegment_ZeroIsMappedToOriginAndCanGrow() {
            using var baseStream = new MemoryStream();
            baseStream.Write(new byte[] { 1, 2, 3, 4 });
            using (var segment = new RelativeStream(baseStream, origin: 4)) {
                segment.Write(new byte[] { 5, 6, 7 });
                segment.Position = 0;
                segment.WriteByte(9);
            }

            CollectionAssert.AreEqual(
                new byte[] { 1, 2, 3, 4, 9, 6, 7 },
                baseStream.ToArray());
        }

        [TestMethod]
        public void WritableSegment_SetLengthPreservesPrefix() {
            using var baseStream = new MemoryStream();
            baseStream.Write(new byte[] { 1, 2, 3, 4, 5, 6 });
            using var segment = new RelativeStream(baseStream, origin: 4);

            segment.SetLength(1);

            CollectionAssert.AreEqual(
                new byte[] { 1, 2, 3, 4, 5 },
                baseStream.ToArray());
            Assert.AreEqual(1, segment.Length);
        }

        [TestMethod]
        public void Dispose_WithLeaveOpen_KeepsBaseStreamUsable() {
            using var baseStream = new MemoryStream(new byte[8]);
            var segment = new RelativeStream(baseStream, origin: 2, fixedLength: 4, leaveOpen: true);

            segment.Dispose();
            baseStream.Position = 0;

            Assert.AreEqual(0, baseStream.ReadByte());
        }
    }
}
