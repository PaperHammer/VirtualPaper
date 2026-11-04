using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using VirtualPaper.Common.Logging;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace Workloads.Creation.WebBackdrop.Core.Utils {
    /// <summary>
    /// 将 Web 项目导出为 VP 标准 Web 壁纸包（FileType.FWebZip）。
    /// 导出格式遵循库（VirtualPaper.Utils.BasicDataBuilders.WebZipBasicDataBuilder）的导入规范：
    /// 一个 .zip 压缩包，包内根目录必须包含 project.json（WpWebProjectData），
    /// 并通过 file / preview 字段声明 HTML 入口文件与预览图。
    /// </summary>
    public static class WebProjectExporter {
        public const string ExportExtension = ".zip";

        /// <summary>日志分类标记（静态类不能作为 GetLogger&lt;T&gt; 的类型参数）</summary>
        private sealed class LogCategory { }

        /// <summary>调试运行时写入项目目录的临时元数据，导出时应剔除</summary>
        private const string BasicDataFileName = "wp_metadata_basic.json";

        /// <summary>
        /// 将当前项目打包为 FWebZip 包并写入 <paramref name="savePath"/>。
        /// </summary>
        /// <param name="designFileUtil">当前项目会话的文件工具（提供项目目录与 project.json 数据）</param>
        /// <param name="savePath">导出目标 .zip 路径</param>
        /// <param name="token">取消令牌</param>
        /// <returns>实际写入的 .zip 路径</returns>
        public static string Export(WebDesignFileUtil designFileUtil, string savePath, CancellationToken token = default) {
            if (designFileUtil == null) throw new ArgumentNullException(nameof(designFileUtil));
            if (string.IsNullOrWhiteSpace(savePath)) throw new ArgumentException("Export path cannot be empty", nameof(savePath));

            var projectFolder = designFileUtil.ProjectFolder;
            var projectData = designFileUtil.GetOrCreateProjectData();

            // 入口文件：以 project.json 的 file 字段为准；缺失时回退到 manifest 中 role=entry 的文件
            var entryRelative = NormalizePath(projectData.File);
            if (!File.Exists(Path.Combine(projectFolder, entryRelative))) {
                var manifestEntryRelative = designFileUtil.GetManifestItems()
                    .Where(item => string.Equals(item.Role, "entry", StringComparison.OrdinalIgnoreCase))
                    .Select(item => NormalizePath(item.Path))
                    .FirstOrDefault(relative => !string.IsNullOrWhiteSpace(relative)
                        && File.Exists(Path.Combine(projectFolder, relative)));
                if (!string.IsNullOrWhiteSpace(manifestEntryRelative)) {
                    entryRelative = manifestEntryRelative;
                    projectData.File = entryRelative;
                }
            }

            if (string.IsNullOrWhiteSpace(entryRelative) || !File.Exists(Path.Combine(projectFolder, entryRelative))) {
                throw new FileNotFoundException("The HTML entry specified in project.json does not exist",
                    Path.Combine(projectFolder, entryRelative));
            }

            // preview 允许缺失：库支持缩略图为空，UI 层用原生占位图显示
            token.ThrowIfCancellationRequested();

            // 先落盘 project.json，保证包内元数据与编辑器当前状态一致
            designFileUtil.SaveProjectData(projectData);

            // 只打包 web package 的内容：以 .vpw manifest 登记的文件为白名单，
            // 并始终包含入口、project.json 与预览图；目录中未登记的杂项文件不打包、不入库，
            // 被跳过的文件记录警告日志，便于排查导出后“资源缺失”问题。
            var allowedFiles = BuildPackageFileSet(designFileUtil, entryRelative, NormalizePath(projectData.Preview));
            var logger = ArcLog.GetLogger<LogCategory>();
            return ZipFolder(projectFolder, savePath,
                (file, relative) => {
                    if (IsExcludedFile(designFileUtil, file, relative)) return true;
                    if (allowedFiles.Contains(NormalizePath(relative))) return false;

                    logger.Warn($"Skipped file (not registered in project manifest): {relative}");
                    return true;
                },
                token);
        }

        /// <summary>
        /// 构建 Web 壁纸包的文件白名单：.vpw manifest 中登记的所有文件，
        /// 外加入口文件、元数据文件（project.json）与预览图。
        /// </summary>
        private static HashSet<string> BuildPackageFileSet(WebDesignFileUtil designFileUtil, string entryRelative, string previewRelative) {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in designFileUtil.GetManifestItems()) {
                var relative = NormalizePath(item.Path);
                if (!string.IsNullOrWhiteSpace(relative)) set.Add(relative);
            }

            set.Add(entryRelative);
            if (!string.IsNullOrWhiteSpace(previewRelative)) set.Add(previewRelative);

            var metadataRelative = NormalizePath(designFileUtil.GetManifestItems()
                .FirstOrDefault(item => string.Equals(item.Role, "metadata", StringComparison.OrdinalIgnoreCase))?.Path);
            if (string.IsNullOrWhiteSpace(metadataRelative)) metadataRelative = "project.json";
            set.Add(metadataRelative);

            return set;
        }

        /// <summary>
        /// 全量导出：将项目目录完整打包为 .zip（含 .vpw 工程文件与全部资源），
        /// 用于项目备份/迁移；不保证可被壁纸库直接导入。
        /// </summary>
        /// <param name="designFileUtil">当前项目会话的文件工具</param>
        /// <param name="savePath">导出目标 .zip 路径</param>
        /// <param name="token">取消令牌</param>
        /// <returns>实际写入的 .zip 路径</returns>
        public static string ExportFull(WebDesignFileUtil designFileUtil, string savePath, CancellationToken token = default) {
            if (designFileUtil == null) throw new ArgumentNullException(nameof(designFileUtil));
            if (string.IsNullOrWhiteSpace(savePath)) throw new ArgumentException("Export path cannot be empty", nameof(savePath));

            return ZipFolder(designFileUtil.ProjectFolder, savePath, null, token);
        }

        private static string ZipFolder(string sourceFolder, string savePath, Func<string, string, bool>? shouldExclude, CancellationToken token) {
            var tempDir = Path.Combine(Path.GetTempPath(), "vp_webexport_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try {
                foreach (var file in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories)) {
                    token.ThrowIfCancellationRequested();

                    var relative = Path.GetRelativePath(sourceFolder, file);
                    if (shouldExclude?.Invoke(file, relative) == true) continue;

                    var target = Path.Combine(tempDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target, overwrite: true);
                }

                token.ThrowIfCancellationRequested();

                if (File.Exists(savePath)) File.Delete(savePath);
                ZipFile.CreateFromDirectory(tempDir, savePath, CompressionLevel.Optimal, includeBaseDirectory: false);

                return savePath;
            }
            finally {
                TryDeleteDirectory(tempDir);
            }
        }

        private static bool IsExcludedFile(WebDesignFileUtil designFileUtil, string file, string relative) {
            // .vpw 工程文件仅供编辑器使用，库导入不需要
            if (designFileUtil.IsProjectFile(file)) return true;
            // 调试运行时写入的临时元数据
            if (string.Equals(Path.GetFileName(relative), BasicDataFileName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string NormalizePath(string? raw) {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            return raw.Replace('\\', '/').TrimStart('/');
        }

        private static void TryDeleteDirectory(string path) {
            try {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch { /* 忽略临时目录清理失败 */ }
        }
    }
}
