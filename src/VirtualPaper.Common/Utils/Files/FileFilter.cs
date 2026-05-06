using System.Text;

namespace VirtualPaper.Common.Utils.Files {
    /// <summary>
    /// OpenFileDialog helper.
    /// </summary>
    public class FileFilter {
        public static FileType GetFileType(string filePath) {
            if (!File.Exists(filePath)) {
                return FileType.FUnknown;
            }

            string extension = Path.GetExtension(filePath).ToLower();

            byte[] headerBytes = new byte[48];
            int bytesRead;
            using (FileStream fs = new(filePath, FileMode.Open, FileAccess.Read)) {
                bytesRead = fs.Read(headerBytes, 0, 48);
            }

            if (bytesRead < 4) {
                return FileType.FUnknown;
            }

            string headerHex = BitConverter.ToString(headerBytes, 0, bytesRead)
                               .Replace("-", "").ToUpper();

            // WebP 特殊处理：RIFF 容器 + WEBP 标识
            if (headerHex.StartsWith("52494646", StringComparison.OrdinalIgnoreCase)
                && bytesRead >= 12
                && headerHex.Length >= 24
                && headerHex.Substring(16, 8) == "57454250"
                && extension == ".webp") {
                return FileType.FImage;
            }

            // 通用签名匹配
            foreach (var sig in _signatures) {
                bool matched = sig.MustStartWith
                    ? headerHex.StartsWith(sig.MagicHex, StringComparison.OrdinalIgnoreCase)
                    : headerHex.Contains(sig.MagicHex, StringComparison.OrdinalIgnoreCase);

                if (matched && sig.ValidExtensions.Contains(extension)) {
                    return sig.Type;
                }
            }

            // APNG 特殊处理：PNG 签名 + acTL chunk
            if (extension == ".apng"
                && headerHex.StartsWith("89504E470D0A1A0A", StringComparison.OrdinalIgnoreCase)) {
                string headerText = Encoding.ASCII.GetString(headerBytes, 0, bytesRead);
                if (headerText.Contains("acTL")) {
                    return FileType.FGif;
                }
            }

            return FileType.FUnknown;
        }

        public static FileType GetRuntimeFileType(string extension) {
            foreach (var kvp in FileTypeToExtension) {
                if (kvp.Value.Contains(extension)) {
                    return kvp.Key;
                }
            }
            throw new ArgumentException("No matching file type found.", nameof(extension));
        }

        public static Dictionary<FileType, string[]> FileTypeToExtension { get; } = new() {
            [FileType.FImage] = [".jpg", ".jpeg", ".bmp", ".png", ".svg", ".webp"],
            [FileType.FGif] = [".gif", ".apng"],
            [FileType.FVideo] = [".mp4", ".webm"],
            [FileType.FDesign] = [FileExtension.FE_Design],
        };

        public static string[] AvatarFilter =>
            [".jpg", ".bmp", ".png", ".jpe", ".gif", ".tif", ".tiff", ".heic", ".heif", ".heics", ".heifs", ".avif", ".avifs"];

        private record FileSignature(
            string MagicHex,
            FileType Type,
            string[] ValidExtensions,
            bool MustStartWith = false
        );

        private static readonly FileSignature[] _signatures = [
            // Image
            new("FFD8FF",           FileType.FImage, [".jpg", ".jpeg"],  MustStartWith: true),
            new("424D",             FileType.FImage, [".bmp"],           MustStartWith: true),
            new("89504E470D0A1A0A", FileType.FImage, [".png"],           MustStartWith: true),
            new("3C737667",         FileType.FImage, [".svg"],           MustStartWith: true),  // <svg
            new("3C3F786D",         FileType.FImage, [".svg"],           MustStartWith: true),  // <?xm

            // GIF
            new("474946383961",     FileType.FGif,   [".gif"],           MustStartWith: true),  // GIF89a
            new("474946383761",     FileType.FGif,   [".gif"],           MustStartWith: true),  // GIF87a

            // Video
            new("66747970",         FileType.FVideo, [".mp4"],           MustStartWith: false), // ftyp 在偏移 4
            new("1A45DFA3",         FileType.FVideo, [".webm"],          MustStartWith: true),  // EBML
        ];
    }
}
