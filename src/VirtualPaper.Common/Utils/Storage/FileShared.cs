using System.Text;
using System.Text.Json;

namespace VirtualPaper.Common.Utils.Storage {
    public static class FileShared {
        private static readonly string SharedFilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "VirtualPaper", "shared_data.json");

        private const string MutexName = "Global\\VirtualPaper_FileShared";

        public static void Write(SharedContext context) {
            using var mutex = new Mutex(false, MutexName);
            try {
                mutex.WaitOne();

                string json = context.ToJson();
                Directory.CreateDirectory(Path.GetDirectoryName(SharedFilePath)!);

                // 原子写入：先写临时文件再替换
                string tempFile = Path.GetTempFileName();
                File.WriteAllText(tempFile, json, Encoding.UTF8);
                File.Move(tempFile, SharedFilePath, true);
            }
            catch {
                throw;
            }
            finally {
                try { mutex.ReleaseMutex(); } catch { /* ignore */ }
            }
        }

        public static SharedContext? Read() {
            using var mutex = new Mutex(false, MutexName);
            try {
                mutex.WaitOne();

                if (!File.Exists(SharedFilePath))
                    return null;

                using var stream = new FileStream(SharedFilePath,
                    FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string json = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                return SharedContext.FromJson(json);
            }
            catch {
                throw;
            }
            finally {
                try { mutex.ReleaseMutex(); } catch { /* ignore */ }
            }
        }

        // 清理共享文件
        public static void Cleanup() {
            using var mutex = new Mutex(false, MutexName);
            try {
                mutex.WaitOne();
                if (File.Exists(SharedFilePath))
                    File.Delete(SharedFilePath);
            }
            catch {
                throw;
            }
            finally {
                try { mutex.ReleaseMutex(); } catch { /* ignore */ }
            }
        }

        public static bool Exists() => File.Exists(SharedFilePath);

        public static FileInfo? GetFileInfo() {
            try {
                return File.Exists(SharedFilePath) ? new FileInfo(SharedFilePath) : null;
            }
            catch {
                return null;
            }
        }
    }

    public class SharedContext {
        public string BaseDir { get; set; } = string.Empty;

        public string ToJson() =>
            JsonSerializer.Serialize(this, _options);

        public static SharedContext? FromJson(string json) =>
            JsonSerializer.Deserialize<SharedContext>(json);

        private static readonly JsonSerializerOptions _options = new() {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };
    }
}
