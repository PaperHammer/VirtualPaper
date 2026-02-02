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

        public static T Load<T>(string filePath, JsonSerializerContext context) {
            return LoadAsync<T>(filePath, context).Result;
        }

        public static void Save<T>(string filePath, T data, JsonSerializerContext context) {
            SaveAsync(filePath, data, context).Wait();
        }

        public static async Task<T> LoadAsync<T>(string filePath, JsonSerializerContext context, params JsonConverter[]? converters) {
            try {
                JsonSerializerOptions combinedLoadOptions = new(_optionsLoad) { TypeInfoResolver = JsonTypeInfoResolver.Combine(context) };

                if (converters != null) {
                    foreach (var converter in converters) {
                        combinedLoadOptions.Converters.Add(converter);
                    }
                }

                using FileStream stream = File.OpenRead(filePath);
                return await JsonSerializer.DeserializeAsync<T>(stream, combinedLoadOptions);
            }
            catch (Exception ex) {
                throw new FileAccessException(filePath, "read json", ex);
            }
        }

        public static async Task SaveAsync<T>(string filePath, T data, JsonSerializerContext context, params JsonConverter[]? converters) {
            try {
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath)) {
                    Directory.CreateDirectory(directoryPath);
                }

                JsonSerializerOptions combinedStoreOptions = new(_optionsStore) { TypeInfoResolver = JsonTypeInfoResolver.Combine(context) };

                if (converters != null) {
                    foreach (var converter in converters) {
                        combinedStoreOptions.Converters.Add(converter);
                    }
                }

                using FileStream stream = File.Create(filePath);
                await JsonSerializer.SerializeAsync(stream, data, combinedStoreOptions);
            }
            catch (Exception ex) {
                throw new FileAccessException(filePath, "read json", ex);
            }
        }

        private static readonly JsonSerializerOptions _optionsLoad = new() {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip, // 允许 JSON 文件里写注释
            Converters = {
                new JsonStringEnumConverter() // 允许 Enum 读写为字符串
            }
        };

        // ref: https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonignorecondition?view=net-8.0
        private static readonly JsonSerializerOptions _optionsStore = new() {
            WriteIndented = true,
            // 允许写入空值
            // Property is always serialized and deserialized, regardless of IgnoreNullValues configuration.
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            ReadCommentHandling = JsonCommentHandling.Skip, // 允许 JSON 文件里写注释
            Converters = {
                new JsonStringEnumConverter() // 允许 Enum 读写为字符串
            }
        };
    }
}
