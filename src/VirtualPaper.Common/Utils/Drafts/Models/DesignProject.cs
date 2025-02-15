namespace VirtualPaper.Common.Utils.Drafts.Models {
    public class DesignProject {
        public string Name { get; set; } = string.Empty;
        public ProjectType Type { get; set; }
        public string Path { get; set; } = string.Empty;
        public Guid Guid { get; set; }
    }
}
