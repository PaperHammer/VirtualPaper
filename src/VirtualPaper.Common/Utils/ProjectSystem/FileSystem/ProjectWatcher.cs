namespace VirtualPaper.Common.Utils.ProjectSystem.FileSystem {
    public class ProjectWatcher {
        public event Action<string>? Created;
        public event Action<string>? Deleted;
        public event Action<string>? Modified;
        public event Action<string, string>? Renamed;

        public ProjectWatcher(string root) {
            _watcher = new FileSystemWatcher(root) {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            _watcher.Created += (_, e) => {
                Created?.Invoke(e.FullPath);
            };

            _watcher.Deleted += (_, e) => {
                Deleted?.Invoke(e.FullPath);
            };

            _watcher.Changed += (_, e) => {
                Modified?.Invoke(e.FullPath);
            };

            _watcher.Renamed += (_, e) => {
                Renamed?.Invoke(e.OldFullPath, e.FullPath);
            };
        }

        public void Start() {
            _watcher.EnableRaisingEvents = true;
        }

        public void Stop() {
            _watcher.EnableRaisingEvents = false;
        }
        
        private readonly FileSystemWatcher _watcher;
    }
}
