using VirtualPaper.Common;

namespace VirtualPaper.Models.DraftPanel {
    public class ProjectTemplate {
        public string ItemImageKey { get; set; } = string.Empty;
        public string DescImageKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public ProjectType Type { get; set; }
    }
}
