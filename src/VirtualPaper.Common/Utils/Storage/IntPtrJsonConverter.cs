using System.Text.Json;
using System.Text.Json.Serialization;

namespace VirtualPaper.Common.Utils.Storage {
    public class IntPtrJsonConverter : JsonConverter<nint> {
        public override nint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            if (reader.TokenType == JsonTokenType.Number) {
                if (reader.TryGetInt64(out long value)) {
                    return new nint(value);
                }
            }

            return default;
        }

        public override void Write(Utf8JsonWriter writer, nint value, JsonSerializerOptions options) {
            writer.WriteNumberValue(value.ToInt64());
        }
    }
}
