using Workloads.Creation.StaticImg.Core.Utils;

namespace StaticImg.Test.T_StaticImg {
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

        [TestMethod]
        public void Constructor_RejectsInvalidRangesAndNonSeekableStream() {
            using var baseStream = new MemoryStream(new byte[8]);
            using var nonSeekable = new NonSeekableStream();

            Assert.Throws<ArgumentNullException>(
                () => new RelativeStream(null!, origin: 0));
            Assert.Throws<ArgumentException>(
                () => new RelativeStream(nonSeekable, origin: 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RelativeStream(baseStream, origin: -1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RelativeStream(baseStream, origin: 9));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RelativeStream(baseStream, origin: 4, fixedLength: -1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RelativeStream(baseStream, origin: 4, fixedLength: 5));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RelativeStream(baseStream, origin: 1, fixedLength: long.MaxValue));
        }

        [TestMethod]
        public void Read_RepositionsBaseStreamBeforeEveryOperation() {
            using var baseStream = new MemoryStream(Enumerable.Range(0, 16)
                .Select(static value => (byte)value)
                .ToArray());
            using var segment = new RelativeStream(baseStream, origin: 5, fixedLength: 4);

            Assert.AreEqual(5, segment.ReadByte());
            baseStream.Position = baseStream.Length;

            Assert.AreEqual(6, segment.ReadByte());
            Assert.AreEqual(2, segment.Position);
        }

        [TestMethod]
        public void SetLength_TruncatesBaseStreamAndClampsPosition() {
            using var baseStream = new MemoryStream(new byte[12], writable: true);
            using var segment = new RelativeStream(baseStream, origin: 4);
            segment.Position = 7;

            segment.SetLength(3);

            Assert.AreEqual(7, baseStream.Length);
            Assert.AreEqual(3, segment.Length);
            Assert.AreEqual(3, segment.Position);
        }

        [TestMethod]
        public void Write_AfterSeekingPastEnd_ZeroFillsGap() {
            using var baseStream = new MemoryStream();
            baseStream.Write(new byte[] { 10, 11, 12, 13 });
            using var segment = new RelativeStream(baseStream, origin: 2);
            segment.Position = 4;

            segment.WriteByte(99);

            CollectionAssert.AreEqual(
                new byte[] { 10, 11, 12, 13, 0, 0, 99 },
                baseStream.ToArray());
            Assert.AreEqual(5, segment.Position);
        }

        [TestMethod]
        public void Read_ArrayOverloadValidatesBufferRange() {
            using var baseStream = new MemoryStream(new byte[8]);
            using var segment = new RelativeStream(baseStream, origin: 0, fixedLength: 8);
            byte[] buffer = new byte[4];

            Assert.Throws<ArgumentNullException>(() => segment.Read(null!, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => segment.Read(buffer, -1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => segment.Read(buffer, 0, -1));
            Assert.Throws<ArgumentException>(() => segment.Read(buffer, 5, 0));
            Assert.Throws<ArgumentException>(() => segment.Read(buffer, 3, 2));
        }

        [TestMethod]
        public void SpanRead_StopsAtFixedBoundary() {
            byte[] source = Enumerable.Range(0, 12).Select(static value => (byte)value).ToArray();
            using var baseStream = new MemoryStream(source);
            using var segment = new RelativeStream(baseStream, origin: 3, fixedLength: 4);
            Span<byte> result = stackalloc byte[6];

            int read = segment.Read(result);

            Assert.AreEqual(4, read);
            CollectionAssert.AreEqual(source[3..7], result[..read].ToArray());
            Assert.AreEqual(0, segment.Read(result));
        }

        [TestMethod]
        public async Task WritableSegment_SpanAndAsyncWritesUseRelativePosition() {
            using var baseStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 0, 0, 0, 0 });
            using var segment = new RelativeStream(baseStream, origin: 4);

            segment.Write(new byte[] { 5, 6 }.AsSpan());
            await segment.WriteAsync(new byte[] { 7, 8 });

            CollectionAssert.AreEqual(
                new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                baseStream.ToArray());
            Assert.AreEqual(4, segment.Position);
        }

        [TestMethod]
        public void FixedSegment_IsReadOnly() {
            using var baseStream = new MemoryStream(new byte[8]);
            using var segment = new RelativeStream(baseStream, origin: 2, fixedLength: 4);

            Assert.IsFalse(segment.CanWrite);
            Assert.Throws<NotSupportedException>(() => segment.WriteByte(1));
            Assert.Throws<NotSupportedException>(() => segment.SetLength(2));
        }

        [TestMethod]
        public void BinaryReaderAndWriter_RoundTripWithinRelativeCoordinates() {
            using var baseStream = new MemoryStream(new byte[16]);
            using var segment = new RelativeStream(baseStream, origin: 8);
            using (var writer = new BinaryWriter(segment, System.Text.Encoding.UTF8, leaveOpen: true)) {
                writer.Write(0x12345678);
                writer.Write((short)42);
            }

            segment.Position = 0;
            using var reader = new BinaryReader(segment, System.Text.Encoding.UTF8, leaveOpen: true);

            Assert.AreEqual(0x12345678, reader.ReadInt32());
            Assert.AreEqual(42, reader.ReadInt16());
            Assert.AreEqual(6, segment.Position);
        }

        [TestMethod]
        public void Dispose_WithoutLeaveOpen_DisposesBaseStreamAndSegment() {
            var baseStream = new MemoryStream(new byte[8]);
            var segment = new RelativeStream(baseStream, origin: 2, leaveOpen: false);

            segment.Dispose();

            Assert.IsFalse(segment.CanRead);
            Assert.IsFalse(segment.CanSeek);
            Assert.IsFalse(segment.CanWrite);
            Assert.Throws<ObjectDisposedException>(() => _ = segment.Length);
            Assert.Throws<ObjectDisposedException>(() => _ = baseStream.Position);
        }

        private sealed class NonSeekableStream : MemoryStream {
            public override bool CanSeek => false;
        }
    }
}
