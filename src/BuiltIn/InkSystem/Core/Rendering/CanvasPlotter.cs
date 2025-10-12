using System.Numerics;
using BuiltIn.Events;
using BuiltIn.InkSystem.Core.Brushes;
using BuiltIn.InkSystem.Tool.Bsae;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Microsoft.UI.Input;
using VirtualPaper.Common.Extensions;

namespace BuiltIn.InkSystem.Core.Rendering {
    /// <summary>
    /// 2D 画布绘制器基类
    /// </summary>
    public abstract class CanvasPlotter : RenderBase {
        protected StrokeBase CurrentStroke { get; set; } = null!;

        /// <summary>
        /// 初始化绘制状态
        /// </summary>
        /// <param name="pointerPoint"></param>
        protected virtual void InitDrawState(PointerPoint p) {
            _isDrawing = true;
            _currentPoints = [];
            _currentPoints.Add(p.Position.ToVector2());
            //_allStrokes.Add((CurrentStroke, _currentPoints));

            if (RenderTarget != null && SnapshotRenderTarget != null) {
                using var ds = SnapshotRenderTarget.CreateDrawingSession();
                ds.Clear(Colors.Transparent);
                ds.DrawImage(RenderTarget);
            }
        }

        protected abstract void InitCurrentStroke(CanvasPointerEventArgs e);

        public override void HandlePressed(CanvasPointerEventArgs e) {
            if (RenderTarget == null || e.PointerPos != PointerPosition.InsideCanvas) return;

            PointerPoint pointerPoint = e.Pointer;
            if (pointerPoint.Properties.IsMiddleButtonPressed) return;

            InitCurrentStroke(e);
            InitDrawState(pointerPoint);
            RenderToTarget();
        }

        public override void HandleMoved(CanvasPointerEventArgs e) {
            if (CurrentStroke == null || !_isDrawing || e.PointerPos != PointerPosition.InsideCanvas) {
                EndDrawing();
                return;
            }

            var vec = e.Pointer.Position.ToVector2();
            _currentPoints.Add(vec);
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
            if (RenderTarget == null || TempRenderTarget == null || _currentPoints.Count == 0 || CurrentStroke == null)
                return;

            var dirty = CurrentStroke.GetBounds(_currentPoints).IntersectRect(RenderTarget.Bounds);
            using (var dsTemp = TempRenderTarget.CreateDrawingSession()) {
                dsTemp.Clear(Colors.Transparent);

                using var geometry = CreateStrokeGeometry(dsTemp.Device, _currentPoints, CurrentStroke.BrushArgs.Thickness);

                switch (CurrentStroke.EffectMode) {
                    case StrokeMode.Normal:
                        CurrentStroke.DrawStroke(dsTemp, geometry, _currentPoints.Count == 1);
                        break;
                    case StrokeMode.OnlyEffect:
                    case StrokeMode.OnlyEffect | StrokeMode.AddEffect:
                        CurrentStroke.ApplyEffect(dsTemp, geometry, dirty, _currentPoints.Count == 1);
                        break;
                    case StrokeMode.OnlyEffect | StrokeMode.AddEffectWithCopy:
                        CurrentStroke.ApplyEffectWithCopy(SnapshotRenderTarget, dsTemp, geometry, dirty, _currentPoints.Count == 1);
                        break;
                    default:
                        break;
                }
            }

            using (var ds = RenderTarget.CreateDrawingSession()) {
                if ((CurrentStroke.EffectMode & StrokeMode.AddEffectWithCopy) == 0) {
                    ds.Blend = CanvasBlend.Copy;
                    ds.DrawImage(SnapshotRenderTarget);
                    ds.Blend = CanvasBlend.SourceOver;
                    ds.DrawImage(TempRenderTarget, dirty, dirty);
                }
                else {
                    ds.Blend = CanvasBlend.Copy;
                    ds.DrawImage(TempRenderTarget, dirty, dirty);
                }
            }

            HandleRender(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, dirty));
        }

        private static CanvasGeometry CreateStrokeGeometry(CanvasDevice device, List<Vector2> points, float thickness) {
            if (points.Count == 1)
                return CanvasGeometry.CreateCircle(device, points[0], thickness / 2);

            using var builder = new CanvasPathBuilder(device);
            builder.BeginFigure(points[0]);
            for (int i = 1; i < points.Count - 1; i++) {
                var mid = (points[i] + points[i + 1]) / 2;
                builder.AddQuadraticBezier(points[i], mid);
            }
            builder.AddLine(points[^1]);
            builder.EndFigure(CanvasFigureLoop.Open);

            return CanvasGeometry.CreatePath(builder);
        }

        private bool _isDrawing = false;
        private List<Vector2> _currentPoints = [];
        //private readonly List<(StrokeBase Stroke, List<Vector2> Points)> _allStrokes = [];        
    }
}
