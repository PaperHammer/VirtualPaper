using System.IO;
using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.IntelligentPanel.Models {
    public class StyleOptionItem {
        public string? Name { get; set; }
        public string? ThumbnailResourceKey { get; set; }
        public string? ImagePath { get; set; }
        public string? FileSize { get; internal set; }
        public string? FileExt { get; internal set; }
        public bool IsCustom { get; internal set; }
    }
}
