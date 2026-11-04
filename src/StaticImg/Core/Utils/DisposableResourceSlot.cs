using System;

namespace Workloads.Creation.StaticImg.Core.Utils {
    /// <summary>
    /// Owns at most one disposable resource and guarantees that replaced or released
    /// resources are disposed exactly once by this slot.
    /// </summary>
    internal sealed partial class DisposableResourceSlot<T> : IDisposable where T : class, IDisposable {
        public T? Value { get; private set; }

        public void Replace(T value) {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(Value, value)) return;

            T? previous = Value;
            Value = value;
            DisposeSafely(previous);
        }

        public bool Release(T? expectedValue = null) {
            if (expectedValue != null && !ReferenceEquals(Value, expectedValue)) return false;

            T? resource = Value;
            Value = null;
            DisposeSafely(resource);
            return resource != null;
        }

        public void Dispose() => Release();

        private static void DisposeSafely(T? resource) {
            try {
                resource?.Dispose();
            }
            catch {
                // GPU 设备丢失或重复清理不应阻断其余资源的释放。
            }
        }
    }

    /// <summary>
    /// Owns the original-content and selected-region snapshots independently, while
    /// tracking (but not owning) the source render target for the active operation.
    /// </summary>
    internal sealed partial class SelectionResourceStore<T> : IDisposable where T : class, IDisposable {
        public T? OriginalContentSnapshot => _originalContentSnapshot.Value;
        public T? SourceRenderTarget { get; private set; }
        public T? SelectedRegionSnapshot => _selectedRegionSnapshot.Value;

        public void ReplaceOriginalContentSnapshot(T snapshot, T sourceRenderTarget) {
            ArgumentNullException.ThrowIfNull(sourceRenderTarget);
            _originalContentSnapshot.Replace(snapshot);
            SourceRenderTarget = sourceRenderTarget;
        }

        public void ReplaceSelectedRegionSnapshot(T snapshot) => _selectedRegionSnapshot.Replace(snapshot);

        public bool ReleaseOriginalContentSnapshot(T? expectedSnapshot = null) {
            bool released = _originalContentSnapshot.Release(expectedSnapshot);
            if (released) SourceRenderTarget = null;
            return released;
        }

        public bool ReleaseSelectedRegionSnapshot() => _selectedRegionSnapshot.Release();

        public void Dispose() {
            _selectedRegionSnapshot.Dispose();
            _originalContentSnapshot.Dispose();
            SourceRenderTarget = null;
        }

        private readonly DisposableResourceSlot<T> _originalContentSnapshot = new();
        private readonly DisposableResourceSlot<T> _selectedRegionSnapshot = new();
    }
}
