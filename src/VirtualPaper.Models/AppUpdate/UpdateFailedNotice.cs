using System.Text.Json.Serialization;

namespace VirtualPaper.Models.AppUpdate {
    [JsonSerializable(typeof(UpdateFailedNotice))]
    public partial class UpdateFailedNoticeContext : JsonSerializerContext { }

    public class UpdateFailedNotice {
        [JsonPropertyName("message_key")]
        public string MessageKey { get; set; } = string.Empty;

        [JsonPropertyName("exception_message")]
        public string ExceptionMessage { get; set; } = string.Empty;
    }
}
