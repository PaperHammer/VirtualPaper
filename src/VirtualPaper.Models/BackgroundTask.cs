namespace VirtualPaper.Models {
    public class BackgroundTask {
        public string TaskInfo { get; set; } = string.Empty;
        public bool Cancelable { get; set; }
        public Action? Cancel { get; set; }
        public Action<BackgroundTask>? Completed { get; set; }
    }
}
