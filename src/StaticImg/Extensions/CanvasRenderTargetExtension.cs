using Microsoft.Graphics.Canvas;

namespace Workloads.Creation.StaticImg.Extensions {
    public static class CanvasRenderTargetExtension {
        public static CanvasRenderTarget Clone(this CanvasRenderTarget source) {
            var clone = new CanvasRenderTarget(
                source.Device,
                source.SizeInPixels.Width,
                source.SizeInPixels.Height,
                source.Dpi,
                source.Format,
                source.AlphaMode);

            // 运行在 GPU 上
            // GetPixelBytes 运行在 CPU 上
            clone.CopyPixelsFromBitmap(source);

            return clone;
        }   
    }
}
