using System.Text;
using ICSharpCode.SharpZipLib.Zip;

namespace VirtualPaper.Common.Utils.Files {
    /// <summary>
    /// OpenFileDialog helper.
    /// </summary>
    public class FileFilter {
        public static WpFileType GetWpFileType(string filePath) {
            if (!File.Exists(filePath)) {
                return WpFileType.FUnknown;
            }

            string extension = Path.GetExtension(filePath);
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
            byte[] headerBytes = new byte[48];
            fs.Read(headerBytes, 0, 48);

            string headerHex = BitConverter.ToString(headerBytes).Replace("-", "").ToUpper();

            foreach (var entry in _fileHeaderMap) {
                if (headerHex.Contains(entry.Key, StringComparison.OrdinalIgnoreCase)
                    && WpFileTypeToExtension[entry.Value].Contains(extension.ToLower())) {
                    return entry.Value;
                }
            }

            if (extension == ".apng"
                && headerHex.StartsWith("89504E470D0A1A0A", StringComparison.OrdinalIgnoreCase)) {
                string headerText = Encoding.ASCII.GetString(headerBytes);
                if (headerText.Contains("acTL")) {
                    return WpFileType.FGif; // .apng
                }
            }

            return WpFileType.FUnknown;
        }

        public static WpFileType GetWpFileTypeWithExt(string extension) {
            foreach (var kvp in WpFileTypeToExtension) {
                if (kvp.Value.Contains(extension)) {
                    return kvp.Key;
                }
            }
            throw new ArgumentException("No matching file type found.", nameof(extension));
        }

        public static DeskPetEngineType GetDpFileType(string filePath) {
            if (!File.Exists(filePath) || !DeskPetFilter.Contains(Path.GetExtension(filePath).ToLower())) {
                return DeskPetEngineType.Unknown;
            }

            try {
                using FileStream fsInput = File.OpenRead(filePath);
                using var zf = new ZipFile(fsInput);
                bool hasModel3Json = false;
                bool hasSpineAtlas = false;
                bool hasSpineSkel = false;
                bool hasSpineJson = false;
                bool hasIndexHtml = false;
                bool hasActionsXml = false;
                bool hasBehaviorsXml = false;

                // 用于单媒体文件(Gif/Video)判断
                int validFileCount = 0;
                string predominantExt = string.Empty;

                foreach (ZipEntry entry in zf) {
                    // 忽略文件夹
                    if (!entry.IsFile) continue;

                    string nameLower = entry.Name.ToLowerInvariant();
                    string fileName = Path.GetFileName(nameLower);

                    // 过滤掉系统自带的隐藏/垃圾文件，防止干扰单文件判断
                    if (nameLower.Contains("__macosx") ||
                        fileName == ".ds_store" ||
                        fileName == "thumbs.db" ||
                        fileName == "desktop.ini") {
                        continue;
                    }

                    validFileCount++;
                    string ext = Path.GetExtension(fileName);

                    if (validFileCount == 1) {
                        predominantExt = ext;
                    }

                    // Live2D 特征: 包含 .model3.json 文件
                    if (nameLower.EndsWith(".model3.json")) {
                        hasModel3Json = true;
                    }
                    // Spine 特征: 包含 .atlas，同时包含 .skel 或 普通的 .json
                    else if (ext == ".atlas") {
                        hasSpineAtlas = true;
                    }
                    else if (ext == ".skel") {
                        hasSpineSkel = true;
                    }
                    else if (ext == ".json") {
                        hasSpineJson = true;
                    }
                    // WebHtml 特征: 包含入口 index.html
                    else if (fileName == "index.html") {
                        hasIndexHtml = true;
                    }
                    // Shimeji 传统桌宠特征: 动作定义和行为定义 XML
                    else if (fileName == "actions.xml") {
                        hasActionsXml = true;
                    }
                    else if (fileName == "behaviors.xml") {
                        hasBehaviorsXml = true;
                    }
                }

                // === 综合裁定类型 (按优先级从高到低) ===

                // 优先级1: Live2D
                if (hasModel3Json) {
                    return DeskPetEngineType.Live2D;
                }

                // 优先级2: Spine (必须要有图集和骨骼数据)
                if (hasSpineAtlas && (hasSpineSkel || hasSpineJson)) {
                    return DeskPetEngineType.Spine;
                }

                // 优先级3: 序列帧桌宠
                if (hasActionsXml && hasBehaviorsXml) {
                    return DeskPetEngineType.SpriteSheet;
                }

                // 优先级4: Web网页端
                if (hasIndexHtml) {
                    return DeskPetEngineType.WebHtml;
                }

                // 优先级5: 单媒体文件(动图/透明视频)
                // 如果有效文件仅有 1 个，判断其扩展名
                if (validFileCount == 1) {
                    if (predominantExt == ".gif") {
                        return DeskPetEngineType.Gif;
                    }
                    if (predominantExt == ".webm" || predominantExt == ".mov" || predominantExt == ".mp4") {
                        return DeskPetEngineType.VideoAlpha;
                    }
                }
            }
            catch {
                return DeskPetEngineType.Unknown;
            }

            return DeskPetEngineType.Unknown;
        }

        public static Dictionary<WpFileType, string[]> WpFileTypeToExtension { get; } = new() {
            [WpFileType.FImage] = [".jpg", ".jpeg", ".bmp", ".png", ".svg", ".webp"],
            [WpFileType.FGif] = [".gif", ".apng"],
            [WpFileType.FVideo] = [".mp4", ".webm"],
            [WpFileType.FDesign] = [FileExtension.FE_Design],
        };

        public static string[] AvatarFilter =>
            [".jpg", ".bmp", ".png", ".jpe", ".gif", ".tif", ".tiff", ".heic", ".heif", ".heics", ".heifs", ".avif", ".avifs"];

        public static string[] DeskPetFilter =>
            [".zip"];

        private static readonly Dictionary<string, WpFileType> _fileHeaderMap = new()
        {
            {"FFD8FF", WpFileType.FImage}, // .jpg .jpeg
            {"424D", WpFileType.FImage}, // .bmp
            {"89504E470D0A1A0A", WpFileType.FImage}, // .png
            {"3C737667", WpFileType.FImage}, // .svg
            {"3C3F786D", WpFileType.FImage}, // .svg
            {"52494646", WpFileType.FImage}, // .webp

            {"474946383961", WpFileType.FGif}, // .gif
            {"acTL", WpFileType.FGif}, // .anpg

            {"66747970", WpFileType.FVideo}, // .mp4
            {"1A45DFA3", WpFileType.FVideo}, // .webm
        };
    }
}
