using VirtualPaper.Common.Utils.ProjectSystem.Documents;
using VirtualPaper.Common.Utils.ProjectSystem.Events;
using VirtualPaper.Common.Utils.ProjectSystem.FileSystem;
using VirtualPaper.Common.Utils.ProjectSystem.Project;

namespace VirtualPaper.Common.Utils.ProjectSystem {
    /// <summary>
    /// 项目系统管理器，负责管理项目树、文档和文件系统监视器，并处理项目变化事件
    /// </summary>
    public class ProjectSystemManager {
        public event Action<ProjectChangedEvent>? Changed;

        public string ProjectRoot { get; }
        public ProjectTree Tree { get; }
        public DocumentManager Documents { get; }
        public ProjectWatcher Watcher { get; }

        public ProjectSystemManager(string root) {
            ProjectRoot = root;

            Tree = new ProjectTree(root);
            Documents = new DocumentManager();
            Watcher = new ProjectWatcher(root);
            Watcher.Created += OnCreated;
            Watcher.Deleted += OnDeleted;
            Watcher.Modified += OnModified;
            Watcher.Renamed += OnRenamed;
        }

        public void Start() {
            Watcher.Start();
        }

        public void Stop() {
            Watcher.Stop();
        }

        internal void OnCreated(string path) {
            Tree.Add(path);
            Raise(ProjectChangeType.Created, path);
        }

        internal void OnDeleted(string path) {
            Tree.Remove(path);
            Raise(ProjectChangeType.Deleted, path);
        }

        internal void OnRenamed(string oldPath, string newPath) {
            Tree.Rename(oldPath, newPath);

            if (Documents.IsOpen(oldPath)) {
                Documents.Rename(oldPath, newPath);
            }

            Raise(ProjectChangeType.Renamed, newPath, oldPath);
        }

        internal void OnModified(string path) {
            var document = Documents.Get(path);

            /*
             * 文件没有打开
             *
             * 例如:
             * 外部创建/修改资源文件
             */
            if (document == null) {
                Raise(ProjectChangeType.Modified, path);
                return;
            }

            /*
             * 判断是否是真正的磁盘变化
             *
             * 过滤 FileSystemWatcher 重复事件
             */
            if (!document.IsDiskChanged()) {
                return;
            }

            /*
             * 编辑器中有修改
             *
             * 磁盘版本和内存版本冲突
             */
            if (document.IsDirty) {
                Raise(ProjectChangeType.Conflict, path);
                return;
            }

            /*
             * 没有修改
             *
             * 安全刷新
             */
            document.ReloadFromDisk();
            Raise(ProjectChangeType.Reloaded, path);
        }

        private void Raise(ProjectChangeType type, string path, string? oldPath = null) {
            Changed?.Invoke(new ProjectChangedEvent {
                Type = type,
                Path = path,
                OldPath = oldPath
            });
        }
    }
}
