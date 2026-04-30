using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.UI.Test.T_WpSettings {
    // ── FileFilter.FileTypeToExtension 内容锁定 ───────────────────

    [TestClass]
    public class FileFilterExtensionMapTests {

        [TestMethod]
        public void FileTypeToExtension_FImage_ContainsExactExpectedExtensions() {
            string[] expected = [".jpg", ".jpeg", ".bmp", ".png", ".svg", ".webp"];
            CollectionAssert.AreEquivalent(
                expected,
                FileFilter.FileTypeToExtension[FileType.FImage],
                "FImage extensions changed. Update the picker filter and this test together.");
        }

        [TestMethod]
        public void FileTypeToExtension_FGif_ContainsExactExpectedExtensions() {
            string[] expected = [".gif", ".apng"];
            CollectionAssert.AreEquivalent(
                expected,
                FileFilter.FileTypeToExtension[FileType.FGif],
                "FGif extensions changed.");
        }

        [TestMethod]
        public void FileTypeToExtension_FVideo_ContainsExactExpectedExtensions() {
            string[] expected = [".mp4", ".webm"];
            CollectionAssert.AreEquivalent(
                expected,
                FileFilter.FileTypeToExtension[FileType.FVideo],
                "FVideo extensions changed.");
        }
    }
}
