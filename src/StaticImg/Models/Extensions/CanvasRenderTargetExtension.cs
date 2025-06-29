using Microsoft.Graphics.Canvas;

namespace Workloads.Creation.StaticImg.Models.Extensions {
    public static class CanvasRenderTargetExtension {
        public static CanvasRenderTarget Clone(this CanvasRenderTarget source) {
            var pixels = source.GetPixelBytes();
            var clone = new CanvasRenderTarget(
                source.Device,
                source.SizeInPixels.Width,
                source.SizeInPixels.Height,
                source.Dpi,
                source.Format,
                source.AlphaMode);

            using (var ds = clone.CreateDrawingSession()) {
                clone.SetPixelBytes(pixels);
            }

            return clone;
        }
    }
}
