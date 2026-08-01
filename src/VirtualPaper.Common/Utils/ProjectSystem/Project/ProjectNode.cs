namespace VirtualPaper.Common.Utils.ProjectSystem.Project {
    /// <summary>
    /// 项目节点，表示项目中的文件或文件夹
    /// </summary>
    public abstract class ProjectNode {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public ProjectFolder? Parent { get; set; }
    }

    /// <summary>
    /// 项目文件夹节点，表示项目中的文件夹
    /// </summary>
    public class ProjectFolder : ProjectNode {
        public List<ProjectNode> Children { get; } = [];
    }

    /// <summary>
    /// 项目文件节点，表示项目中的文件
    /// </summary>
    public class ProjectFile : ProjectNode {
        public DateTime LastWriteTime { get; set; }
    }
}
