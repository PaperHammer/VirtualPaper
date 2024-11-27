//using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VirtualPaper.Common.Utils.Storage {
    public static class JsonStorage<T> {
        static JsonStorage() {
            _optionsStore.Converters.Add(new IntPtrJsonConverter());
            _optionsLoad.Converters.Add(new IntPtrJsonConverter());
        }

        #region system.text.json
        public static T LoadData(string filePath) {
            using FileStream stream = File.OpenRead(filePath);

            try {
                return JsonSerializer.Deserialize<T>(stream, _optionsLoad);
            }
            catch (JsonException ex) {
                throw new ArgumentException("json null/corrupt", ex);
            }
        }

        public static void StoreData(string filePath, T data) {
            using FileStream stream = File.Create(filePath);
            JsonSerializer.Serialize(stream, data, _optionsStore);
        }

        public static async Task<T> LoadDataAsync(string filePath) {
            using FileStream stream = File.OpenRead(filePath);

            try {
                return await JsonSerializer.DeserializeAsync<T>(stream, _optionsLoad);
            }
            catch (JsonException ex) {
                throw new ArgumentException("json null/corrupt", ex);
            }
        }

        public static async Task StoreDataAsync(string filePath, T data) {
            using FileStream stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, data, _optionsStore);
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

        //public static T LoadData(string filePath)
        //{
        //    using StreamReader file = File.OpenText(filePath);
        //    var serializer = new JsonSerializer()
        //    {
        //        TypeNameHandling = TypeNameHandling.All,
        //    };
        //    var tmp = (T)serializer.Deserialize(file, typeof(T));

        //    return tmp == null ? throw new ArgumentNullException($"json null/corrupt: {filePath}") : tmp;
        //}

        //public static void StoreData(string filePath, T data)
        //{
        //    if (!File.Exists(filePath))
        //    {
        //        File.Create(filePath).Close();
        //    }

        //    JsonSerializer serializer = new()
        //    {
        //        Formatting = Formatting.Indented,
        //        NullValueHandling = NullValueHandling.Include,
        //    };

        //    using StreamWriter sw = new(filePath);
        //    using JsonWriter writer = new JsonTextWriter(sw);
        //    serializer.Serialize(writer, data, typeof(T));
        //}
    }
}
