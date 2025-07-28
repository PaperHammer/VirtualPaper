using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;
using Workloads.Creation.StaticImg.Models.ToolItems.Utils;

namespace Workloads.Creation.StaticImg.Models.ToolItems.Base {
    internal abstract class TrackMapping(InkCanvasConfigData data) : Tool {
        /// <summary>
        /// 初始化绘制状态
        /// </summary>
        /// <param name="pointerPoint"></param>
        protected virtual void InitDrawState(PointerPoint pointerPoint) {
            _isDrawing = true;
            _blendedColor = BlendColor(pointerPoint.Properties.IsRightButtonPressed ?
                data.BackgroundColor : data.ForegroundColor, data.BrushOpacity / 100);
            _thickness = (int)data.BrushThickness;
            _lastProcessedPoint = pointerPoint.Position;
        }

        protected virtual void InitCanvasBlend() {
            _blend = CanvasBlend.SourceOver;
        }

        /// <summary>
        /// 初始化分段数据
        /// </summary>
        /// <param name="e"></param>
        protected virtual void InitSegement(CanvasPointerEventArgs e) {
            var pos = e.Pointer.Position;
            _cachedStroke.Clear();
            _tmpStroke = StrokeFactory.CreateStroke(data.SelectedBrush.Type);
            _tmpStroke.Points.Clear();
            _tmpStroke.Points.Add(pos.ToVector2());
            _tmpStroke.Color = _blendedColor;
            _tmpStroke.Thickness = _thickness;
            _tmpStroke.Brush = BrushManager.GetBrush(
                _blendedColor, _thickness, BrushShape.Circle, RenderTarget!.Format, RenderTarget.AlphaMode);
            _pointerQueue.Clear();
            _lastProcessedPoint = pos;
        }

        public override void OnPointerPressed(CanvasPointerEventArgs e) {
            if (e.PointerPos != PointerPosition.InsideCanvas || RenderTarget == null) return;

            PointerPoint pointerPoint = e.Pointer;
            if (pointerPoint.Properties.IsMiddleButtonPressed) return;

            InitCanvasBlend();
            InitDrawState(pointerPoint);
            InitSegement(e);

            RenderToTarget();
        }

        public override void OnPointerMoved(CanvasPointerEventArgs e) {
            if (!_isDrawing || e.PointerPos != PointerPosition.InsideCanvas) {
                EndDrawing();
                return;
            }

            _pointerQueue.Enqueue(e.Pointer.Position);
            ProcessPointerQueue();
        }

        protected void EndDrawing() {
            if (!_isDrawing) return;

            _isDrawing = false;

            if (_pointerQueue.Count > 0) {
                ProcessPointerQueue();
            }

            Record( _blend, [.. _cachedStroke], data.SelectedInkCanvas.RenderData.RenderTarget);

            _pointerQueue.Clear();
            _historyPoints.Clear();
            _tmpStroke.Points.Clear();
            _cachedStroke.Clear();
        }

        public override void OnPointerReleased(CanvasPointerEventArgs e) {
            EndDrawing();
        }

        public override void OnPointerExited(CanvasPointerEventArgs e) {
            base.OnPointerExited(e);
            EndDrawing();
        }

        protected void ProcessPointerQueue() {
            while (_pointerQueue.TryDequeue(out var newPoint)) {
                double distance = Distance(newPoint, _lastProcessedPoint);
                int steps = Math.Clamp((int)(distance / 2), 1, _maxInterpolationSteps);

                // 线性插值确保点密度
                for (int i = 1; i <= steps; i++) {
                    double t = (double)i / steps;
                    Point interpolated = new(
                        _lastProcessedPoint.X + t * (newPoint.X - _lastProcessedPoint.X),
                        _lastProcessedPoint.Y + t * (newPoint.Y - _lastProcessedPoint.Y));

                    _historyPoints.Enqueue(interpolated);
                    if (_historyPoints.Count > _historySize)
                        _historyPoints.Dequeue();

                    // 使用更高效的平滑算法
                    Point smoothed = _historyPoints.Count < 4 ?
                        interpolated : CalculateCatmullRomSmooth();

                    _tmpStroke.Points.Add(smoothed.ToVector2());
                    _lastProcessedPoint = smoothed;

                    // 段大小控制 (关键修改点)
                    if (_tmpStroke.Points.Count >= _maxSegmentPoints) {
                        RenderToTarget();
                        _tmpStroke.Points.Clear();
                        _tmpStroke.Points.Add(_lastProcessedPoint.ToVector2());
                    }
                }
            }
        }

        protected static double Distance(Point newPoint, Point lastProcessedPoint) {
            return Math.Sqrt(Math.Pow(newPoint.X - lastProcessedPoint.X, 2) +
                             Math.Pow(newPoint.Y - lastProcessedPoint.Y, 2));
        }

        // Catmull-Rom样条曲线平滑算法
        protected Point CalculateCatmullRomSmooth() {
            if (_historyPoints.Count < 4)
                return _historyPoints.LastOrDefault();

            var points = _historyPoints.ToArray();
            double tension = 0.5; // 可调节张力参数

            return new Point(
                CalculateCatmullRom(points[0].X, points[1].X, points[2].X, points[3].X, tension),
                CalculateCatmullRom(points[0].Y, points[1].Y, points[2].Y, points[3].Y, tension)
            );
        }

        protected static double CalculateCatmullRom(double p0, double p1, double p2, double p3, double t) {
            return 0.5 * ((2 * p1) +
                         (-p0 + p2) * t +
                         (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                         (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t);
        }

        // 核心绘制逻辑
        protected void RenderToTarget() {
            try {
                if (RenderTarget == null) return;

                _cachedStroke.Add(_tmpStroke);
                var affectedRect = _tmpStroke.GetAffectedArea();
                using (var ds = RenderTarget.CreateDrawingSession()) {
                    ds.Blend = _blend;
                    _tmpStroke.Draw(ds);
                }

                OnRendered(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, affectedRect));
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
        }

        protected void HandleDeviceLost() {
            _tmpStroke.Points.Clear();
            _cachedStroke.Clear();
        }

        /// <summary>
        /// 最大插值步数-限制高速移动时的最大插值点数
        /// <para>推荐值：</para>
        /// <para>手写笔记：8-12</para>
        /// <para>绘画涂鸦：12-20</para>
        /// </summary>
        protected readonly int _maxInterpolationSteps = 30;

        /// <summary>
        /// 每段最大点数-合并多个点为一条线段
        /// <para>较大会导致绘制效果不跟手, 较小没什么用</para>
        /// <para>推荐值：6-12</para>
        /// </summary>
        protected const int _maxSegmentPoints = 6; // 每段最大点数

        protected const int _historySize = 5; // 平滑历史点数
        protected Color _blendedColor;
        protected bool _isDrawing;
        protected Point _lastProcessedPoint;
        protected float _thickness;
        protected readonly Queue<Point> _pointerQueue = new();
        protected readonly Queue<Point> _historyPoints = new(_historySize);
        protected StrokeBase _tmpStroke;
        protected List<StrokeBase> _cachedStroke = [];
        protected CanvasBlend _blend;
    }
}
