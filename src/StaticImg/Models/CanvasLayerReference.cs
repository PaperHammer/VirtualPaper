using System;

namespace Workloads.Creation.StaticImg.Models {
    internal class CanvasLayerReference {
        public CanvasLayerReference(CanvasLayerResources resources, Action onReleased) {
            _weakRef = new WeakReference<CanvasLayerResources>(resources);
            _onResourceReleased = onReleased;
        }

        public bool TryGetResources(out CanvasLayerResources resources) {
            if (_weakRef.TryGetTarget(out resources))
                return true;

            _onResourceReleased?.Invoke();
            resources = null;
            return false;
        }

        private readonly WeakReference<CanvasLayerResources> _weakRef;
        private readonly Action _onResourceReleased;
    }
}
