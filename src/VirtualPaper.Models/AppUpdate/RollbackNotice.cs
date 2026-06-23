using System.Text.Json.Serialization;

namespace VirtualPaper.Models.AppUpdate {
    [JsonSerializable(typeof(RollbackNotice))]
    public partial class RollbackNoticeContext : JsonSerializerContext { }

    public class RollbackNotice {
        [JsonPropertyName("rollback")]
        public bool Rollback { get; set; }

        [JsonPropertyName("message_key")]
        public string MessageKey { get; set; } = string.Empty;
    }
}
