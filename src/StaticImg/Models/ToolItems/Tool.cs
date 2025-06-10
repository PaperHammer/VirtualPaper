using System;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using VirtualPaper.UIComponent.Services;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    abstract class Tool : ICursorService, IDisposable {
        public event EventHandler<CursorChangedEventArgs>? SystemCursorChangeRequested;
        public virtual event EventHandler<RenderTargetChangedEventArgs>? RenderRequest;

        protected virtual CanvasRenderTarget? RenderTarget { get; set; }

        public virtual void OnPointerEntered(CanvasPointerEventArgs e) {
            RenderTarget = e.RenderData.RenderTarget;
            SystemCursorChangeRequested?.Invoke(this, new(InputSystemCursor.Create(InputSystemCursorShape.Cross)));
        }
        public virtual void OnPointerPressed(CanvasPointerEventArgs e) { }
        public virtual void OnPointerMoved(CanvasPointerEventArgs e) { }
        public virtual void OnPointerReleased(CanvasPointerEventArgs e) { }
        public virtual void OnPointerExited(CanvasPointerEventArgs e) {
            SystemCursorChangeRequested?.Invoke(this, new CursorChangedEventArgs(null));
        }
        public virtual bool IsPointerOverTarget(CanvasPointerEventArgs e) {
            return RenderTarget != null &&
                e.Pointer.Position.X >= 0 && e.Pointer.Position.X < RenderTarget.SizeInPixels.Width &&
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

        public virtual void Dispose() {
            SystemCursorChangeRequested = null;
            RenderRequest = null;
        }
    }
}
