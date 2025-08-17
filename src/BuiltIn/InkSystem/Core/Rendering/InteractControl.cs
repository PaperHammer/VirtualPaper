using System.Numerics;
using BuiltIn.Events;
using BuiltIn.InkSystem.Core.Brushes;
using BuiltIn.Tool.Bsae;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using Windows.Foundation;

namespace BuiltIn.InkSystem.Core.Rendering {
    public abstract class InteractControl : CanvasRenderTargetInteract {
        protected event EventHandler<CanvasPointerEventArgs>? OnInitSegement;

        /// <summary>
        /// 初始化绘制状态
        /// </summary>
        /// <param name="pointerPoint"></param>
        protected virtual void InitDrawState(PointerPoint pointerPoint) {
            _isDrawing = true;
            _previousPoint = pointerPoint.Position;
        }

        protected virtual void InitCanvasBlend() {
            _blend = CanvasBlend.SourceOver;
        }

        /// <summary>
        /// 初始化分段数据
        /// </summary>
        /// <param name="e"></param>
        protected virtual void InitSegement(CanvasPointerEventArgs e) {
            OnInitSegement?.Invoke(this, e);            
            if (_curStroke == null) return;
            var pos = e.Pointer.Position;
            _curStroke.Points.Clear();
            _curStroke.Points.Add(pos.ToVector2());
            _cachedStroke.Clear();
            _pointerQueue.Clear();
            _previousPoint = pos;
        }

        public override void HandlePressed(CanvasPointerEventArgs e) {
            if (RenderTarget == null || e.PointerPos != PointerPosition.InsideCanvas) return;

            PointerPoint pointerPoint = e.Pointer;
            if (pointerPoint.Properties.IsMiddleButtonPressed) return;

            InitCanvasBlend();
            InitDrawState(pointerPoint);
            InitSegement(e);

            RenderToTarget();
        }

        public override void HandleMoved(CanvasPointerEventArgs e) {
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


            _pointerQueue.Clear();
            _historyPoints.Clear();
            _curStroke?.Points.Clear();
            _cachedStroke.Clear();
        }

        public override void HandleReleased(CanvasPointerEventArgs e) {
            EndDrawing();
        }

        public override void HandleExited(CanvasPointerEventArgs e) {
            base.HandleExited(e);
            EndDrawing();
        }

        protected void ProcessPointerQueue() {
            while (_pointerQueue.TryDequeue(out var newPoint)) {
                double distance = Distance(newPoint, _previousPoint);
                int steps = Math.Clamp((int)(distance / 1.5), 3, _maxInterpolationSteps); // 提高插值密度

                for (int i = 1; i <= steps; i++) {
                    double t = (double)i / steps;
                    Point interpolated = CubicBezierInterpolate(_previousPoint, newPoint, t);

                    // 使用加权历史点平滑
                    Point smoothed = ApplyWeightedSmoothing(interpolated);
                    _curStroke!.Points.Add(smoothed.ToVector2());
                    _previousPoint = smoothed;

                    // 段大小控制 (关键修改点)
                    if (_curStroke.Points.Count >= _maxSegmentPoints) {
                        RenderToTarget();
                        _curStroke.Points.Clear();
                        _curStroke.Points.Add(_previousPoint.ToVector2());
                    }
                }
            }
        }

        // 三阶贝塞尔插值
        private Point CubicBezierInterpolate(Point start, Point end, double t) {
            Point cp1 = new(
                start.X + (end.X - _previousPoint.X) * 0.25,
                start.Y + (end.Y - _previousPoint.Y) * 0.25);

            Point cp2 = new(
                end.X - (end.X - start.X) * 0.25,
                end.Y - (end.Y - start.Y) * 0.25);

            double u = 1 - t;
            return new Point(
                u * u * u * start.X + 3 * u * u * t * cp1.X + 3 * u * t * t * cp2.X + t * t * t * end.X,
                u * u * u * start.Y + 3 * u * u * t * cp1.Y + 3 * u * t * t * cp2.Y + t * t * t * end.Y);
        }

        // 五点加权平滑
        private Point ApplyWeightedSmoothing(Point newPoint) {
            if (_historyPoints.Count < 3) return newPoint;

            var points = _historyPoints.ToArray();
            return new Point(
                (points[0].X * 0.1 + points[1].X * 0.2 + points[2].X * 0.4 + newPoint.X * 0.3),
                (points[0].Y * 0.1 + points[1].Y * 0.2 + points[2].Y * 0.4 + newPoint.Y * 0.3));
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
                if (RenderTarget == null || _curStroke == null) return;

                _cachedStroke.Add(_curStroke);
                var affectedRect = _curStroke.GetAffectedArea();
                using (var ds = RenderTarget.CreateDrawingSession()) {
                    ds.Blend = _blend;
                    _curStroke.Draw(ds);
                }

                HandleRender(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, affectedRect));
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
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

        protected bool _isDrawing;
        protected CanvasBlend _blend;
        protected StrokeRenderer? _curStroke;
        protected List<StrokeRenderer> _cachedStroke = [];
        protected Point _previousPoint;
        protected readonly Queue<Point> _historyPoints = new(_historySize);
        protected readonly Queue<Point> _pointerQueue = new();
    }
}

