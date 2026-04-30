using System.Text.Json.Serialization;

namespace VirtualPaper.Common.Utils.Storage.Adapter {
    public class JsonSaverWrapper : IJsonSaver {
        public T Load<T>(string path, JsonSerializerContext context) =>
            JsonSaver.Load<T>(path, context);

        public void Save<T>(string path, T value, JsonSerializerContext context) =>
            JsonSaver.Save(path, value, context);
    }
}
