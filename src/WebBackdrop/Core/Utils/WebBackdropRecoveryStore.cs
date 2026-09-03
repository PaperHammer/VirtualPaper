using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace Workloads.Creation.WebBackdrop.Core.Utils {
    /// <summary>
    /// 未保存编辑的崩溃恢复存储。
    ///  - 编辑器定期把“未保存文件”的内存内容备份到 AppData 下的恢复目录；
    ///  - 异常退出后，下次打开项目时检测备份：与磁盘一致则静默清理，
    ///    不一致则提示用户恢复，避免崩溃造成用户编辑丢失。
    /// 备份文件按项目内相对路径镜像存放，写盘复用原子写入（临时文件 + 覆盖移动）。
    /// </summary>
    public static class WebBackdropRecoveryStore {
        public static string RecoveryRoot => Path.Combine(Constants.CommonPaths.AppDataDir, "WebBackdrop", "Recovery");

        public static string GetProjectRecoveryDir(string projectFolder) {
            return Path.Combine(RecoveryRoot, SanitizeName(Path.GetFileName(projectFolder)));
        }

        /// <summary>项目内文件的备份绝对路径（按相对路径镜像到恢复目录）。</summary>
        public static string GetBackupPath(string projectFolder, string filePath) {
            var relative = FileUtil.GetContainedRelativePath(projectFolder, filePath, nameof(filePath));
            return Path.GetFullPath(Path.Combine(GetProjectRecoveryDir(projectFolder), relative));
        }

        /// <summary>从备份绝对路径还原出项目内原始文件的绝对路径。</summary>
        public static string GetOriginalPath(string projectFolder, string backupPath) {
            var relative = FileUtil.GetContainedRelativePath(
                GetProjectRecoveryDir(projectFolder),
                backupPath,
                nameof(backupPath));
            return Path.GetFullPath(Path.Combine(projectFolder, relative));
        }

        public static IReadOnlyList<string> ListBackupPaths(string projectFolder) {
            var dir = GetProjectRecoveryDir(projectFolder);
            if (!Directory.Exists(dir)) return [];
            return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToList();
        }

        public static async Task WriteBackupAsync(string projectFolder, string filePath, string content, string encodingText) {
            // 空内容不备份：避免把用户“清空但未保存”的中间状态当作数据覆盖原文件
            if (string.IsNullOrEmpty(content)) return;

            try {
                var backupPath = GetBackupPath(projectFolder, filePath);
                var directory = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(directory)) {
                    Directory.CreateDirectory(directory);
                }
                await FileUtil.WriteAllTextAsync(backupPath, content, GetEncoding(encodingText));
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebDesignFileUtil>().Warn($"Failed to write editor backup: {filePath}\n{ex.Message}");
            }
        }

        public static void DeleteBackup(string projectFolder, string filePath) {
            try {
                var backupPath = GetBackupPath(projectFolder, filePath);
                if (File.Exists(backupPath)) {
                    File.Delete(backupPath);
                }
            }
            catch { /* 清理失败不影响主流程 */ }
        }

        /// <summary>把备份原子地还原到原始路径（先写临时文件再覆盖移动，避免中途崩溃损坏原文件）。</summary>
        public static async Task RestoreAsync(string projectFolder, string backupPath) {
            var originalPath = GetOriginalPath(projectFolder, backupPath);
            var bytes = await File.ReadAllBytesAsync(backupPath);

            var directory = Path.GetDirectoryName(originalPath);
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);

            var tempPath = Path.Combine(directory, $".{Path.GetRandomFileName()}.tmp");
            try {
                await File.WriteAllBytesAsync(tempPath, bytes);
                File.Move(tempPath, originalPath, overwrite: true);
            }
            finally {
                if (File.Exists(tempPath)) {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        /// <summary>字节级比较两个文件是否一致（恢复检测用，避免编码差异导致误判）。</summary>
        public static bool AreFilesEqual(string left, string right) {
            try {
                if (!File.Exists(left) || !File.Exists(right)) return false;
                return File.ReadAllBytes(left).AsSpan().SequenceEqual(File.ReadAllBytes(right));
            }
            catch {
                return false;
            }
        }

        private static Encoding GetEncoding(string encodingText) {
            return encodingText switch {
                "UTF-8 BOM" => new UTF8Encoding(true),
                "UTF-16 LE" => Encoding.Unicode,
                "UTF-16 BE" => Encoding.BigEndianUnicode,
                _ => new UTF8Encoding(false),
            };
        }

        private static string SanitizeName(string name) {
            if (string.IsNullOrWhiteSpace(name)) return "_";
            foreach (var invalid in Path.GetInvalidFileNameChars()) {
                name = name.Replace(invalid, '_');
            }
            return name;
        }
    }
}
