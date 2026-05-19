using System.Text;

namespace VirtualPaper.Common.Utils.Files {
    /// <summary>
    /// OpenFileDialog helper.
    /// </summary>
    public class FileFilter {
        public static FileType GetFileType(string filePath) {
            if (!TryReadHeader(filePath, out string extension, out string headerHex, out byte[] headerBytes, out int bytesRead)) {
                return FileType.FUnknown;
            }

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

        /// <summary>
        /// 在 <see cref="GetFileType"/> 基础上进行更精细的分类：
        /// 对 AI 模型支持的光栅图像格式（jpg / jpeg / bmp / png / webp）做魔术头校验，
        /// 验证通过则返回 <see cref="FileType.FimageAI"/>；其余格式回落到 <see cref="GetFileType"/> 的结果。
        /// </summary>
        public static FileType GetFileTypeFroImageAI(string filePath) {
            if (!TryReadHeader(filePath, out string extension, out string headerHex, out byte[] headerBytes, out int bytesRead)) {
                return FileType.FUnknown;
            }

            // WebP 特殊处理 → FimageAI
            if (headerHex.StartsWith("52494646", StringComparison.OrdinalIgnoreCase)
                && bytesRead >= 12
                && headerHex.Length >= 24
                && headerHex.Substring(16, 8) == "57454250"
                && extension == ".webp") {
                return FileType.FimageAI;
            }

            // 优先匹配 AI 签名
            foreach (var sig in _signaturesAI) {
                bool matched = sig.MustStartWith
                    ? headerHex.StartsWith(sig.MagicHex, StringComparison.OrdinalIgnoreCase)
                    : headerHex.Contains(sig.MagicHex, StringComparison.OrdinalIgnoreCase);

                if (matched && sig.ValidExtensions.Contains(extension)) {
                    return sig.Type;
                }
            }

            // 回落到通用签名（FGif / FVideo / FImage(svg) 等）
            foreach (var sig in _signatures) {
                bool matched = sig.MustStartWith
                    ? headerHex.StartsWith(sig.MagicHex, StringComparison.OrdinalIgnoreCase)
                    : headerHex.Contains(sig.MagicHex, StringComparison.OrdinalIgnoreCase);

                if (matched && sig.ValidExtensions.Contains(extension)) {
                    return sig.Type;
                }
            }

            // APNG 特殊处理
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
            [FileType.FimageAI] = FImageAIExts,
            [FileType.FWebZip] = [".zip", ".rar", ".7z"],
        };

        /// <summary>
        /// AI 模型（超分辨率、风格迁移等）支持处理的光栅图像格式。
        /// 不含 <c>.svg</c>：OpenCV <c>ImRead</c> 无法解析矢量格式。
        /// </summary>
        public static string[] FImageAIExts => [".jpg", ".jpeg", ".png", ".bmp", ".webp"];

        public static string[] AvatarFilter =>
            [".jpg", ".bmp", ".png", ".jpe", ".gif", ".tif", ".tiff", ".heic", ".heif", ".heics", ".heifs", ".avif", ".avifs"];

        /// <summary>
        /// 读取文件头部字节，失败（文件不存在或过短）时返回 false。
        /// </summary>
        private static bool TryReadHeader(
            string filePath,
            out string extension,
            out string headerHex,
            out byte[] headerBytes,
            out int bytesRead) {

            extension = string.Empty;
            headerHex = string.Empty;
            headerBytes = [];
            bytesRead = 0;

            if (!File.Exists(filePath)) return false;

            extension = Path.GetExtension(filePath).ToLower();
            headerBytes = new byte[48];

            using (FileStream fs = new(filePath, FileMode.Open, FileAccess.Read)) {
                bytesRead = fs.Read(headerBytes, 0, 48);
            }

            if (bytesRead < 4) return false;

            headerHex = BitConverter.ToString(headerBytes, 0, bytesRead)
                                    .Replace("-", "").ToUpper();
            return true;
        }

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

            // Archive (FWebZip)
            new("504B0304",         FileType.FWebZip, [".zip"],           MustStartWith: true),  // ZIP local file header
            new("504B0506",         FileType.FWebZip, [".zip"],           MustStartWith: true),  // ZIP empty archive (end of central directory)
            new("504B0708",         FileType.FWebZip, [".zip"],           MustStartWith: true),  // ZIP spanned archive
            new("526172211A07",     FileType.FWebZip, [".rar"],           MustStartWith: true),  // RAR4 (Rar!\x1a\x07)
            new("526172211A070100", FileType.FWebZip, [".rar"],           MustStartWith: true),  // RAR5
            new("377ABCAF271C",     FileType.FWebZip, [".7z"],            MustStartWith: true),  // 7z (7z\xbc\xaf\x27\x1c)
        ];

        /// <summary>
        /// AI 场景专用签名：与 <see cref="_signatures"/> 中的光栅图像条目对应，
        /// 但类型为 <see cref="FileType.FimageAI"/>，不含 svg 等矢量格式。
        /// </summary>
        private static readonly FileSignature[] _signaturesAI = [
            new("FFD8FF",           FileType.FimageAI, [".jpg", ".jpeg"],  MustStartWith: true),
            new("424D",             FileType.FimageAI, [".bmp"],           MustStartWith: true),
            new("89504E470D0A1A0A", FileType.FimageAI, [".png"],           MustStartWith: true),
        ];
    }
}
