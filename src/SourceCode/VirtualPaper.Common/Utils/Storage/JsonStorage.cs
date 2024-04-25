using Newtonsoft.Json;

namespace VirtualPaper.Common.Utils.Storage
{
    public static class JsonStorage<T>
    {
        //public static T LoadData(string filePath)
        //{
        //    using FileStream stream = File.OpenRead(filePath);

        //    try
        //    {
        //        return JsonSerializer.Deserialize<T>(stream, _optionsLoad);
        //    }
        //    catch (JsonException ex)
        //    {
        //        throw new ArgumentException("json null/corrupt", ex);
        //    }
        //}

        //public static void StoreData(string filePath, T data)
        //{
        //    using FileStream stream = File.Create(filePath);
        //    JsonSerializer.Serialize(stream, data, _optionsStore);
        //}

        //private static readonly JsonSerializerOptions _optionsLoad = new()
        //{
        //    AllowTrailingCommas = true,
        //    PropertyNameCaseInsensitive = true
        //};
        //private static readonly JsonSerializerOptions _optionsStore = new()
        //{
        //    WriteIndented = true,
        //    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        //};

        public static T LoadData(string filePath)
        {
            using StreamReader file = File.OpenText(filePath);
            var serializer = new JsonSerializer()
            {
                TypeNameHandling = TypeNameHandling.All,
            };
            var tmp = (T)serializer.Deserialize(file, typeof(T));

            return tmp == null ? throw new ArgumentNullException("json null/corrupt") : tmp;
        }

        public static void StoreData(string filePath, T data)
        {
            JsonSerializer serializer = new()
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
            };

            using StreamWriter sw = new(filePath);
            using JsonWriter writer = new JsonTextWriter(sw);
            serializer.Serialize(writer, data, typeof(T));
        }
    }
}
