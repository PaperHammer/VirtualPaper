using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.ProjectSystem;
using VirtualPaper.Common.Utils.ProjectSystem.Events;

namespace Workloads.Creation.WebBackdrop.Core.Utils {
    public enum FileChangeType {
        Created,
        Changed,
        Deleted,
        Renamed,
    }

    public partial class ProjectFileManager : IDisposable {
        public event Action<ProjectChangedEvent>? Changed;

        public ProjectSystemManager ProjectSystem { get; }

        public ProjectFileManager(
            string projectFolder,
            Action<string> addToManifest,
            Action<string> removeFromManifest,
            Action<string, string> renameInManifest) {

            _addToManifest = addToManifest;
            _removeFromManifest = removeFromManifest;
            _renameInManifest = renameInManifest;

            ProjectSystem = new ProjectSystemManager(projectFolder);
            ProjectSystem.Changed += OnProjectChanged;
        }

        public void Start() => ProjectSystem.Start();
        public void Stop() => ProjectSystem.Stop();

        public void UpdateSnapshot(string filePath) {
            ProjectSystem.Documents.Open(filePath);
        }

        public void NotifySaved(string filePath) {
            ProjectSystem.Documents.Get(filePath)?.RefreshDiskStamp();
        }

        /// <summary>
        /// 同步编辑器脏状态到文档跟踪器，使“外部修改 + 未保存编辑”能正确触发冲突而非静默覆盖。
        /// </summary>
        public void SetDirty(string filePath, bool isDirty) {
            var document = ProjectSystem.Documents.Get(filePath);
            if (document != null) {
                document.IsDirty = isDirty;
            }
        }

        public bool TryConsumeExternalChange(string filePath, out FileChangeType changeType) {
            // 写入方（watcher/防抖续体）与读取方（UI 线程）并发访问，使用原子移除避免竞态
            if (_pendingExternalChanges.TryRemove(filePath, out changeType)) {
                return true;
            }

            changeType = default;
            return false;
        }

        /// <summary>
        /// 忽略目标路径的下一次 Created 事件：用于“另存为”等不希望自动登记进 manifest 的写入。
        /// </summary>
        public void IgnoreNextCreated(string filePath) {
            lock (_changeLock) {
                _ignoreNextCreated.Add(Path.GetFullPath(filePath));
            }
        }

        public void CloseDocument(string filePath) {
            ProjectSystem.Documents.Close(filePath);
        }

        // FileSystemWatcher 一次保存常会产生多次 Modified/Reloaded，合并后避免重复清单处理、UI 刷新与文件冲突提示

        // 项目文件系统的总入口；创建、删除、重命名立即处理，修改、重载、冲突进入合并队列
        private void OnProjectChanged(ProjectChangedEvent e) {
            if (e.Type is ProjectChangeType.Modified or ProjectChangeType.Reloaded or ProjectChangeType.Conflict) {
                QueueChangedEvent(e);
                return;
            }

            ApplyProjectChange(e);
        }

        // 按文件路径暂存最新修改事件；取消上一轮 150ms 延时并重新计时，连续事件只保留一次
        private void QueueChangedEvent(ProjectChangedEvent e) {
            CancellationToken token;
            lock (_changeLock) {
                _pendingChangedEvents[e.Path] = e;
                _changeDebounceCancellation?.Cancel();
                _changeDebounceCancellation?.Dispose();
                _changeDebounceCancellation = new CancellationTokenSource();
                token = _changeDebounceCancellation.Token;
            }
            _ = FlushChangedEventsAsync(token);
        }

        // 等待 150ms 无新事件后，取出队列内事件，逐个交给实际处理逻辑；取消时直接退出。
        // 延时不用 token：Task.Delay(token) 被取消时会抛调试器可见的 TaskCanceledException 噪音
        //（与 RefreshPropertyPanelAsync 的处理方式保持一致），改为延迟后检查取消标记。
        private async Task FlushChangedEventsAsync(CancellationToken cancellationToken) {
            await Task.Delay(150);
            if (cancellationToken.IsCancellationRequested) return;

            List<ProjectChangedEvent> events;
            lock (_changeLock) {
                events = [.. _pendingChangedEvents.Values];
                _pendingChangedEvents.Clear();
            }

            foreach (var projectChangedEvent in events) {
                ApplyProjectChange(projectChangedEvent);
            }
        }

