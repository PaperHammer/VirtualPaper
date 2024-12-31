using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace VirtualPaper.Common.Utils.Files {
    public static class FileUtil {
        /// <summary>
        /// 使用资源管理器打开目标文件夹/文件，若文件存在，则被选中<br>
        /// Does NOT work under desktop bridge!</br>
        /// </summary>
        /// <param name="path"></param>
        public static void OpenFolderByExplorer(string path) {
            try {
                ProcessStartInfo startInfo = new() {
                    FileName = "explorer.exe"
                };
                if (File.Exists(path)) {
                    startInfo.Arguments = "/select, \"" + path + "\"";
                }
                else if (Directory.Exists(path)) {
                    startInfo.Arguments = "\"" + path + "\"";
                }
                else {
                    throw new FileNotFoundException();
                }
                Process.Start(startInfo);
            }
            catch { }
        }

        /// <summary>
        /// 将无效的文件名字符替换为 '-' 字符。
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <returns>Valid filename</returns>
        public static string GetSafeFilename(string filename) {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        //ref: https://stackoverflow.com/questions/1078003/c-how-would-you-make-a-unique-filename-by-adding-a-number
        public static string NextAvailableFilename(string path) {
            // Short-cut if already available
            if (!File.Exists(path))
                return path;

            var numberPattern = "({0})";

            // If path has extension then insert the number pattern just before the extension and return next filename
            if (Path.HasExtension(path))
                return GetNextFilename(path.Insert(path.LastIndexOf(Path.GetExtension(path)), numberPattern));

            // Otherwise just append the pattern to the path and return next filename
            return GetNextFilename(path + numberPattern);
        }

        private static string GetNextFilename(string pattern) {
            string tmp = string.Format(pattern, 1);
            if (tmp == pattern)
                throw new ArgumentException("The pattern must include an index place-holder", nameof(pattern));

            if (!File.Exists(tmp))
                return tmp;

            int min = 1, max = 2; // 最小值是包容性的，最大值是排他性/未经测试的

            while (File.Exists(string.Format(pattern, max))) {
                min = max;
                max *= 2;
            }

            while (max != min + 1) {
                int pivot = (max + min) / 2;
                if (File.Exists(string.Format(pattern, pivot)))
                    min = pivot;
                else
                    max = pivot;
            }

            return string.Format(pattern, max);
        }

        /// <summary>
        /// 计算文件校验和
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>SHA256 checksum.</returns>
        public static string GetChecksumSHA256(string filePath) {
            using SHA256 sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);

            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static void EmptyDirectory(string directory) {
            DirectoryInfo di = new(directory);
            di.Delete(true);
        }

        public static bool IsFileGreaterThanThreshold(string filePath, long bytes) {
            bool result = false;
            try {
                result = new FileInfo(filePath).Length > bytes;
            }
            catch {
                //KeepRun
            }

            return result;
        }

        public static async Task CopyFileAsync(string src, string dest) {
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dest) || !File.Exists(src)) return;
            
            string destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir)) {
                Directory.CreateDirectory(destDir);
            }

            using FileStream sourceStream = File.Open(src, FileMode.Open);
            using FileStream destinationStream = File.Create(dest);
            await sourceStream.CopyToAsync(destinationStream);
        }

        public static async Task<string> UpdateFileFolderPathAsync(string sourcefilePath, string sourceFolderPath, string targetFolderPath) {
            string targetFilePath = sourcefilePath.Replace(sourceFolderPath, targetFolderPath);
            await CopyFileAsync(sourcefilePath, targetFilePath);
            return targetFilePath;
        }

        /// <summary>
        /// Async folder delete operation after given delay.
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="initialDelay"></param>
        /// <param name="retryDelay"></param>
        /// <returns>True if deletion completed succesfully.</returns>
        public static async Task<bool> TryDeleteDirectoryAsync(string folderPath, int initialDelay = 1000, int retryDelay = 4000) {
            bool status = true;
            if (Directory.Exists(folderPath)) {
                await Task.Delay(initialDelay);
                try {
                    await Task.Run(() => Directory.Delete(folderPath, true));
                }
                catch (Exception) {
                    //App.Log.Errors("Folder Delete Failure {0}.\nRetrying..", ex.Message);
                    await Task.Delay(retryDelay);
                    try {
                        await Task.Run(() => Directory.Delete(folderPath, true));
                    }
                    catch (Exception) {
                        //App.Log.Errors("(Retry)Folder Delete Failure: {0}", ie.Message);
                        status = false;
                    }
                }
            }
            return status;
        }
        
        /// <summary>
        /// Async folder delete operation after given delay.
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="initialDelay"></param>
        /// <param name="retryDelay"></param>
        /// <returns>True if deletion completed succesfully.</returns>
        public static async Task<bool> TryDeleteFileAsync(string filePath, int initialDelay = 1000, int retryDelay = 4000) {
            bool status = true;
            if (File.Exists(filePath)) {
                await Task.Delay(initialDelay);
                try {
                    await Task.Run(() => File.Delete(filePath));
                }
                catch (Exception) {
                    //App.Log.Errors("Folder Delete Failure {0}.\nRetrying..", ex.Message);
                    await Task.Delay(retryDelay);
                    try {
                        await Task.Run(() => File.Delete(filePath));
                    }
                    catch (Exception) {
                        //App.Log.Errors("(Retry)Folder Delete Failure: {0}", ie.Message);
                        status = false;
                    }
                }
            }
            return status;
        }

        //ref: https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        /// <summary>
        /// Directory copy operation.
        /// </summary>
        /// <param name="sourceFolderPath"></param>
        /// <param name="destFolderPath"></param>
        /// <param name="copySubDirs"></param>
        public static void CopyDirectory(string sourceFolderPath, string destFolderPath, bool copySubDirs) {
            DirectoryInfo dir = new(sourceFolderPath);

            if (!dir.Exists) {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceFolderPath);
            }

            if (!Directory.Exists(destFolderPath)) {
                Directory.CreateDirectory(destFolderPath);
            }

            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files) {
                string tempPath = Path.Combine(destFolderPath, file.Name);
                file.CopyTo(tempPath, true);
            }

            if (copySubDirs) {
                DirectoryInfo[] dirs = dir.GetDirectories();
                foreach (DirectoryInfo subdir in dirs) {
                    string temppath = Path.Combine(destFolderPath, subdir.Name);
                    CopyDirectory(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        //ref: https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/how-to-write-a-simple-parallel-for-loop
        public static long GetDirectorySize(string path) {
            long totalSize = 0;
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            Parallel.For(0, files.Length,
                         index => {
                             FileInfo fi = new(files[index]);
                             long size = fi.Length;
                             Interlocked.Add(ref totalSize, size);
                         });
            return totalSize;
        }

        //ref: https://stackoverflow.com/questions/14488796/does-net-provide-an-easy-way-convert-bytes-to-kb-mb-gb-etc
        static readonly string[] SizeSuffixes =
                           ["bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
        public static string SizeSuffix(long value, int decimalPlaces = 1) {
            ArgumentOutOfRangeException.ThrowIfNegative(decimalPlaces);
            if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000) {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }
    }
}
