using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace VirtualPaper.Common.Utils.Storage {
    public static class SharedStorage {
        private const string MapName = "Local\\VirtualPaper_SharedMemory";
        private const int BufferSize = 8192;

        // 导入 Win32 API
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFileMapping(
            IntPtr hFile, IntPtr lpAttributes, uint flProtect,
            uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(
            IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh,
            uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PAGE_READWRITE = 0x04;
        private const uint FILE_MAP_ALL_ACCESS = 0xF001F;

        // 写入 SharedContext 到共享内存
        public static void Write(SharedContext context) {
            string json = context.ToJson();
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            if (bytes.Length >= BufferSize)
                throw new InvalidOperationException($"SharedContext data is too large ({bytes.Length} bytes), exceeding the {BufferSize} limit.");

            IntPtr hMapFile = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, PAGE_READWRITE, 0, BufferSize, MapName);
            if (hMapFile == IntPtr.Zero)
                throw new InvalidOperationException($"CreateFileMapping failed: {Marshal.GetLastWin32Error()}");

            IntPtr pBuf = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, (UIntPtr)BufferSize);
            if (pBuf == IntPtr.Zero)
                throw new InvalidOperationException($"MapViewOfFile failed: {Marshal.GetLastWin32Error()}");

            try {
                Marshal.Copy(bytes, 0, pBuf, bytes.Length);
                Marshal.WriteByte(pBuf + bytes.Length, 0); // 结尾补零
            }
            finally {
                UnmapViewOfFile(pBuf);
                CloseHandle(hMapFile);
            }
        }

        // 从共享内存读取 SharedContext
        public static SharedContext? Read() {
            IntPtr hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, false, MapName);
            if (hMapFile == IntPtr.Zero)
                return null;

            IntPtr pBuf = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, (UIntPtr)BufferSize);
            if (pBuf == IntPtr.Zero)
                return null;

            try {
                byte[] buffer = new byte[BufferSize];
                Marshal.Copy(pBuf, buffer, 0, buffer.Length);
                string json = Encoding.UTF8.GetString(buffer).TrimEnd('\0');

                if (string.IsNullOrWhiteSpace(json))
                    return null;

                return SharedContext.FromJson(json);
            }
            finally {
                UnmapViewOfFile(pBuf);
                CloseHandle(hMapFile);
            }
        }
    }

    public class SharedContext {
        public string BaseDir { get; set; } = string.Empty;
        public string PluginDir { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;

        public string ToJson() =>
            JsonSerializer.Serialize(this, _options);

        public static SharedContext? FromJson(string json) {
            try {
                return JsonSerializer.Deserialize<SharedContext>(json);
            }
            catch {
                return null;
            }
        }

        private static readonly JsonSerializerOptions _options = new() { WriteIndented = false };
    }
}