        private void ApplyProjectChange(ProjectChangedEvent e) {
            // 原子写入产生的瞬时临时文件（".xxx.tmp"）不进入清单/UI，也不触发外部变更逻辑
            if (IsTransientFile(e.Path) || (e.OldPath != null && IsTransientFile(e.OldPath))) return;

            // 调试运行时写入的临时元数据不进入清单/UI
            if (IsDebugArtifact(e.Path)) return;

            // “另存为”等写入的新文件不自动登记进 manifest
            if (e.Type == ProjectChangeType.Created && TryConsumeIgnoredCreated(e.Path)) return;

            switch (e.Type) {
                case ProjectChangeType.Created:
                    _addToManifest(e.Path);
                    _pendingExternalChanges[e.Path] = FileChangeType.Created;
                    break;

                case ProjectChangeType.Deleted:
                    // “写临时文件 + File.Move 覆盖”的原子保存可能被 watcher 报成目标文件的 Deleted；
                    // 文件实际仍在磁盘时按覆盖处理：不移除 manifest 条目、不触发 Changed 事件
                    //（否则文件树会把它从列表移除，但 manifest 里还保留着条目）。
                    if (File.Exists(e.Path)) return;

                    _removeFromManifest(e.Path);
                    _pendingExternalChanges[e.Path] = FileChangeType.Deleted;
                    break;

                case ProjectChangeType.Renamed:
                    if (e.OldPath != null) {
                        _renameInManifest(e.OldPath, e.Path);
                    }
                    _pendingExternalChanges[e.Path] = FileChangeType.Renamed;
                    break;

                case ProjectChangeType.Modified:
                case ProjectChangeType.Reloaded:
                case ProjectChangeType.Conflict:
                    _pendingExternalChanges[e.Path] = FileChangeType.Changed;
                    break;
            }

            Changed?.Invoke(e);
        }

        /// <summary>
        /// 判断是否为 WebDesignFileUtil 原子写入产生的瞬时临时文件（"." 开头、".tmp" 结尾）
        /// </summary>
        private static bool IsTransientFile(string path) {
            var fileName = Path.GetFileName(path);
            return fileName.StartsWith('.') && fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断是否为调试运行时写入项目目录的临时元数据（不纳入项目管理）
        /// </summary>
        private static bool IsDebugArtifact(string path) {
            return string.Equals(Path.GetFileName(path), "wp_metadata_basic.json", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 消费一次性的 Created 忽略标记（命中即移除）。
        /// </summary>
        private bool TryConsumeIgnoredCreated(string path) {
            lock (_changeLock) {
                return _ignoreNextCreated.Remove(Path.GetFullPath(path));
            }
        }

        private bool _isDisposed;
        public void Dispose() {
            if (!_isDisposed) {
                ProjectSystem.Changed -= OnProjectChanged;
                lock (_changeLock) {
                    _changeDebounceCancellation?.Cancel();
                    _changeDebounceCancellation?.Dispose();
                    _changeDebounceCancellation = null;
                    _pendingChangedEvents.Clear();
                }
                ProjectSystem.Stop();
                _isDisposed = true;
            }

            GC.SuppressFinalize(this);
        }

        private readonly Action<string> _addToManifest;
        private readonly Action<string> _removeFromManifest;
        private readonly Action<string, string> _renameInManifest;
        private readonly ConcurrentDictionary<string, FileChangeType> _pendingExternalChanges = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _ignoreNextCreated = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _changeLock = new();
        private readonly Dictionary<string, ProjectChangedEvent> _pendingChangedEvents = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _changeDebounceCancellation;
    }
}
