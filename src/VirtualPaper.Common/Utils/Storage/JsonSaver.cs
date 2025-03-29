using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using static VirtualPaper.Common.Errors;

namespace VirtualPaper.Common.Utils.Storage {
    public static class JsonSaver {
        static JsonSaver() {
            _optionsStore.Converters.Add(new IntPtrJsonConverter());
            _optionsLoad.Converters.Add(new IntPtrJsonConverter());
        }

        #region system.text.json
        public static T Load<T>(string filePath, JsonSerializerContext context) {
            return LoadAsync<T>(filePath, context).Result;
        }

        public static void Store<T>(string filePath, T data, JsonSerializerContext context) {
            SaveAsync(filePath, data, context).Wait();
        }

        public static async Task<T> LoadAsync<T>(string filePath, JsonSerializerContext context) {
            try {
                JsonSerializerOptions combinedLoadOptions = new(_optionsLoad) { TypeInfoResolver = JsonTypeInfoResolver.Combine(context) };
                using FileStream stream = File.OpenRead(filePath);
                return await JsonSerializer.DeserializeAsync<T>(stream, combinedLoadOptions);
            }
            catch (Exception ex) {
                throw new FileAccessException(filePath, "读取", ex);
            }
        }

        public static async Task SaveAsync<T>(string filePath, T data, JsonSerializerContext context) {
            try {
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath)) {
                    Directory.CreateDirectory(directoryPath);
                }

                JsonSerializerOptions combinedStoreOptions = new(_optionsStore) { TypeInfoResolver = JsonTypeInfoResolver.Combine(context) };
                using FileStream stream = File.Create(filePath);
                await JsonSerializer.SerializeAsync(stream, data, combinedStoreOptions);
            }
            catch (Exception ex) {
                throw new FileAccessException(filePath, "写入", ex);
            }
        }

        private static readonly JsonSerializerOptions _optionsLoad = new() {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };

        // ref: https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonignorecondition?view=net-8.0
        private static readonly JsonSerializerOptions _optionsStore = new() {
            WriteIndented = true,
            // 允许写入空值
            // Property is always serialized and deserialized, regardless of IgnoreNullValues configuration.
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        #endregion
    }
}
