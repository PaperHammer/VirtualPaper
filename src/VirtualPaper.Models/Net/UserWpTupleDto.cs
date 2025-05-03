using System.Text.Json.Serialization;

namespace VirtualPaper.Models.Net {
    [JsonSerializable(typeof(UserWpTupleDto))]
    public partial class UserWpTupleDtoContext : JsonSerializerContext { }

    [method: JsonConstructor]
    public class UserWpTupleDto(long uid, string wallpaperId) {
        [JsonInclude]
        public long Uid { get; } = uid;
        [JsonInclude]
        public string WallpaperId { get; } = wallpaperId;
    }
}
