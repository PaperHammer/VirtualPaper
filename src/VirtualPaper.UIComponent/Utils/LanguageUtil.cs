using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using VirtualPaper.Common;
using Windows.Storage;
using WinUI3Localizer;

namespace VirtualPaper.UIComponent.Utils {
    public class LanguageUtil {
        public static ILocalizer LocalizerInstance { get; private set; }

        static LanguageUtil() {
            SetInstance();
        }

        #region load language       
        public static async void LanguageChanged(string lang) {
            await Localizer.Get().SetLanguage(lang);
            SetInstance();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetI18n(string key) {
            // 尝试从热缓存获取
            if (_hotCache.TryGetValue(key, out var hotValue)) {
                RecordAccess(key);
                return hotValue;
            }

            // 尝试从冷缓存恢复
            if (_coldCache.TryGetValue(key, out var weakRef) &&
                weakRef.TryGetTarget(out var coldValue)) {
                PromoteToHotCache(key, coldValue);
                return coldValue;
            }

            // 完全未命中，加载并缓存
            var value = LoadFromSource(key);
            _coldCache[key] = new WeakReference<string>(value);
            return value;
        }

        private static void RecordAccess(string key) {
            _accessCounts.AddOrUpdate(key, 1, (_, count) => count + 1);
        }

        private static void PromoteToHotCache(string key, string value) {
            // 如果热缓存已满，淘汰最少使用的项
            if (_hotCache.Count >= HOT_CACHE_MAX_SIZE) {
                var leastUsedKey = FindLeastUsedKey();
                if (leastUsedKey != null) {
                    _hotCache.TryRemove(leastUsedKey, out _);
                    _coldCache[leastUsedKey] = new WeakReference<string>(value);
                }
            }

            _hotCache[key] = value;
            RecordAccess(key);
        }

        private static string? FindLeastUsedKey() {
            long minCount = long.MaxValue;
            string? result = null;

            foreach (var kvp in _accessCounts) {
                if (kvp.Value < minCount && _hotCache.ContainsKey(kvp.Key)) {
                    minCount = kvp.Value;
                    result = kvp.Key;
                }
            }

            return result;
        }

        private static string LoadFromSource(string key) {
            return LocalizerInstance.GetLocalizedString(key); // 实际加载逻辑
        }

        // ref: https://github.com/AndrewKeepCoding/WinUI3Localizer
        public static async Task InitializeLocalizerForUnpackaged(string lang) {
            // Initialize a "Strings" folder in the executables folder.
            string stringsFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.ModuleName.UIComponent, "Strings");
            StorageFolder stringsFolder = await StorageFolder.GetFolderFromPathAsync(stringsFolderPath);

            ILocalizer localizer = await new LocalizerBuilder()
                .AddStringResourcesFolderForLanguageDictionaries(stringsFolderPath)
                .SetOptions(options => {
                    options.DefaultLanguage = lang;
                })
                .Build();
            SetInstance();
        }

        public static async Task InitializeLocalizerForPackaged(string lang) {
            // Initialize a "Strings" folder in the "LocalFolder" for the packaged app.
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFolder stringsFolder = await localFolder.CreateFolderAsync(
              "Strings",
               CreationCollisionOption.OpenIfExists);

            // Create string resources file from app resources if doesn'T exists.
            string resourceFileName = "Resources.resw";
            await CreateStringResourceFileIfNotExists(stringsFolder, "zh-CN", resourceFileName);
            await CreateStringResourceFileIfNotExists(stringsFolder, "en-US", resourceFileName);

            ILocalizer localizer = await new LocalizerBuilder()
                .AddStringResourcesFolderForLanguageDictionaries(stringsFolder.Path)
                .SetOptions(options => {
                    options.DefaultLanguage = lang;
                })
                .Build();
            SetInstance();
        }

        private static async Task CreateStringResourceFileIfNotExists(StorageFolder stringsFolder, string language, string resourceFileName) {
            StorageFolder languageFolder = await stringsFolder.CreateFolderAsync(
                language,
                CreationCollisionOption.OpenIfExists);

            if (await languageFolder.TryGetItemAsync(resourceFileName) is null) {
                string resourceFilePath = Path.Combine(stringsFolder.Name, language, resourceFileName);
                StorageFile resourceFile = await LoadStringResourcesFileFromAppResource(resourceFilePath);
                _ = await resourceFile.CopyAsync(languageFolder);
            }
        }

        private static async Task<StorageFile> LoadStringResourcesFileFromAppResource(string filePath) {
            Uri resourcesFileUri = new($"ms-appx:///{filePath}");
            return await StorageFile.GetFileFromApplicationUriAsync(resourcesFileUri);
        }

        private static void SetInstance() {
            LocalizerInstance = Localizer.Get();
        }
        #endregion

        // 第一层：热缓存（强引用，固定大小）
        private static readonly ConcurrentDictionary<string, string> _hotCache = new();
        private const int HOT_CACHE_MAX_SIZE = 200;

        // 第二层：冷缓存（弱引用，由GC自动回收）
        private static readonly ConcurrentDictionary<string, WeakReference<string>> _coldCache = new();

        // 访问频率记录（用于热缓存淘汰）
        private static readonly ConcurrentDictionary<string, long> _accessCounts = new();
    }  
}
