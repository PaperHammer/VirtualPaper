namespace VirtualPaper.Common {
    public class Errors {
        public class WorkerWException : Exception {
            public WorkerWException() {
            }

            public WorkerWException(string message)
                : base(message) {
            }

            public WorkerWException(string message, Exception inner)
                : base(message, inner) {
            }
        }

        public class WallpaperNotFoundException : Exception {
            public WallpaperNotFoundException() {
            }

            public WallpaperNotFoundException(string message)
                : base(message) {
            }

            public WallpaperNotFoundException(string message, Exception inner)
                : base(message, inner) {
            }
        }

        public class WallpaperPluginException : Exception {
            public WallpaperPluginException() {
            }

            public WallpaperPluginException(string message)
                : base(message) {
            }

            public WallpaperPluginException(string message, Exception inner)
                : base(message, inner) {
            }
        }

        public class WallpaperPluginNotFoundException : Exception {
            public WallpaperPluginNotFoundException() {
            }

            public WallpaperPluginNotFoundException(string message)
                : base(message) {
            }

            public WallpaperPluginNotFoundException(string message, Exception inner)
                : base(message, inner) {
            }
        }

        /// <summary>
        /// Windows N/KN codec missing.
        /// </summary>
        public class WallpaperPluginMediaCodecException : Exception {
            public WallpaperPluginMediaCodecException() {
            }

            public WallpaperPluginMediaCodecException(string message)
                : base(message) {
            }

            public WallpaperPluginMediaCodecException(string message, Exception inner)
                : base(message, inner) {
            }
        }

        public class WallpaperNotAllowedException : Exception {
            public WallpaperNotAllowedException() {
            }

            public WallpaperNotAllowedException(string message)
                : base(message) {
            }

            public WallpaperNotAllowedException(string message, Exception inner)
                : base(message, inner) {
            }
        }

        public class ScreenNotFoundException : Exception {
            public ScreenNotFoundException() {
            }

            public ScreenNotFoundException(string message)
                : base(message) {
            }

            public ScreenNotFoundException(string message, Exception inner)
                : base(message, inner) {
            }
        }

        public class FileAccessException : Exception {
            public string FilePath { get; } = string.Empty;
            public string Operation { get; } = string.Empty;

            public FileAccessException() {
            }

            public FileAccessException(string filePath, string operation)
                : base(GenerateMessage(filePath, operation)) {
                FilePath = filePath;
                Operation = operation;
            }
            
            public FileAccessException(string filePath, string operation, Exception inner)
                : base(GenerateMessage(filePath, operation), inner) {
                FilePath = filePath;
                Operation = operation;
            }

            private static string GenerateMessage(string filePath, string operation) {
                return $"对文件 \"{filePath}\" 的 {operation} 操作发生错误，请检查文件完整性或应用权限。";
            }
        }
    }
}
