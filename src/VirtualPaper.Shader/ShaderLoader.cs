using System.Collections.Concurrent;
using System.Diagnostics;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using Windows.Storage;

namespace VirtualPaper.Shader {
    public static class ShaderLoader {
        /// <summary>
        /// 默认 Shader 文件夹名（未指定路径时）
        /// </summary>
        private const string DefaultShaderFolderName = "Shaders";

        public static bool IsLoaded => _isLoaded;

        static ShaderLoader() {
            var context = FileShared.Read();
            _baseDir = context?.BaseDir;
        }

        /// <summary>
        /// 确保所有Shader已加载完成（阻塞直到完成）
        /// </summary>
        public static async Task LoadAllShadersAsync() {
            if (_isLoaded) return;

            await _loadingSemaphore.WaitAsync();
            try {
                if (_isLoaded) return;

                var allTypes = Enum.GetValues(typeof(ShaderType)).Cast<ShaderType>();
                var loadingTasks = new List<Task>();

                foreach (var type in allTypes) {
                    var loadTask = LoadShaderInternalAsync(type).ContinueWith(t => {
                        if (t.IsCompletedSuccessfully) {
                            _shaderCache[type] = t.Result;
                        }
                        else if (t.IsFaulted) {
                            // 记录错误但继续加载其他Shader
                            Debug.WriteLine($"Failed to load shader {type}: {t.Exception?.Message}");
                        }
                    });

                    loadingTasks.Add(loadTask);
                }

                // 等待所有加载任务完成
                await Task.WhenAll(loadingTasks);
                _isLoaded = true;
            }
            finally {
                _loadingSemaphore.Release();
            }
        }

        /// <summary>
        /// 获取已加载的Shader数据（同步方法）
        /// </summary>
        public static byte[] GetShader(ShaderType type) {
            if (!_isLoaded)
                throw new InvalidOperationException("Shaders are not loaded yet. Call LoadAllShadersAsync() first.");

            if (_shaderCache.TryGetValue(type, out var shaderData))
                return shaderData;

            throw new KeyNotFoundException($"Shader {type} not found in cache");
        }

        /// <summary>
        /// 重新加载指定 Shader
        /// </summary>
        public static async Task<byte[]> ReloadShaderAsync(ShaderType type) {
            var newData = await LoadShaderInternalAsync(type);
            _shaderCache[type] = newData;
            return newData;
        }

        /// <summary>
        /// 重新加载所有 Shader
        /// </summary>
        public static async Task ReloadAllShadersAsync() {
            _isLoaded = false;
            _shaderCache.Clear();
            await LoadAllShadersAsync();
        }

        private static async Task<byte[]> LoadShaderInternalAsync(ShaderType type) {
            bool isPackaged = Constants.ApplicationType.IsMSIX;

            // 决定 Shader 路径
            string shaderPath;
            if (isPackaged) {
                shaderPath = await ResolveShaderPathForPackagedAsync(type);
            }
            else {
                shaderPath = ResolveShaderPathForUnpackaged(type);
            }

            if (!File.Exists(shaderPath))
                throw new FileNotFoundException($"Shader file not found：{shaderPath}");

            return await File.ReadAllBytesAsync(shaderPath);
        }

        /// <summary>
        /// Packaged 模式下加载 shader
        /// </summary>
        private static async Task<string> ResolveShaderPathForPackagedAsync(ShaderType type) {
            string fileName = ShaderTypeManager.GetShaderName(type);
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
        private static string ResolveShaderPathForUnpackaged(ShaderType type) {
            string fileName = ShaderTypeManager.GetShaderName(type);
            if (_baseDir == null || fileName == string.Empty) return string.Empty;

            string filePath = Path.Combine(_baseDir, Constants.WorkingDir.Shader, fileName);

            return filePath;
        }

        /// <summary>
        /// 清空 shader 缓存
        /// </summary>
        public static void ClearCache() {
            _shaderCache.Clear();
            _isLoaded = false;
        }

        private static readonly ConcurrentDictionary<ShaderType, byte[]> _shaderCache = new();
        private static readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
        private static bool _isLoaded = false;
        private static readonly string? _baseDir;
    }
}