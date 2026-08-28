using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Microsoft.UI.Input;
using VirtualPaper.Common.Extensions;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Workloads.Creation.StaticImg.Core.Brushes;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;
using Workloads.Creation.StaticImg.Events;

namespace Workloads.Creation.StaticImg.Core.Rendering {
    /// <summary>
    /// 2D 画布路径绘制器基类
    /// </summary>
    public abstract class CanvasPathDrawer : RenderBase {
        protected StrokeBase CurrentStroke { get; set; } = null!;
        protected CanvasRenderTarget TempRenderTarget { get; private set; } = null!;
        protected CanvasRenderTarget SnapshotRenderTarget { get; private set; } = null!;

        public override bool IsCanvasReady {
            get {
                if (!base.IsCanvasReady) return false;
                try {
                    if (TempRenderTarget == null || SnapshotRenderTarget == null || CurrentStroke == null) return false;
                    var test1 = TempRenderTarget.Device;
                    var test2 = SnapshotRenderTarget.Device;
                    return true;
                }
                catch {
                    return false;
                }
            }
        }

        protected void EnsurePathBuffersReady() {
            if (RenderTarget == null) return;

            var size = RenderTarget.Size;

            // 如果缓冲区不存在，或者尺寸/DPI与主画布不一致，则重新创建
            if (TempRenderTarget == null ||
                TempRenderTarget.Size != size ||
                TempRenderTarget.Dpi != RenderTarget.Dpi) {
                TempRenderTarget?.Dispose();
                TempRenderTarget = new CanvasRenderTarget(
                    RenderTarget.Device,
                    (float)size.Width,
                    (float)size.Height,
                    RenderTarget.Dpi,
                    RenderTarget.Format,
                    RenderTarget.AlphaMode);
            }

            if (SnapshotRenderTarget == null ||
                SnapshotRenderTarget.Size != size ||
                SnapshotRenderTarget.Dpi != RenderTarget.Dpi) {
                SnapshotRenderTarget?.Dispose();
                SnapshotRenderTarget = new CanvasRenderTarget(
                    RenderTarget.Device,
                    (float)size.Width,
                    (float)size.Height,
                    RenderTarget.Dpi,
                    RenderTarget.Format,
                    RenderTarget.AlphaMode);
            }
        }

        protected virtual void InitDrawState(Vector2 vec) {
            _isDrawing = true;
            _previousStrokeBounds = Rect.Empty;
            CurrentStroke.Points = [];
            CurrentStroke.Points.Add(vec);
            EnsurePathBuffersReady();

            // 每条新笔画只在落笔时清空一次，避免上一条笔画的遮罩残留。
            using (var tempDs = TempRenderTarget.CreateDrawingSession())
                tempDs.Clear(Colors.Transparent);

            using var ds = SnapshotRenderTarget.CreateDrawingSession();
            ds.Clear(Colors.Transparent);
            ds.DrawImage(RenderTarget);
        }

        protected abstract void InitCurrentStroke(CanvasPointerEventArgs e);

        public override void HandlePressed(CanvasPointerEventArgs e) {
            if (e.PointerPos != PointerPosition.InsideCanvas) return;

            PointerPoint pointerPoint = e.Pointer;
            if (pointerPoint.Properties.IsMiddleButtonPressed) return;

            InitCurrentStroke(e);
            InitDrawState(pointerPoint.Position.ToVector2());
            RenderToTarget();
        }

        public override void HandleMoved(CanvasPointerEventArgs e) {
            if (!IsCanvasReady || !_isDrawing || e.PointerPos != PointerPosition.InsideCanvas) {
                EndDrawing();
                return;
            }

            if (StrokePointSampler.AddOrUpdate(CurrentStroke.Points, e.Pointer.Position.ToVector2()))
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
            if (!IsCanvasReady || !CurrentStroke.ShouldRender) return;

            try {
                using var geometry = CurrentStroke.CreateStrokeGeometry(RenderTarget!.Device);
                var bounds = CurrentStroke.GetBounds();
                var dirtyBounds = CombineBounds(_previousStrokeBounds, bounds);

                // *** TempRenderTarget 重用与增量绘制 ***
                using (var dsTemp = TempRenderTarget.CreateDrawingSession()) {
                    // 同时清理上一帧范围，覆盖采样器替换末端点后边界轻微收缩的情况。
                    dsTemp.Blend = CanvasBlend.Copy;
                    dsTemp.FillRectangle(dirtyBounds, Colors.Transparent);
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
                        // 效果链以完整画布坐标计算。通过裁剪层限制写回区域，不能把
                        // dirtyBounds 同时作为效果源矩形，否则部分效果会丢失背景源。
                        using var clipLayer = dsTarget.CreateLayer(1f, dirtyBounds);
                        dsTarget.DrawImage(mergedImage);
                    }
                }

                _previousStrokeBounds = bounds;
                HandleRender(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, dirtyBounds));
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
            catch (ObjectDisposedException) {
                // 渲染目标被意外清理，安全退出本次渲染
                EndDrawing();
            }
            catch (Exception ex) {
                ReportFatalError(ex);
                EndDrawing();
            }
        }

        private static Rect CombineBounds(Rect previous, Rect current) {
            if (previous == Rect.Empty) return current;

            double left = Math.Min(previous.Left, current.Left);
            double top = Math.Min(previous.Top, current.Top);
            double right = Math.Max(previous.Right, current.Right);
            double bottom = Math.Max(previous.Bottom, current.Bottom);
            return new Rect(left, top, right - left, bottom - top);
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

            byte[] originalPixels = SnapshotRenderTarget.GetPixelBytes(x, y, w, h).CompressPixels();
            byte[] currentPixels = RenderTarget.GetPixelBytes(x, y, w, h).CompressPixels();
            var command = new RegionPixelSnapshotCommand(
                LayerId,
                ViewModel.Data,
                dirtyRect,
                originalPixels,
                currentPixels,
                true,
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

        public override void Dispose() {
            base.Dispose();
            TempRenderTarget?.Dispose();
            SnapshotRenderTarget?.Dispose();
            GC.SuppressFinalize(this);
        }

        private bool _isDrawing = false;
        private Rect _previousStrokeBounds = Rect.Empty;
    }
}
