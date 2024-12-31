using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace VirtualPaper.Common.Utils.Storage {
    public static class JsonStorage<T> {
        static JsonStorage() {
            _optionsStore.Converters.Add(new IntPtrJsonConverter());
            _optionsLoad.Converters.Add(new IntPtrJsonConverter());
        }

        #region system.text.json
        public static T LoadData(string filePath, JsonSerializerContext context) {
            try {
                var combinedLoadOptions = new JsonSerializerOptions(_optionsLoad) { TypeInfoResolver = JsonTypeInfoResolver.Combine(context) };
                using FileStream stream = File.OpenRead(filePath);
                return JsonSerializer.Deserialize<T>(stream, combinedLoadOptions);
            }
            catch (JsonException ex) {
                throw new ArgumentException("json null/corrupt", ex);
            }
        }

        public static void StoreData(string filePath, T data, JsonSerializerContext context) {
            try {
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath)) {
                    Directory.CreateDirectory(directoryPath);
                }

                var combinedStoreOptions = new JsonSerializerOptions(_optionsStore) { TypeInfoResolver = JsonTypeInfoResolver.Combine(context) };
                using FileStream stream = File.Create(filePath);
                JsonSerializer.Serialize(stream, data, combinedStoreOptions);
            }
            catch (JsonException ex) {
                throw new ArgumentException("json store null/corrupt", ex);
            }
        }

        public static async Task<T> LoadDataAsync(string filePath, JsonSerializerContext context) {
            try {
                var combinedLoadOptions = new JsonSerializerOptions(_optionsLoad) { TypeInfoResolver = JsonTypeInfoResolver.Combine(context) };
                using FileStream stream = File.OpenRead(filePath);
                return await JsonSerializer.DeserializeAsync<T>(stream, combinedLoadOptions);
            }
            catch (JsonException ex) {
                throw new ArgumentException("json load null/corrupt", ex);
            }
        }

        public static async Task StoreDataAsync(string filePath, T data, JsonSerializerContext context) {
            try {
                var combinedStoreOptions = new JsonSerializerOptions(_optionsStore) { TypeInfoResolver = JsonTypeInfoResolver.Combine(context) }; using FileStream stream = File.Create(filePath);
                await JsonSerializer.SerializeAsync(stream, data, combinedStoreOptions);
            }
            catch (JsonException ex) {
                throw new ArgumentException("json store null/corrupt", ex);
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
