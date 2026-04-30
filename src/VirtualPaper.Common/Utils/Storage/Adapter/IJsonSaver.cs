using System.Text.Json.Serialization;

namespace VirtualPaper.Common.Utils.Storage.Adapter {
    public interface IJsonSaver {
        T Load<T>(string path, JsonSerializerContext context);
        void Save<T>(string path, T value, JsonSerializerContext context);
    }
}
