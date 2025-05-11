using System.Text.Json.Serialization;
using VirtualPaper.Common;

namespace VirtualPaper.Models.Cores.Interfaces {
    public interface IRecentUsed {
        FileType Type { get; }
        [JsonIgnore]
        string Glyph { get; }
        string FileName { get; }
        string FilePath { get; }
        string DateTime { get; set; }
    }
}
