using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using VirtualPaper.UIComponent.Services;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    public abstract class Tool : ICursorService, IDisposable {
        public event EventHandler<CursorChangedEventArgs>? SystemCursorChangeRequested;
        public event EventHandler? RenderRequest;
        //public virtual event EventHandler<RenderTargetChangedEventArgs>? RenderRequest;

        protected Rect Viewport { get; private set; } = Rect.Empty;
        protected virtual bool HandlesPointerOutsideContentArea => false;
        protected virtual CanvasRenderTarget? RenderTarget {
            get => _renderTarget;
            set {
                if (_renderTarget != value) {
                    _renderTarget?.Dispose();
                    _renderTarget = value;
                    UpdateRelatedVariables();
                }
            }
        }

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

        protected void ChangeCursor(InputSystemCursor cursor) {
            SystemCursorChangeRequested?.Invoke(this, new CursorChangedEventArgs(cursor));
        }

        private void UpdateRelatedVariables() {
            if (RenderTarget == null) return;

            Viewport = new Rect(
                0, 0,
                RenderTarget.SizeInPixels.Width,
                RenderTarget.SizeInPixels.Height);
        }

        //protected virtual bool IsPointerOverTarget(CanvasPointerEventArgs e) {
        //    return RenderTarget != null && (HandlesPointerOutsideContentArea ||
        //        e.Pointer.Position.X >= 0 && e.Pointer.Position.X < RenderTarget.SizeInPixels.Width &&
        //        e.Pointer.Position.Y >= 0 && e.Pointer.Position.Y < RenderTarget.SizeInPixels.Height);
        //}

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

        protected virtual void Render() {
            RenderRequest?.Invoke(this, EventArgs.Empty);
        }

        public virtual void Dispose() {
            SystemCursorChangeRequested = null;
            RenderRequest = null;
            GC.SuppressFinalize(this);
        }

        protected static bool IsDeviceLost(Exception ex) {
            return ex.HResult == unchecked((int)0x8899000C); // DXGI_ERROR_DEVICE_REMOVED
        }

        private CanvasRenderTarget? _renderTarget;
    }

    public class StrokeSegment {
        public Point StartPoint { get; }
        public List<Point> Points { get; } = [];

        public StrokeSegment(Point startPoint) {
            StartPoint = startPoint;
            Points.Add(startPoint);
        }
    }
}
