using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace VirtualPaper.Common.Utils.Files {
    public static class FileUtil {
        public static string GetTempFile(string directory, string extension = "") {
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            string fileName = Path.GetRandomFileName();
            if (!string.IsNullOrEmpty(extension)) {
                fileName = Path.ChangeExtension(fileName, extension);
            }

            string fullPath = Path.Combine(directory, fileName);
            using (File.Create(fullPath)) { }

            return fullPath;
        }

        // 当前用户的“文档”目录，如 C:\Users\用户名\Documents
        public static string GetDocumentsDir() {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        // 当前用户的 AppData\Roaming，如 C:\Users\用户名\AppData\Roaming
        public static string GetRoamingAppDataDir() {
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        // 当前用户的 AppData\Local，如 C:\Users\用户名\AppData\Local
        public static string GetLocalAppDataDir() {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        // 桌面路径：C:\Users\用户名\Desktop
        public static string GetDesktopDir() {
            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

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
                    startInfo.ArgumentList.Add("/select,");
                    startInfo.ArgumentList.Add(path);
                }
                else if (Directory.Exists(path)) {
                    startInfo.ArgumentList.Add(path);
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
            // Short-cut if already Available
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
        /// 计算文件 SHA256 校验和
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>SHA256 checksum (小写 hex)</returns>
        public static string GetChecksumSHA256(string filePath) {
            using SHA256 sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);

            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// 验证文件 SHA256 是否与预期值匹配
        /// </summary>
        public static async Task<bool> VerifyFileIntegrityAsync(string filePath, string expectedSha256, CancellationToken token = default) {
            if (!File.Exists(filePath) || !IsValidSHA256(expectedSha256))
                return false;
            var actual = await CalculateFileSHA256Async(filePath, token);
            return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<string> CalculateFileSHA256Async(string filePath, CancellationToken token) {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
            var hashBytes = await sha256.ComputeHashAsync(stream, token);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        private static bool IsValidSHA256(string sha256) {
            if (string.IsNullOrEmpty(sha256) || sha256.Length != 64)
                return false;
            return System.Text.RegularExpressions.Regex.IsMatch(sha256, @"^[a-fA-F0-9]{64}$");
        }

        /// <summary>
        /// 计算字符串内容的 SHA256 校验和（UTF-8 编码）
        /// 自动处理 BOM：如果内容以 UTF-8 BOM 开头，会先移除再计算
        /// </summary>
        /// <param name="content">字符串内容</param>
        /// <returns>SHA256 checksum (小写 hex)</returns>
        public static string GetChecksumSHA256FromContent(string content) {
            // 移除 UTF-8 BOM（如果存在）
            if (content.Length > 0 && content[0] == '\uFEFF') {
                content = content.Substring(1);
            }
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static void RemoveDirectory(string directory) {
            if (!Directory.Exists(directory)) return;

            DirectoryInfo di = new(directory);
            di.Delete(true);
        }

        /// <summary>
        /// 清空目录内容（删除所有文件和子目录），但保留目录本身。
        /// 使用 Enumerate 延迟求值，适合大目录。
        /// 跳过符号链接和联接点，避免意外删除外部文件。
        /// </summary>
        /// <param name="dirPath">要清空的目录路径</param>
        public static void DeleteDirectoryContents(string dirPath) {
            if (!Directory.Exists(dirPath)) return;

            var dir = new DirectoryInfo(dirPath);
            var options = new EnumerationOptions {
                AttributesToSkip = System.IO.FileAttributes.ReparsePoint  // 跳过 symlink/junction
            };

            // 先删文件（延迟枚举，不一次性加载全部路径）
            foreach (var file in dir.EnumerateFiles("*", options)) {
                // 清除只读属性
                if (file.Attributes.HasFlag(System.IO.FileAttributes.ReadOnly)) {
                    file.Attributes &= ~System.IO.FileAttributes.ReadOnly;
                }
                file.Delete();
            }

            // 再删子目录（递归删除，跳过 reparse point）
            foreach (var subDir in dir.EnumerateDirectories("*", options)) {
                subDir.Delete(true);
            }
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
            if (string.IsNullOrEmpty(sourcefilePath)) return string.Empty;
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

        public static bool IsValidFilePath(string path) {
            try {
                if (Path.GetInvalidPathChars().Any(path.Contains))
                    return false;

                // GetInvalidPathChars 不含 < > | " 等，需逐段检查文件名非法字符
                var invalidFileNameChars = new HashSet<char>(Path.GetInvalidFileNameChars());
                foreach (var segment in path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)) {
                    if (segment.Length >= 2 && segment[1] == ':') continue; // 跳过盘符 "C:"
                    if (segment.Any(c => invalidFileNameChars.Contains(c)))
                        return false;
                }

                string? dir = Path.GetDirectoryName(path);
                return !string.IsNullOrEmpty(dir) && Directory.Exists(dir);
            }
            catch {
                return false;
            }
        }

        public static bool IsValidFileName(string name) {
            try {
                return !Path.GetInvalidFileNameChars().Any(name.Contains) &&
                       name.Length <= 255 &&
                       !string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(name));
            }
            catch {
                return false;
            }
        }

        public static string GetFileSize(string filePath) {
            try {
                FileInfo fi = new(filePath);
                return SizeSuffix(fi.Length);
            }
            catch {
                return "0 bytes";
            }
        }

        public static async Task<StorageFile?> GetAppxFileAsync(string msAppxPath) {
            try {
                var uri = new Uri(msAppxPath);
                string relativePath = uri.AbsolutePath.TrimStart('/');

                string filePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                var sf = await StorageFile.GetFileFromPathAsync(filePath);
                //var file = await StorageFile.GetFileFromApplicationUriAsync(uri); //todo?
                return sf;
            }
            catch {
                return null;
            }
        }

        public static async Task<string> GetAppxFileSizeAsync(string msAppxPath) {
            try {
                var file = await GetAppxFileAsync(msAppxPath);
                if (file == null) return "0 bytes";
                var properties = await file.GetBasicPropertiesAsync();
                return SizeSuffix((long)properties.Size);
            }
            catch {
                return "0 bytes";
            }
        }

        public static async Task<(uint Width, uint Height)> GetImageResolutionAsync(string filePath) {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            using var stream = await file.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            return (decoder.PixelWidth, decoder.PixelHeight);
        }

        //ref: https://stackoverflow.com/questions/14488796/does-net-provide-an-easy-way-convert-bytes-to-kb-mb-gb-etc
        static readonly string[] SizeSuffixes = ["bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
        public static string SizeSuffix(long value, int decimalPlaces = 1) {
            ArgumentOutOfRangeException.ThrowIfNegative(decimalPlaces);
            if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }

            string fmt = "0." + new string('0', decimalPlaces);

            if (value == 0) { return string.Format("{0:" + fmt + "} bytes", 0m); }

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

            return string.Format("{0:" + fmt + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }
    }
}
