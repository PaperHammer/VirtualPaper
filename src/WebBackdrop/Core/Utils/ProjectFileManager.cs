using System;
using System.Collections.Generic;
using VirtualPaper.Common.Utils.ProjectSystem;
using VirtualPaper.Common.Utils.ProjectSystem.Events;

namespace Workloads.Creation.WebBackdrop.Core.Utils {
    public enum FileChangeType {
        Created,
        Changed,
        Deleted,
        Renamed,
    }

    public class ProjectFileManager : IDisposable {
        public event Action<ProjectChangedEvent>? Changed;

        public ProjectSystemManager ProjectSystem { get; }

        public ProjectFileManager(
            string projectFolder,
            Func<string, bool> isProjectFile,
            Action<string> addToManifest,
            Action<string> removeFromManifest,
            Action<string, string> renameInManifest) {

            _isProjectFile = isProjectFile;
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

        private void OnProjectChanged(ProjectChangedEvent e) {
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
                ProjectSystem.Stop();
                _isDisposed = true;
            }

            GC.SuppressFinalize(this);
        }

        private readonly Func<string, bool> _isProjectFile;
        private readonly Action<string> _addToManifest;
        private readonly Action<string> _removeFromManifest;
        private readonly Action<string, string> _renameInManifest;
        private readonly Dictionary<string, FileChangeType> _pendingExternalChanges = new(StringComparer.OrdinalIgnoreCase);
    }
}
