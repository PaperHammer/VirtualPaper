using System.Collections.Concurrent;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Storage;
using Windows.Storage;

namespace VirtualPaper.Shader {
    public class ShaderLoader {
        /// <summary>
        /// 默认 Shader 文件夹名（未指定路径时）
        /// </summary>
        private const string DefaultShaderFolderName = "Shaders";

        public static bool IsLoaded => _isInited;

        static ShaderLoader() {
            var context = FileShared.Read();
            _baseDir = context?.BaseDir;
        }

        /// <summary>
        /// 确保所有Shader已加载完成（阻塞直到完成）
        /// </summary>
        public static async Task LoadAllShadersAsync() {
            if (_isInited) return;

            await _loadingSemaphore.WaitAsync();
            try {
                if (_isInited) return;

                var allTypes = Enum.GetValues(typeof(ShaderType)).Cast<ShaderType>();
                var loadingTasks = new List<Task>();

                foreach (var type in allTypes) {
                    if (type == ShaderType.None) continue;
                    var loadTask = LoadShaderInternalAsync(type).ContinueWith(t => {
                        if (t.IsCompletedSuccessfully) {
                            _shaderCache[type] = t.Result;
                        }
                        else if (t.IsFaulted) {
                            // 记录错误但继续加载其他Shader
                            ArcLog.GetLogger<ShaderLoader>().Error(t.Exception);
                        }
                    });

                    loadingTasks.Add(loadTask);
                }

                await Task.WhenAll(loadingTasks);
                _isInited = true;
            }
            finally {
                _loadingSemaphore.Release();
            }
        }

        /// <summary>
        /// 获取已加载的Shader数据（同步方法）
        /// </summary>
        public static byte[] GetShader(ShaderType type) {
            if (!_isInited)
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
            _isInited = false;
            _shaderCache.Clear();
            await LoadAllShadersAsync();
        }

        private static async Task<byte[]> LoadShaderInternalAsync(ShaderType type) {
            bool isPackaged = Constants.ApplicationType.IsMSIX;

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
            _isInited = false;
        }

        private static readonly ConcurrentDictionary<ShaderType, byte[]> _shaderCache = new();
        private static readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
        private static bool _isInited = false;
        private static readonly string? _baseDir;
    }
}