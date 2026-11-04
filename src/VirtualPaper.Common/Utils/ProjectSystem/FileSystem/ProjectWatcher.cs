using VirtualPaper.Common.Logging;

namespace VirtualPaper.Common.Utils.ProjectSystem.FileSystem {
    /// <summary>
    /// 项目文件系统监视器，负责监视项目根目录下的文件和文件夹的创建、删除、修改和重命名事件
    /// </summary>
    public sealed class ProjectWatcher : IDisposable {
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
                Dispatch(() => Created?.Invoke(e.FullPath));
            };

            _watcher.Deleted += (_, e) => {
                Dispatch(() => Deleted?.Invoke(e.FullPath));
            };

            _watcher.Changed += (_, e) => {
                Dispatch(() => Modified?.Invoke(e.FullPath));
            };

            _watcher.Renamed += (_, e) => {
                Dispatch(() => Renamed?.Invoke(e.OldFullPath, e.FullPath));
            };
        }

        public void Start() {
            lock (_gate) {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _running = true;
                _watcher.EnableRaisingEvents = true;
            }
        }

        public void Stop() {
            lock (_gate) {
                if (_disposed) return;
                _running = false;
                _watcher.EnableRaisingEvents = false;
            }
        }

        private void Dispatch(Action callback) {
            // Stop/Dispose must wait for a running callback, and queued callbacks must not
            // touch a project after its owner has closed it or removed its directory.
            lock (_gate) {
                if (!_running || _disposed) return;
                try { callback(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                    ArcLog.GetLogger<ProjectWatcher>().Error(ex);
                }
            }
        }

        public void Dispose() {
            lock (_gate) {
                if (_disposed) return;
                _running = false;
                _disposed = true;
                _watcher.Dispose();
                Created = null;
                Deleted = null;
                Modified = null;
                Renamed = null;
            }
        }

        private readonly object _gate = new();
        private bool _running, _disposed;
        private readonly FileSystemWatcher _watcher;
    }
}
