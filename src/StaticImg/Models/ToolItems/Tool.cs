using System;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using VirtualPaper.UIComponent.Services;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    abstract class Tool : ICursorService {
        public event EventHandler<CursorChangedEventArgs> SystemCursorChangeRequested;
        protected virtual CanvasRenderTarget RenderTarget { get; set; }
        protected virtual RenderState State { get; set; }

        public virtual void OnPointerEntered(CanvasPointerEventArgs e, RenderState state) {
            RenderTarget = e.RenderData.RenderTarget;
            State = state;
            SystemCursorChangeRequested?.Invoke(this, new(InputSystemCursor.Create(InputSystemCursorShape.Cross)));
        }
        public virtual void OnPointerPressed(CanvasPointerEventArgs e) { }
        public virtual void OnPointerMoved(CanvasPointerEventArgs e) { }
        public virtual void OnPointerReleased(CanvasPointerEventArgs e) { }
        public virtual void OnPointerExited(CanvasPointerEventArgs e) {
            SystemCursorChangeRequested?.Invoke(this, new CursorChangedEventArgs(null));
        }
        public virtual bool IsPointerOverTarget(CanvasPointerEventArgs e) {
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

        public virtual void RequestCursorChange(InputCursor cursor) { }
    }
}
