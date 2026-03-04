using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Microsoft.UI.Input;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Workloads.Creation.StaticImg.Events;

namespace Workloads.Creation.StaticImg.Core.Rendering {
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
            CaptureUndoRedoSnapshot();
            base.RequestOnceRender();
        }

        public override void HandleReleased(CanvasPointerEventArgs e) {
            EndDrawing();
        }

        public override void HandleExited(CanvasPointerEventArgs e) {
            base.HandleExited(e);
            EndDrawing();
        }

        protected void RenderToTarget() {
            if (!IsRenderReady || !CurrentStroke.ShouldRender) return;

            using var geometry = CurrentStroke.CreateStrokeGeometry(RenderTarget!.Device);
            var bounds = CurrentStroke.GetBounds();

            // *** TempRenderTarget 重用与增量绘制 ***
            using (var dsTemp = TempRenderTarget.CreateDrawingSession()) {
                dsTemp.Clear(Colors.Transparent);
                CurrentStroke.RenderIncrement(dsTemp, geometry);
            }

            // *** 合成并绘制到 RenderTarget ***
            using (var mergedImage = CurrentStroke.MergeImages(
                TempRenderTarget,
                SnapshotRenderTarget,
                RenderTarget!.Device
            )) {
                // 将合成结果写入 RenderTarget
                using (var dsTarget = RenderTarget.CreateDrawingSession()) {
                    // 因为 MergeImages 返回的 Effect 已经包含了 SnapshotRT 的内容，
                    // 所以用 Copy 模式替换 RenderTarget 的内容。
                    dsTarget.Blend = CanvasBlend.Copy;
                    dsTarget.DrawImage(mergedImage);
                }
            }

            HandleRender(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, bounds));
        }

        private void CaptureUndoRedoSnapshot() {
            if (CurrentStroke == null) return;

            var rawBounds = CurrentStroke.GetBounds();
            var dirtyRect = EnlargeToIntegerBounds(rawBounds, RenderTarget.SizeInPixels);
            int x = (int)dirtyRect.Left;
            int y = (int)dirtyRect.Top;
            int w = (int)dirtyRect.Width;
            int h = (int)dirtyRect.Height;

            if (w <= 0 || h <= 0) return;

            byte[] originalPixels = SnapshotRenderTarget.GetPixelBytes(x, y, w, h);
            byte[] currentPixels = RenderTarget.GetPixelBytes(x, y, w, h);
            var command = new RegionPixelSnapshotCommand(
                LayerId,
                ViewModel.Data,
                dirtyRect,
                originalPixels,
                currentPixels,
                "Path Drawer",
                (region) => HandleRender(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, region))
            );

            ViewModel.Session.UnReUtil.RecordCommand(command);
        }

        private static Rect EnlargeToIntegerBounds(Rect rect, BitmapSize maxBounds) {
            int left = (int)Math.Floor(rect.Left);
            int top = (int)Math.Floor(rect.Top);
            int right = (int)Math.Ceiling(rect.Right);
            int bottom = (int)Math.Ceiling(rect.Bottom);

            left = Math.Max(0, left);
            top = Math.Max(0, top);
            right = Math.Min((int)maxBounds.Width, right);
            bottom = Math.Min((int)maxBounds.Height, bottom);

            return new Rect(left, top, right - left, bottom - top);
        }

        private bool _isDrawing = false;
    }
}
