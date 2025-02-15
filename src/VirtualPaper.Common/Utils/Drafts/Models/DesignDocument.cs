namespace VirtualPaper.Common.Utils.Drafts.Models {
    public class DesignDocument {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public List<DesignProject> Projects { get; set; } = [];
    }
}
