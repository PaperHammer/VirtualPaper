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
        /// 确保所有 Shader 已加载完成
        /// </summary>
        public static async Task LoadAllShadersAsync() {
            if (_isInited) return;

            await _loadingSemaphore.WaitAsync();
            try {
                if (_isInited) return;

                await LoadShadersInternalAsync();
                _isInited = true;
            }
            finally {
                _loadingSemaphore.Release();
            }
        }

        /// <summary>
        /// 获取已加载的 Shader 数据（同步方法）
        /// </summary>
        public static byte[] GetShader(ShaderType type) {
            if (!_isInited)
                throw new InvalidOperationException("Shaders are not loaded yet. Call LoadAllShadersAsync() first.");

            if (_shaderCache.TryGetValue(type, out var shaderData))
                return shaderData;

            throw new KeyNotFoundException($"Shader {type} not found in cache.");
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
        /// 重新加载所有 Shader（持锁操作，加载完成前 GetShader 不受影响）
        /// </summary>
        public static async Task ReloadAllShadersAsync() {
            await _loadingSemaphore.WaitAsync();
            try {
                // 先加载到临时字典，完成后再原子替换，全程 _isInited 保持 true
                var newCache = new ConcurrentDictionary<ShaderType, byte[]>();

                var allTypes = Enum.GetValues(typeof(ShaderType))
                                   .Cast<ShaderType>()
                                   .Where(t => t != ShaderType.None);

                var loadingTasks = allTypes.Select(type =>
                    LoadShaderInternalAsync(type).ContinueWith(t => {
                        if (t.IsCompletedSuccessfully) {
                            newCache[type] = t.Result;
                        }
                        else if (t.IsFaulted) {
                            ArcLog.GetLogger<ShaderLoader>().Error(t.Exception);
                        }
                    }, TaskScheduler.Default)
                );

                await Task.WhenAll(loadingTasks);

                // 原子替换：批量写入，不清空旧缓存、不改动 _isInited
                foreach (var (k, v) in newCache)
                    _shaderCache[k] = v;

                _isInited = true; // 防御性赋值（首次调用路径兼容）
            }
            finally {
                _loadingSemaphore.Release();
            }
        }

        /// <summary>
        /// 清空 Shader 缓存
        /// </summary>
        public static void ClearCache() {
            _shaderCache.Clear();
            _isInited = false;
        }

        /// <summary>
        /// 加载所有 ShaderType（不含 None），结果写入缓存
        /// </summary>
        private static async Task LoadShadersInternalAsync() {
            var allTypes = Enum.GetValues(typeof(ShaderType))
                               .Cast<ShaderType>()
                               .Where(t => t != ShaderType.None);

            var loadingTasks = allTypes.Select(type =>
                LoadShaderInternalAsync(type).ContinueWith(t => {
                    if (t.IsCompletedSuccessfully) {
                        _shaderCache[type] = t.Result;
                    }
                    else if (t.IsFaulted) {
                        ArcLog.GetLogger<ShaderLoader>().Error(t.Exception);
                    }
                }, TaskScheduler.Default)
            );

            await Task.WhenAll(loadingTasks);
        }

        private static async Task<byte[]> LoadShaderInternalAsync(ShaderType type) {
            string shaderPath = Constants.ApplicationType.IsMSIX
                ? await ResolveShaderPathForPackagedAsync(type)
                : ResolveShaderPathForUnpackaged(type);

            if (!File.Exists(shaderPath))
                throw new FileNotFoundException($"Shader file not found: {shaderPath}", shaderPath);

            return await File.ReadAllBytesAsync(shaderPath);
        }

        /// <summary>
        /// Packaged 模式：优先从 ms-appx 包内加载，失败则退回 LocalFolder
        /// </summary>
        private static async Task<string> ResolveShaderPathForPackagedAsync(ShaderType type) {
            string fileName = ShaderTypeManager.GetShaderName(type);

            try {
                var uri = new Uri($"ms-appx:///{DefaultShaderFolderName}/{fileName}");
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                return file.Path;
            }
            catch (FileNotFoundException) {
                // 包内资源不存在，退回 LocalFolder
            }
            catch (Exception ex) {
                ArcLog.GetLogger<ShaderLoader>().Warn(
                    $"Failed to resolve packaged shader path for {type}: {ex.Message}");
            }

            // Fallback：LocalFolder
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string folderPath = Path.Combine(localFolder.Path, DefaultShaderFolderName);
            Directory.CreateDirectory(folderPath);
            return Path.Combine(folderPath, fileName);
        }

        /// <summary>
        /// Unpackaged 模式：从配置的 BaseDir 加载
        /// </summary>
        private static string ResolveShaderPathForUnpackaged(ShaderType type) {
            if (_baseDir == null)
                throw new InvalidOperationException(
                    "BaseDir is not configured. Check FileShared.Read().");

            string fileName = ShaderTypeManager.GetShaderName(type);
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException(
                    $"No shader file name mapped for ShaderType: {type}", nameof(type));

            return Path.Combine(_baseDir, Constants.WorkingDir.Shader, fileName);
        }

        private static readonly ConcurrentDictionary<ShaderType, byte[]> _shaderCache = new();
        private static readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
        private static volatile bool _isInited = false;
        private static readonly string? _baseDir;
    }
}
