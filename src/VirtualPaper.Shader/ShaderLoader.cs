using System.Collections.Concurrent;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using Windows.Storage;

namespace VirtualPaper.Shader {
    public static class ShaderUtil {
        /// <summary>
        /// 默认 Shader 文件夹名（未指定路径时）
        /// </summary>
        private const string DefaultShaderFolderName = "Shaders";

        static ShaderUtil() {
            var context = SharedStorage.Read();
            _pluginDir = context?.PluginDir;
        }

        /// <summary>
        /// 从指定文件夹与文件名加载 shader
        /// </summary>
        /// <param name="fileName">Shader 文件名</param>
        /// <param name="customFolderPath">自定义 Shader 文件夹路径</param>
        /// <returns>Shader 文件的二进制数据</returns>
        public static async Task<byte[]> LoadShaderAsync(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Shader file name cannot be empty", nameof(fileName));

            if (_shaderCache.TryGetValue(fileName, out var cached))
                return cached;

            bool isPackaged = Constants.ApplicationType.IsMSIX;

            // 决定 Shader 路径
            string shaderPath;
            if (isPackaged) {
                shaderPath = await ResolveShaderPathForPackagedAsync(fileName);
            }
            else {
                shaderPath = await ResolveShaderPathForUnpackagedAsync(fileName);
            }

            if (!File.Exists(shaderPath))
                throw new FileNotFoundException($"Shader file not found：{shaderPath}");

            var bytes = await File.ReadAllBytesAsync(shaderPath);
            _shaderCache[fileName] = bytes;

            return bytes;
        }

        /// <summary>
        /// Packaged 模式下加载 shader
        /// </summary>
        private static async Task<string> ResolveShaderPathForPackagedAsync(string fileName) {
            try {
                var uri = new Uri($"ms-appx:///{DefaultShaderFolderName}/{fileName}");
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);

                return file.Path;
            }
            catch {
                // 退回 LocalFolder
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                string folderPath = Path.Combine(localFolder.Path, DefaultShaderFolderName);
                Directory.CreateDirectory(folderPath);

                return Path.Combine(folderPath, fileName);
            }
        }

        /// <summary>
        /// Unpackaged 模式下加载 shader
        /// </summary>
        private static async Task<string?> ResolveShaderPathForUnpackagedAsync(string fileName) {
            if (_pluginDir == null) return null;

            string stringsFolderPath = Path.Combine(_pluginDir, Constants.WorkingDir.Shader);
            StorageFolder shaderFolder = await StorageFolder.GetFolderFromPathAsync(stringsFolderPath);

            return Path.Combine(shaderFolder.Path, fileName);
        }

        /// <summary>
        /// 清空 shader 缓存
        /// </summary>
        public static void ClearCache() => _shaderCache.Clear();

        private static readonly ConcurrentDictionary<string, byte[]> _shaderCache = new();
        private static readonly string? _pluginDir;
    }
}
