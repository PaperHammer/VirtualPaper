using System.Runtime.InteropServices;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using Workloads.Creation.StaticImg;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Utils.DraftUtils.Models;

namespace VirtualPaper.UI.Test.T_StaticImg {
    [TestClass]
    public class StaticImgDesignFileUtilTests {
        // ── Create 工厂 ──────────────────────────────────────────────

        [TestMethod]
        public void Create_WithValidFilePath_ReturnsInstance() {
            var tempFile = Path.GetTempFileName();
            try {
                var util = StaticImgDesignFileUtil.Create(tempFile, FileType.FImage);
                Assert.IsNotNull(util);
                Assert.AreEqual(Path.GetFullPath(tempFile), util.FilePath);
            } finally {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void Create_WithFileNameOnly_CombinesWithDocumentsDir() {
            var util = StaticImgDesignFileUtil.Create("test.vpd", FileType.FImage);
            Assert.IsNotNull(util);
            Assert.EndsWith("test.vpd", util.FilePath);
            Assert.IsTrue(Path.IsPathRooted(util.FilePath));
        }

        [TestMethod]
        public void Create_WithEmptyInput_Throws() {
            Assert.Throws<ArgumentException>(() => StaticImgDesignFileUtil.Create("", FileType.FImage));
        }

        [TestMethod]
        public void Create_WithWhitespace_Throws() {
            Assert.Throws<ArgumentException>(() => StaticImgDesignFileUtil.Create("   ", FileType.FImage));
        }

        [TestMethod]
        public void Create_WithInvalidChars_Throws() {
            Assert.Throws<ArgumentException>(() => StaticImgDesignFileUtil.Create("test<file>.vpd", FileType.FImage));
        }

        // ── ExportFormatDefult ────────────────────────────────────────

        [TestMethod]
        [DataRow("test.png", ExportImageFormat.Png)]
        [DataRow("test.bmp", ExportImageFormat.Bmp)]
        [DataRow("test.jpg", ExportImageFormat.Jpeg)]
        [DataRow("test.jpeg", ExportImageFormat.Jpeg)]
        [DataRow("test.jxr", ExportImageFormat.JpegXR)]
        [DataRow("test.vpd", ExportImageFormat.Png)] // 未知扩展名 → 默认 Png
        [DataRow("test", ExportImageFormat.Png)]     // 无扩展名 → 默认 Png
        public void Create_WithExtension_SetsExportFormat(string fileName, ExportImageFormat expected) {
            var util = StaticImgDesignFileUtil.Create(fileName, FileType.FImage);
            Assert.AreEqual(expected, util.ExportFormatDefult);
        }

        // ── IsValidVpdFile ────────────────────────────────────────────

        [TestMethod]
        public void IsValidVpdFile_ExistingVpdFile_ReturnsTrue() {
            var tempFile = Path.ChangeExtension(Path.GetTempFileName(), ".vpd");
            File.WriteAllBytes(tempFile, new byte[10]);
            try {
                var util = StaticImgDesignFileUtil.Create(tempFile, FileType.FImage);
                Assert.IsTrue(util.IsValidVpdFile);
            } finally {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void IsValidVpdFile_NonVpdExtension_ReturnsFalse() {
            var tempFile = Path.GetTempFileName(); // .tmp
            try {
                var util = StaticImgDesignFileUtil.Create(tempFile, FileType.FImage);
                Assert.IsFalse(util.IsValidVpdFile);
            } finally {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void IsValidVpdFile_NonExistentFile_ReturnsFalse() {
            var util = StaticImgDesignFileUtil.Create("nonexistent.vpd", FileType.FImage);
            Assert.IsFalse(util.IsValidVpdFile);
        }

        // ── IsSaveFromInit ────────────────────────────────────────────

        [TestMethod]
        public void IsSaveFromInit_ExistingVpdFile_True() {
            var tempFile = Path.ChangeExtension(Path.GetTempFileName(), ".vpd");
            File.WriteAllBytes(tempFile, new byte[10]);
            try {
                var util = StaticImgDesignFileUtil.Create(tempFile, FileType.FImage);
                Assert.IsTrue(util.IsSaveFromInit);
            } finally {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void IsSaveFromInit_NonVpdFile_False() {
            var tempFile = Path.GetTempFileName();
            try {
                var util = StaticImgDesignFileUtil.Create(tempFile, FileType.FImage);
                Assert.IsFalse(util.IsSaveFromInit);
            } finally {
                File.Delete(tempFile);
            }
        }

        // ── Ext / FileName / FileNameWithoutEx ────────────────────────

        [TestMethod]
        public void Ext_ReturnsCorrectExtension() {
            var util = StaticImgDesignFileUtil.Create("test.vpd", FileType.FImage);
            Assert.AreEqual(".vpd", util.Ext);
        }

        [TestMethod]
        public void FileName_ReturnsCorrectName() {
            var util = StaticImgDesignFileUtil.Create("test.vpd", FileType.FImage);
            Assert.AreEqual("test.vpd", util.FileName);
        }

        [TestMethod]
        public void FileNameWithoutEx_ReturnsCorrectName() {
            var util = StaticImgDesignFileUtil.Create("test.vpd", FileType.FImage);
            Assert.AreEqual("test", util.FileNameWithoutEx);
        }

        // ── GetFileHeaderAsync ────────────────────────────────────────

        [TestMethod]
        public async Task GetFileHeaderAsync_ValidFile_ReturnsHeader() {
            var tempFile = Path.ChangeExtension(Path.GetTempFileName(), ".vpd");
            try {
                // 构造一个合法的 FileHeader 并写入文件
                var arcSize = new ArcSize(800, 600, 96, RebuildMode.None);
                var header = FileHeader.Create(arcSize, 1, 100, 200);
                byte[] headerBytes = StructureToBytes(header);
                // 填充足够字节使文件完整
                using (var fs = File.Create(tempFile)) {
                    fs.Write(headerBytes);
                    fs.Write(new byte[300]); // business + layers 占位
                }

                var result = await StaticImgDesignFileUtil.GetFileHeaderAsync(tempFile);

                Assert.IsNotNull(result);
                Assert.AreEqual(800f, result.Value.CanvasWidth);
                Assert.AreEqual(600f, result.Value.CanvasHeight);
                Assert.AreEqual(96u, result.Value.Dpi);
                Assert.AreEqual(1, result.Value.LayerCount);
            } finally {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public async Task GetFileHeaderAsync_NonExistentFile_ReturnsNull() {
            var result = await StaticImgDesignFileUtil.GetFileHeaderAsync(@"C:\nonexistent_file.vpd");
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetFileHeaderAsync_EmptyPath_ReturnsNull() {
            var result = await StaticImgDesignFileUtil.GetFileHeaderAsync("");
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetFileHeaderAsync_NullPath_ReturnsNull() {
            var result = await StaticImgDesignFileUtil.GetFileHeaderAsync(null!);
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetFileHeaderAsync_TooSmallFile_ReturnsNull() {
            var tempFile = Path.ChangeExtension(Path.GetTempFileName(), ".vpd");
            File.WriteAllBytes(tempFile, new byte[] { 1, 2, 3 });
            try {
                var result = await StaticImgDesignFileUtil.GetFileHeaderAsync(tempFile);
                Assert.IsNull(result);
            } finally {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public async Task GetFileHeaderAsync_InvalidMagic_ReturnsNull() {
            var tempFile = Path.ChangeExtension(Path.GetTempFileName(), ".vpd");
            try {
                // 写入错误 magic
                var header = new FileHeader {
                    Magic = System.Text.Encoding.ASCII.GetBytes("XXXX"),
                    Version = 1,
                    CanvasWidth = 100,
                    CanvasHeight = 100,
                    Dpi = 96,
                    LayerCount = 1,
                    BusinessDataOffset = (uint)Marshal.SizeOf<FileHeader>(),
                    BusinessDataLength = 0,
                    LayersOffset = (uint)Marshal.SizeOf<FileHeader>(),
                    LayersLength = 0,
                };
                File.WriteAllBytes(tempFile, StructureToBytes(header));

                var result = await StaticImgDesignFileUtil.GetFileHeaderAsync(tempFile);
                Assert.IsNull(result);
            } finally {
                File.Delete(tempFile);
            }
        }

        // ── 辅助 ─────────────────────────────────────────────────────

        private static byte[] StructureToBytes<T>(T structure) where T : struct {
            int size = Marshal.SizeOf<T>();
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(structure, ptr, false);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }
    }
}
