using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.Services;

/// <summary>Keep requests until UI has handled them, including while UI is starting or closing.</summary>
internal sealed class ProjectOpenQueue {
    public void Enqueue(IEnumerable<string> paths) {
        var normalized = ProjectFileActivation.NormalizePaths(paths);
        if (normalized.Length == 0) return;
        lock (_lock) {
            _requests.Enqueue(new(Guid.NewGuid().ToString("N"), normalized));
            _canRetryUi = true;
        }
    }

    public ProjectOpenRequest? Peek() {
        lock (_lock) return _requests.TryPeek(out var request) ? request : null;
    }

    public void Complete(string id) {
        lock (_lock) {
            if (_requests.TryPeek(out var request) && request.Id == id) _requests.Dequeue();
        }
    }

    // Normal closing hands pending opens to a new UI. An unexpected exit gets
    // only one recovery attempt; normal handoff does not spend that attempt.
    public bool TryReopenUi(bool expectedExit = false) {
        lock (_lock) {
            if (_requests.Count == 0) return false;
            if (expectedExit) return true;
            if (!_canRetryUi) return false;
            _canRetryUi = false;
            return true;
        }
    }

    private readonly object _lock = new();
    private readonly Queue<ProjectOpenRequest> _requests = new();
    private bool _canRetryUi;
}
