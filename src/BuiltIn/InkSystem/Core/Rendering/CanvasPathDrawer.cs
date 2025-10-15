using System.Numerics;
using BuiltIn.Events;
using BuiltIn.InkSystem.Tool;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Microsoft.UI.Input;
using VirtualPaper.Common.Extensions;
using Windows.Foundation;

namespace BuiltIn.InkSystem.Core.Rendering {
    /// <summary>
    /// 2D 画布路径绘制器基类
    /// </summary>
    public abstract class CanvasPathDrawer : RenderBase {
        protected virtual void InitDrawState(Vector2 vec) {
            _isDrawing = true;
            CurrentStroke.Points = [];
            CurrentStroke.Points.Add(vec);

            using var ds = SnapshotRenderTarget.CreateDrawingSession();
            ds.Clear(Colors.Transparent);
            ds.DrawImage(RenderTarget);
        }

        protected abstract void InitCurrentStroke(CanvasPointerEventArgs e);

        public override void HandlePressed(CanvasPointerEventArgs e) {
            if (!IsCanvasReady || e.PointerPos != PointerPosition.InsideCanvas) return;

            PointerPoint pointerPoint = e.Pointer;
            if (pointerPoint.Properties.IsMiddleButtonPressed) return;

            InitCurrentStroke(e);
            InitDrawState(pointerPoint.Position.ToVector2());
            RenderToTarget();
        }

        public override void HandleMoved(CanvasPointerEventArgs e) {
            if (!IsRenderReady || !_isDrawing || e.PointerPos != PointerPosition.InsideCanvas) {
                EndDrawing();
                return;
            }

            CurrentStroke.Points.Add(e.Pointer.Position.ToVector2());
            RenderToTarget();
        }

        protected void EndDrawing() {
            if (!_isDrawing) return;

            _isDrawing = false;
        }

        public override void HandleReleased(CanvasPointerEventArgs e) {
            EndDrawing();
        }

        public override void HandleExited(CanvasPointerEventArgs e) {
            base.HandleExited(e);
            EndDrawing();
        }

        protected void RenderToTarget() {
            if (!IsRenderReady || !CurrentStroke.ShouldRender)
                return;

            var bounds = CurrentStroke.GetBounds().IntersectRect(RenderTarget!.Bounds);
            using (var dsTemp = TempRenderTarget.CreateDrawingSession()) {
                dsTemp.Clear(Colors.Transparent);

                using var geometry = CurrentStroke.CreateStrokeGeometry(dsTemp.Device);
                CurrentStroke.Render(dsTemp, geometry, bounds, SnapshotRenderTarget);
            }

            Merge(bounds);
            HandleRender(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, bounds));
        }

        protected virtual void Merge(Rect bounds) {
            using (var ds = RenderTarget!.CreateDrawingSession()) {
                ds.Blend = CanvasBlend.Copy;
                ds.DrawImage(SnapshotRenderTarget);
                ds.Blend = CanvasBlend.SourceOver;
                ds.DrawImage(TempRenderTarget, bounds, bounds);
            }
        }

        private bool _isDrawing = false;
    }
}
