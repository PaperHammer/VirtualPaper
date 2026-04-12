using VirtualPaper.Common;

namespace VirtualPaper.Models.DraftPanel {
    public class ProjectTemplate {
        public string? ItemImageKey { get; set; }
        public string? DescImageKey { get; set; }
        public string? Name { get; set; }
        public string? Desc { get; set; }
        public ProjectType Type { get; set; }
    }
}
