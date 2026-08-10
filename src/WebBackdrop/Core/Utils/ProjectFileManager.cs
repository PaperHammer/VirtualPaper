using System;
using System.Collections.Generic;
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

        public bool TryConsumeExternalChange(string filePath, out FileChangeType changeType) {
            if (_pendingExternalChanges.TryGetValue(filePath, out changeType)) {
                _pendingExternalChanges.Remove(filePath);
                return true;
            }

            changeType = default;
            return false;
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

        // 等待 150ms 无新事件后，取出队列内事件，逐个交给实际处理逻辑；取消时直接退出
        private async Task FlushChangedEventsAsync(CancellationToken cancellationToken) {
            try {
                await Task.Delay(150, cancellationToken);
                List<ProjectChangedEvent> events;
                lock (_changeLock) {
                    events = [.. _pendingChangedEvents.Values];
                    _pendingChangedEvents.Clear();
                }
                foreach (var projectChangedEvent in events) {
                    ApplyProjectChange(projectChangedEvent);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void ApplyProjectChange(ProjectChangedEvent e) {
            switch (e.Type) {
                case ProjectChangeType.Created:
                    _addToManifest(e.Path);
                    _pendingExternalChanges[e.Path] = FileChangeType.Created;
                    break;

                case ProjectChangeType.Deleted:
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
        private readonly Dictionary<string, FileChangeType> _pendingExternalChanges = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _changeLock = new();
        private readonly Dictionary<string, ProjectChangedEvent> _pendingChangedEvents = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _changeDebounceCancellation;
    }
}
