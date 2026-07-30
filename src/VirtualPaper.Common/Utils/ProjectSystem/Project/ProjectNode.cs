namespace VirtualPaper.Common.Utils.ProjectSystem.Project {
    public abstract class ProjectNode {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public ProjectFolder? Parent { get; set; }
    }

    public class ProjectFolder : ProjectNode {
        public List<ProjectNode> Children { get; } = [];
    }

    public class ProjectFile : ProjectNode {
        public DateTime LastWriteTime { get; set; }
    }
}
