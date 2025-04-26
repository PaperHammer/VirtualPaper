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

            string extension = Path.GetExtension(filePath);
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
            byte[] headerBytes = new byte[48];
            fs.Read(headerBytes, 0, 48);

            string headerHex = BitConverter.ToString(headerBytes).Replace("-", "").ToUpper();

            foreach (var entry in _fileHeaderMap) {
                if (headerHex.Contains(entry.Key, StringComparison.OrdinalIgnoreCase)
                    && FileTypeToExtension[entry.Value].Contains(extension)) {
                    return entry.Value;
                }
            }

            if (extension == ".apng"
                && headerHex.StartsWith("89504E470D0A1A0A", StringComparison.OrdinalIgnoreCase)) {
                string headerText = Encoding.ASCII.GetString(headerBytes);
                if (headerText.Contains("acTL")) {
                    return FileType.FGif; // .apng
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
            [FileType.FProject] = [FileExtension.FE_Project],
        };

        public static string[] AvatarFilter =>
            [".jpg", ".bmp", ".png", ".jpe", ".gif", ".tif", ".tiff", ".heic", ".heif", ".heics", ".heifs", ".avif", ".avifs"];

        private static readonly Dictionary<string, FileType> _fileHeaderMap = new()
        {
            {"FFD8FF", FileType.FImage}, // .jpg .jpeg
            {"424D", FileType.FImage}, // .bmp
            {"89504E470D0A1A0A", FileType.FImage}, // .png
            {"3C737667", FileType.FImage}, // .svg
            {"3C3F786D", FileType.FImage}, // .svg
            {"52494646", FileType.FImage}, // .webp

            {"474946383961", FileType.FGif}, // .gif
            {"acTL", FileType.FGif}, // .anpg

            {"66747970", FileType.FVideo}, // .mp4
            {"1A45DFA3", FileType.FVideo}, // .webm
        };
    }
}
