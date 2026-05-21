using System.IO;
using SharpCompress.Archives;
using SharpCompress.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models;
using VirtualPaper.Models.Cores;

namespace VirtualPaper.Utils.BasicDataBuilders {
    /// <summary>
    /// 处理 VP 标准 Web 壁纸包（FWebZip）的元数据构建
    /// 支持 .zip / .rar / .7z，包内必须含 project.json
    /// </summary>
    internal class WebZipBasicDataBuilder : IWpBasicDataBuilder {
        public void Build(string srcPath, string folderPath, string folderName, WpBasicData data, CancellationToken token) {
            // 解压压缩包（zip/rar/7z 统一走 SharpCompress）
            using (var stream = File.OpenRead(srcPath))
            using (var archive = ArchiveFactory.Open(stream)) {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory)) {
                    token.ThrowIfCancellationRequested();
                    entry.WriteToDirectory(folderPath, new ExtractionOptions {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }

            token.ThrowIfCancellationRequested();

            // 读 project.json
            string projectJsonPath = Path.Combine(folderPath, "project.json");
            if (!File.Exists(projectJsonPath))
                throw new FileNotFoundException("压缩包内缺少 project.json", projectJsonPath);

            var project = JsonSaver.Load<WpWebProjectData>(projectJsonPath, WpWebProjectDataContext.Default);

            // HTML 入口路径
            string entryRelative = NormalizePath(project.File);
            string entryAbsolute = Path.Combine(folderPath, entryRelative);
            if (!File.Exists(entryAbsolute))
                throw new FileNotFoundException($"project.json 指定的 HTML 入口不存在：{project.File}", entryAbsolute);

            data.FilePath = entryAbsolute;

            // 缩略图：将 preview 复制为库规范命名
            string thumbnailPath = Path.Combine(folderPath, folderName + Common.Constants.Field.ThumGifSuff);
            string previewRelative = NormalizePath(project.Preview);
            if (!string.IsNullOrEmpty(previewRelative)) {
                string previewAbsolute = Path.Combine(folderPath, previewRelative);
                if (File.Exists(previewAbsolute)) {
                    File.Copy(previewAbsolute, thumbnailPath, overwrite: true);
                    data.ThumbnailPath = thumbnailPath;
                }
            }
            // 找不到 preview 留空，UI 层显示默认占位图

            // 文件属性（以原始压缩包大小计）
            var fileInfo = new FileInfo(srcPath);
            double sizeMb = fileInfo.Length / 1024.0 / 1024.0;
            data.FileSize = sizeMb >= 0.01 ? $"{sizeMb:0.00} MB" : $"{fileInfo.Length / 1024.0:0.00} KB";
            data.FileExtension = Path.GetExtension(srcPath).ToLowerInvariant();
            data.AspectRatio = "-";
            data.Resolution = "-";

            // 元数据字段
            data.Title = string.IsNullOrWhiteSpace(project.Title)
                           ? Path.GetFileNameWithoutExtension(srcPath)
                           : project.Title;
            data.Desc = project.Desc;
            data.Authors = project.Authors;
            data.Tags = project.Tags;
        }

        private static string NormalizePath(string? raw) {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            return raw.Replace('\\', '/').TrimStart('/');
        }
    }
}