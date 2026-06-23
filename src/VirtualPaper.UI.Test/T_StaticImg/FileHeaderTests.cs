using Workloads.Creation.StaticImg;
using Workloads.Creation.StaticImg.Models.SerializableData;

namespace VirtualPaper.UI.Test.T_StaticImg {
    [TestClass]
    public class FileHeaderTests {
        [TestMethod]
        public void Create_SetsMagicCorrectly() {
            var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            Assert.AreEqual("_VPD", System.Text.Encoding.ASCII.GetString(header.Magic));
        }

        [TestMethod]
        public void Create_SetsVersion1() {
            var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            Assert.AreEqual((ushort)1, header.Version);
        }

        [TestMethod]
        public void Create_SetsCanvasDimensions() {
            var arcSize = new ArcSize(1920, 1080, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 2, 50, 150);

            Assert.AreEqual(1920f, header.CanvasWidth);
            Assert.AreEqual(1080f, header.CanvasHeight);
        }

        [TestMethod]
        public void Create_SetsDpi() {
            var arcSize = new ArcSize(800, 600, 300, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            Assert.AreEqual(300u, header.Dpi);
        }

        [TestMethod]
        public void Create_SetsLayerCount() {
            var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 5, 100, 200);

            Assert.AreEqual(5, header.LayerCount);
        }

        [TestMethod]
        public void Create_SetsBusinessDataOffset() {
            var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            Assert.AreEqual((uint)System.Runtime.InteropServices.Marshal.SizeOf<FileHeader>(), header.BusinessDataOffset);
        }

        [TestMethod]
        public void Create_SetsLayersOffset() {
            var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            Assert.AreEqual(header.BusinessDataOffset + 100u, header.LayersOffset);
        }

        [TestMethod]
        public void Create_SetsDataLengths() {
            var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            Assert.AreEqual(100u, header.BusinessDataLength);
            Assert.AreEqual(200u, header.LayersLength);
        }

        // --- IsValid ---

        [TestMethod]
        public void IsValid_CreatedHeader_ReturnsTrue() {
            var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            Assert.IsTrue(header.IsValid());
        }

        [TestMethod]
        public void IsValid_ZeroWidth_ReturnsFalse() {
            var arcSize = new ArcSize(0, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            Assert.IsFalse(header.IsValid());
        }

        [TestMethod]
        public void IsValid_ZeroHeight_ReturnsFalse() {
            var arcSize = new ArcSize(800, 0, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            Assert.IsFalse(header.IsValid());
        }

        [TestMethod]
        public void IsValid_ZeroLayerCount_ReturnsFalse() {
            var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 0, 100, 200);

            Assert.IsFalse(header.IsValid());
        }

        [TestMethod]
        public void IsValid_DpiTooLow_ReturnsFalse() {
            var header = new FileHeader {
                Magic = System.Text.Encoding.ASCII.GetBytes("_VPD"),
                Version = 1,
                CanvasWidth = 800,
                CanvasHeight = 600,
                Dpi = 10, // below 72
                LayerCount = 1,
                BusinessDataOffset = (uint)System.Runtime.InteropServices.Marshal.SizeOf<FileHeader>(),
                BusinessDataLength = 100,
                LayersOffset = 200,
                LayersLength = 100,
            };

            Assert.IsFalse(header.IsValid());
        }

        [TestMethod]
        public void IsValid_DpiTooHigh_ReturnsFalse() {
            var header = new FileHeader {
                Magic = System.Text.Encoding.ASCII.GetBytes("_VPD"),
                Version = 1,
                CanvasWidth = 800,
                CanvasHeight = 600,
                Dpi = 2000, // above 1200
                LayerCount = 1,
                BusinessDataOffset = (uint)System.Runtime.InteropServices.Marshal.SizeOf<FileHeader>(),
                BusinessDataLength = 100,
                LayersOffset = 200,
                LayersLength = 100,
            };

            Assert.IsFalse(header.IsValid());
        }

        [TestMethod]
        public void IsValid_WrongMagic_ReturnsFalse() {
            var header = new FileHeader {
                Magic = System.Text.Encoding.ASCII.GetBytes("XXXX"),
                Version = 1,
                CanvasWidth = 800,
                CanvasHeight = 600,
                Dpi = 96,
                LayerCount = 1,
                BusinessDataOffset = (uint)System.Runtime.InteropServices.Marshal.SizeOf<FileHeader>(),
                BusinessDataLength = 100,
                LayersOffset = 200,
                LayersLength = 100,
            };

            Assert.IsFalse(header.IsValid());
        }

        [TestMethod]
        public void IsValid_WrongVersion_ReturnsFalse() {
            var header = new FileHeader {
                Magic = System.Text.Encoding.ASCII.GetBytes("_VPD"),
                Version = 99,
                CanvasWidth = 800,
                CanvasHeight = 600,
                Dpi = 96,
                LayerCount = 1,
                BusinessDataOffset = (uint)System.Runtime.InteropServices.Marshal.SizeOf<FileHeader>(),
                BusinessDataLength = 100,
                LayersOffset = 200,
                LayersLength = 100,
            };

            Assert.IsFalse(header.IsValid());
        }

        // --- GetTotalFileSize ---

        [TestMethod]
        public void GetTotalFileSize_CalculatesCorrectly() {
            var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            var expected = System.Runtime.InteropServices.Marshal.SizeOf<FileHeader>() + 100L + 200L;
            Assert.AreEqual(expected, header.GetTotalFileSize());
        }

        // --- ReservedFlag ---

        [TestMethod]
        public void ReservedFlag_DefaultIsZero() {
            var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            Assert.AreEqual(0, header.GetReservedFlag());
        }

        [TestMethod]
        public void SetReservedFlag_StoresValue() {
            var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            header.SetReservedFlag(0x42);
            Assert.AreEqual(0x42, header.GetReservedFlag());
        }

        [TestMethod]
        public void SetReservedFlag_OnlyAffectsLowByte() {
            var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
            var header = FileHeader.Create(arcSize, 1, 100, 200);

            header.SetReservedFlag(0xFF);
            Assert.AreEqual(0xFF, header.GetReservedFlag());

            header.SetReservedFlag(0x00);
            Assert.AreEqual(0x00, header.GetReservedFlag());
        }
    }
}
