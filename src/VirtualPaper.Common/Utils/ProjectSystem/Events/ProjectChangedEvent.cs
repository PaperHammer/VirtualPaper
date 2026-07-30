namespace VirtualPaper.Common.Utils.ProjectSystem.Events {
    public class ProjectChangedEvent {
        public ProjectChangeType Type { get; set; }
        public string Path { get; set; } = string.Empty;
        public string? OldPath { get; set; }
    }

    public enum ProjectChangeType {
        Created,
        Deleted,
        Renamed,
        Modified,
        Reloaded,
        Conflict
    }
}
