using Microsoft.Graphics.Canvas;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItemUtil {
    abstract class Tool {
        protected virtual CanvasRenderTarget RenderTarget { get; set; }

        public virtual void OnPointerEntered(CanvasPointerEventArgs e) {
            RenderTarget = e.RenderData.RenderTarget;
        }
        public virtual void OnPointerPressed(CanvasPointerEventArgs e) { }
        public virtual void OnPointerMoved(CanvasPointerEventArgs e) { }
        public virtual void OnPointerReleased(CanvasPointerEventArgs e) { }
        public virtual void OnPointerExited(CanvasPointerEventArgs e) { }
        public virtual bool IsPointerOverTaregt(CanvasPointerEventArgs e) {
            return e.Pointer.Position.X >= 0 && e.Pointer.Position.X < RenderTarget.SizeInPixels.Width &&
                   e.Pointer.Position.Y >= 0 && e.Pointer.Position.Y < RenderTarget.SizeInPixels.Height;
        }

        protected static Color BlendColor(Color color, double brushOpacity) {
            byte blendedA = (byte)(color.A * brushOpacity);

            return Color.FromArgb(
                blendedA,
                color.R,
                color.G,
                color.B
            );
        }
    }
}
