using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Workloads.Creation.WebBackdrop.Core.Utils {
    /// <summary>
    /// 独立于文件树懒加载状态，低内存遍历项目中的实际文件。
    /// 每次只保留一个目录的直接子项，并跳过常见依赖/构建目录和重解析点。
    /// </summary>
    internal static class WebProjectFileEnumerator {
        private static readonly HashSet<string> ExcludedDirectoryNames = new(
            StringComparer.OrdinalIgnoreCase) {
                ".git",
                ".hg",
                ".svn",
                ".vs",
                "bin",
                "obj",
                "node_modules",
            };

        public static IEnumerable<string> EnumerateFiles(
            string projectFolder,
            CancellationToken token,
            int maximumFileCount = int.MaxValue) {
            if (!Directory.Exists(projectFolder) || maximumFileCount <= 0) yield break;

            var pendingFolders = new Stack<string>();
            pendingFolders.Push(projectFolder);
            var fileCount = 0;

            while (pendingFolders.Count > 0 && fileCount < maximumFileCount) {
                if (token.IsCancellationRequested) yield break;
                var folder = pendingFolders.Pop();

                string[] files;
                string[] folders;
                try {
                    files = Directory.GetFiles(folder);
                    folders = Directory.GetDirectories(folder);
                }
                catch (UnauthorizedAccessException) {
                    continue;
                }
                catch (IOException) {
                    continue;
                }

                foreach (var file in files) {
                    if (token.IsCancellationRequested || fileCount >= maximumFileCount) yield break;
                    fileCount++;
                    yield return file;
                }

                foreach (var childFolder in folders) {
                    if (token.IsCancellationRequested) yield break;
                    if (ExcludedDirectoryNames.Contains(Path.GetFileName(childFolder))) continue;

                    try {
                        if ((File.GetAttributes(childFolder) & FileAttributes.ReparsePoint) != 0) continue;
                    }
                    catch (UnauthorizedAccessException) {
                        continue;
                    }
                    catch (IOException) {
                        continue;
                    }

                    pendingFolders.Push(childFolder);
                }
            }
        }
    }
}
