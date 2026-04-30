using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.UI.Test.T_WpSettings {
    [TestClass]
    public class FileFilterGetFileTypeTests {

        private string _testDir = null!;

        [TestInitialize]
        public void Setup() {
            _testDir = Path.Combine(Path.GetTempPath(), $"FileFilterTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        [TestCleanup]
        public void Cleanup() {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }

        // ── FImage ────────────────────────────────────────────────────

        [TestMethod]
        public void GetFileType_JpgWithValidHeader_ReturnsFImage() {
            var path = CreateFile("test.jpg", Hex("FFD8FF E0 00 10 4A 46 49 46"));
            Assert.AreEqual(FileType.FImage, FileFilter.GetFileType(path));
        }

        [TestMethod]
        public void GetFileType_JpegWithValidHeader_ReturnsFImage() {
            var path = CreateFile("test.jpeg", Hex("FFD8FF E0 00 10 4A 46 49 46"));
            Assert.AreEqual(FileType.FImage, FileFilter.GetFileType(path));
        }

        [TestMethod]
        public void GetFileType_BmpWithValidHeader_ReturnsFImage() {
            var path = CreateFile("test.bmp", Hex("424D 3A 00 00 00 00 00"));
            Assert.AreEqual(FileType.FImage, FileFilter.GetFileType(path));
        }

        [TestMethod]
        public void GetFileType_PngWithValidHeader_ReturnsFImage() {
            // PNG 头但不含 acTL（不是 APNG）
            var path = CreateFile("test.png", PngHeaderOnly());
            Assert.AreEqual(FileType.FImage, FileFilter.GetFileType(path));
        }

        [TestMethod]
        public void GetFileType_SvgXmlWithValidHeader_ReturnsFImage() {
            // 3C3F786D = "<?xm"（svg 以 <?xml 开头）
            var path = CreateFile("test.svg", Hex("3C3F786D 6C20 7665 7273 696F 6E"));
            Assert.AreEqual(FileType.FImage, FileFilter.GetFileType(path));
        }

        [TestMethod]
        public void GetFileType_SvgTagWithValidHeader_ReturnsFImage() {
            // 3C737667 = "<svg"
            var path = CreateFile("test.svg", Hex("3C737667 2076 6572 7369 6F6E"));
            Assert.AreEqual(FileType.FImage, FileFilter.GetFileType(path));
        }

        [TestMethod]
        public void GetFileType_WebpWithValidHeader_ReturnsFImage() {
            // RIFF????WEBP：52494646 + 4字节大小 + 57455250
            var header = new byte[48];
            var riff = Hex("52494646");
            var webp = Hex("57455250");
            Array.Copy(riff, 0, header, 0, 4);
            Array.Copy(webp, 0, header, 8, 4);
            var path = CreateFile("test.webp", header);
            Assert.AreEqual(FileType.FImage, FileFilter.GetFileType(path));
        }

        // ── FGif ──────────────────────────────────────────────────────

        [TestMethod]
        public void GetFileType_GifWithValidHeader_ReturnsFGif() {
            // 474946383961 = "GIF89a"
            var path = CreateFile("test.gif", Hex("474946383961 00 00"));
            Assert.AreEqual(FileType.FGif, FileFilter.GetFileType(path));
        }

        [TestMethod]
        public void GetFileType_ApngWithAcTLChunk_ReturnsFGif() {
            var path = CreateFile("test.apng", ApngHeader());
            Assert.AreEqual(FileType.FGif, FileFilter.GetFileType(path));
        }

        [TestMethod]
        public void GetFileType_PngWithoutAcTL_ReturnsFImage() {
            // .png 扩展名 + PNG 头但不含 acTL，应返回 FImage 而非 FGif
            var path = CreateFile("test.png", PngHeaderOnly());
            Assert.AreEqual(FileType.FImage, FileFilter.GetFileType(path));
        }

        // ── FVideo ────────────────────────────────────────────────────

        [TestMethod]
        public void GetFileType_Mp4WithValidHeader_ReturnsFVideo() {
            // 66747970 = "ftyp"，出现在偏移 4 处
            var header = new byte[48];
            var ftyp = Hex("66747970");
            Array.Copy(ftyp, 0, header, 4, 4);
            var path = CreateFile("test.mp4", header);
            Assert.AreEqual(FileType.FVideo, FileFilter.GetFileType(path));
        }

        [TestMethod]
        public void GetFileType_WebmWithValidHeader_ReturnsFVideo() {
            // 1A45DFA3 = EBML 头
            var path = CreateFile("test.webm", Hex("1A45DFA3 01 00 00 00"));
            Assert.AreEqual(FileType.FVideo, FileFilter.GetFileType(path));
        }

        // ── 异常 / 边界 ───────────────────────────────────────────────

        [TestMethod]
        public void GetFileType_FileNotExists_ReturnsFUnknown() {
            var path = Path.Combine(_testDir, "nonexistent.jpg");
            Assert.AreEqual(FileType.FUnknown, FileFilter.GetFileType(path));
        }

        [TestMethod]
        public void GetFileType_ValidExtensionButWrongHeader_ReturnsFUnknown() {
            // .jpg 后缀但写入 PNG 魔数，头部与后缀不匹配
            var path = CreateFile("test.jpg", PngHeaderOnly());
            Assert.AreEqual(FileType.FUnknown, FileFilter.GetFileType(path));
        }

        [TestMethod]
        public void GetFileType_UnknownExtension_ReturnsFUnknown() {
            var path = CreateFile("test.xyz", Hex("FFD8FF E0"));
            Assert.AreEqual(FileType.FUnknown, FileFilter.GetFileType(path));
        }

        [TestMethod]
        public void GetFileType_EmptyFile_ReturnsFUnknown() {
            var path = Path.Combine(_testDir, "empty.jpg");
            File.WriteAllBytes(path, []);
            Assert.AreEqual(FileType.FUnknown, FileFilter.GetFileType(path));
        }

        // ── 辅助方法 ──────────────────────────────────────────────────

        /// <summary>
        /// 创建临时文件并写入指定字节，返回路径。
        /// 若 content 不足 48 字节，自动补零至 48 字节（保证 fs.Read 正常）。
        /// </summary>
        private string CreateFile(string fileName, byte[] content) {
            var path = Path.Combine(_testDir, fileName);
            var padded = new byte[Math.Max(48, content.Length)];
            Array.Copy(content, padded, content.Length);
            File.WriteAllBytes(path, padded);
            return path;
        }

        /// <summary>
        /// 将带空格的 Hex 字符串转换为字节数组，方便直接写魔数。
        /// 例如 "FFD8FF E0" → [0xFF, 0xD8, 0xFF, 0xE0]
        /// </summary>
        private static byte[] Hex(string hex) {
            var clean = hex.Replace(" ", "");
            var bytes = new byte[clean.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);
            return bytes;
        }

        /// <summary>
        /// 标准 PNG 头（不含 acTL），对应普通 .png 文件。
        /// 89504E470D0A1A0A + IHDR chunk
        /// </summary>
        private static byte[] PngHeaderOnly() {
            // 8字节PNG签名 + IHDR chunk（不含acTL）
            return Hex("89504E47 0D0A1A0A 00000001 49484452");
        }

        /// <summary>
        /// APNG 头：PNG 签名 + 头部前 48 字节内嵌入 "acTL" 字符串。
        /// GetFileType 用 ASCII 解码后检查 headerText.Contains("acTL")。
        /// </summary>
        private static byte[] ApngHeader() {
            var header = new byte[48];

            // PNG 签名
            var sig = Hex("89504E470D0A1A0A");
            Array.Copy(sig, 0, header, 0, 8);

            // 在偏移 8 后写入 "acTL"（ASCII）
            var acTL = System.Text.Encoding.ASCII.GetBytes("acTL");
            Array.Copy(acTL, 0, header, 8, 4);

            return header;
        }
    }
}
