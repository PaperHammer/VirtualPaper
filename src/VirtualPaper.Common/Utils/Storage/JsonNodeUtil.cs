using System.Text.Json;
using System.Text.Json.Nodes;

namespace VirtualPaper.Common.Utils.Storage {
    public class JsonNodeUtil {
        #region system.text.json
        public static void Write(string filePath, JsonNode jsonNode) {
            using FileStream fs = new(filePath, FileMode.Create, FileAccess.Write);
            using Utf8JsonWriter writer = new(fs);
            jsonNode.WriteTo(writer);
        }
        
        public static void Write(string filePath, JsonElement jsonElement) {
            using FileStream fs = new(filePath, FileMode.Create, FileAccess.Write);
            using Utf8JsonWriter writer = new(fs);
            jsonElement.WriteTo(writer);
        }

        public static JsonNode GetWritableJson(string filePath) {
            var json = File.ReadAllText(filePath);

            return JsonNode.Parse(json)!;
        }

        public static JsonElement GetReadonlyJson(string filePath) {
            var json = File.ReadAllText(filePath);

            return JsonDocument.Parse(json).RootElement;
        }
        #endregion
    }
}
