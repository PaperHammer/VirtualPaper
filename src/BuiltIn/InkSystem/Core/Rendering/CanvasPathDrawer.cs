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
            if (!IsRenderReady || !CurrentStroke.ShouldRender) return;

            using var geometry = CurrentStroke.CreateStrokeGeometry(RenderTarget!.Device);
            var bounds = CurrentStroke.GetBounds();
            
            // *** TempRenderTarget 重用与增量绘制 ***
            using (var dsTemp = TempRenderTarget.CreateDrawingSession()) {
                dsTemp.Clear(Colors.Transparent);
                CurrentStroke.RenderIncrement(dsTemp, geometry); // 调用子类绘制增量
            }
            
            // *** 合成并绘制到 RenderTarget ***
            using (var mergedImage = CurrentStroke.MergeImages(
                TempRenderTarget,
                SnapshotRenderTarget,
                RenderTarget!.Device
            )) {
                // 将合成结果写入 RenderTarget
                using (var dsTarget = RenderTarget.CreateDrawingSession()) {
                    // 注意：因为 MergeImages 返回的 Effect 已经包含了 SnapshotRT 的内容，
                    // 所以我们用 Copy 模式替换 RenderTarget 的内容。
                    dsTarget.Blend = CanvasBlend.Copy;
                    dsTarget.DrawImage(mergedImage);
                }
            }
           
            HandleRender(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, bounds));
        }

        private bool _isDrawing = false;
    }
}
