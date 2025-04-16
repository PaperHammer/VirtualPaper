using Microsoft.Graphics.Canvas;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItemUtil {
    abstract class Tool {
        protected virtual CanvasRenderTarget RenderTarget { get; set; }

        public abstract void OnPointerEntered(CanvasPointerEventArgs e);
        public abstract void OnPointerPressed(CanvasPointerEventArgs e);
        public abstract void OnPointerMoved(CanvasPointerEventArgs e);
        public abstract void OnPointerReleased(CanvasPointerEventArgs e);
        public abstract void OnPointerExited(CanvasPointerEventArgs e);

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
