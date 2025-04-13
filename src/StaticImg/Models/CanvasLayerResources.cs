using System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace Workloads.Creation.StaticImg.Models {
    internal sealed partial class CanvasLayerResources : IDisposable {
        public CanvasControl Control { get; init; }
        public CanvasRenderTarget RenderTarget { get; private set; }
        public CanvasDevice Device { get; private set; }

        public CanvasLayerResources(CanvasControl control, CanvasDevice device, LayerManagerData layerManagerData) {
            Control = control ?? throw new ArgumentNullException(nameof(control));
            Device = device ?? CanvasDevice.GetSharedDevice();
            _layerManagerData = layerManagerData;
            InitializeRenderTarget();

            // 自动注册清理回调
            control.Unloaded += (s, e) => Dispose();
        }

        private void InitializeRenderTarget() {
            RenderTarget?.Dispose();
            RenderTarget = new CanvasRenderTarget(
                Device,
                (float)Control.ActualWidth,
                (float)Control.ActualHeight,
                _layerManagerData.Size.Dpi);
        }

        public void ResizeRenderTarget() {
            if (Control.ActualWidth > 0 && Control.ActualHeight > 0)
                InitializeRenderTarget();
        }

        public void Dispose() {
            RenderTarget?.Dispose();
            GC.SuppressFinalize(this);
        }

        private readonly LayerManagerData _layerManagerData;
    }
}
