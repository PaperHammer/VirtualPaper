using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Microsoft.UI.Input;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItems.BaseTool {
    internal abstract class DrawingTool(InkCanvasConfigData data) : Tool {
        public override void OnPointerPressed(CanvasPointerEventArgs e) {
            if (e.PointerPos != PointerPosition.InsideCanvas) return;

            PointerPoint pointerPoint = e.Pointer;
            if (pointerPoint.Properties.IsMiddleButtonPressed)
                return;

            InitDrawState(pointerPoint);
            InitSegement(e);
            InitBrush();

            RenderToTarget();
        }

        /// <summary>
        /// 初始化笔刷
        /// </summary>
        protected virtual void InitBrush() {
            if (!_brushCache.TryGetValue((_size, _blendedColor), out _brush)) {
                var renderTarget = new CanvasRenderTarget(
                    MainPage.Instance.SharedDevice, _size, _size, data.Size.Dpi,
                    Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    CanvasAlphaMode.Premultiplied);
                using (var _tmpDs = renderTarget.CreateDrawingSession()) {
                    _tmpDs.Clear(Colors.Transparent);
                    _tmpDs.FillCircle(_size / 2, _size / 2, _size / 2, _blendedColor);
                }
                _brushCache[(_size, _blendedColor)] = renderTarget;
                _brush = renderTarget;
            }
        }

        /// <summary>
        /// 初始化分段数据
        /// </summary>
        /// <param name="e"></param>
        protected virtual void InitSegement(CanvasPointerEventArgs e) {
            _strokeSegments.Clear();
            _currentSegment = new StrokeSegment(e.Pointer.Position);
            _pointerQueue.Clear();
            _lastProcessedPoint = e.Pointer.Position;
        }

        /// <summary>
        /// 初始化绘制状态
        /// </summary>
        /// <param name="pointerPoint"></param>
        protected virtual void InitDrawState(PointerPoint pointerPoint) {
            _isDrawing = true;
            _blendedColor = BlendColor(pointerPoint.Properties.IsRightButtonPressed ?
                data.BackgroundColor : data.ForegroundColor, data.BrushOpacity / 100);
            _size = (int)data.BrushThickness;
            _lastProcessedPoint = pointerPoint.Position;
        }

        public override void OnPointerMoved(CanvasPointerEventArgs e) {
            if (!_isDrawing || e.PointerPos != PointerPosition.InsideCanvas) {
                EndDrawing();
                return;
            }

            _pointerQueue.Enqueue(e.Pointer.Position);
            ProcessPointerQueue();
        }

        public override void OnPointerReleased(CanvasPointerEventArgs e) {
            EndDrawing();
        }

        public override void OnPointerExited(CanvasPointerEventArgs e) {
            base.OnPointerExited(e);
            EndDrawing();
        }

        protected void EndDrawing() {
            if (!_isDrawing) return;

            _isDrawing = false;

            if (_pointerQueue.Count > 0) {
                ProcessPointerQueue();
            }

            if (_currentSegment.Points.Count > 0) {
                RenderToTarget();
            }

            _strokeSegments.Clear();
            _pointerQueue.Clear();
            _historyPoints.Clear();
            data.SelectedInkCanvas.RenderData.DirtyRegion = Rect.Empty;

            // 定期清理不常用的笔刷 超过20个笔刷时清理
            if (_brushCache.Count > 20) {
                var toRemove = _brushCache
                    .OrderByDescending(x => x.Key.Item1) // 按笔刷大小排序
                    .Skip(10) // 保留最常用的10个
                    .ToList();

                foreach (var item in toRemove) {
                    item.Value?.Dispose();
                    _brushCache.Remove(item.Key);
                }
            }
        }

        // 核心绘制逻辑
        protected void RenderToTarget() {
            try {
                if (RenderTarget == null || _currentSegment.Points.Count == 0) return;

                _strokeSegments.AddLast(_currentSegment);
                using (var ds = RenderTarget.CreateDrawingSession()) {
                    DrawSegment(ds, _currentSegment.Points);
                }

                //Render();
                var dirtyRect = CalculateSegmentBounds(_currentSegment);
                OnRendered(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, dirtyRect));
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
        }

        protected void HandleDeviceLost() {
            _brushCache.Clear();
            RenderTarget?.Dispose();
            RenderTarget = null;
        }

        protected Rect CalculateSegmentBounds(StrokeSegment segment) {
            if (segment.Points.Count == 0) return Rect.Empty;

            double minX = segment.Points.Min(p => p.X) - _size;
            double minY = segment.Points.Min(p => p.Y) - _size;
            double maxX = segment.Points.Max(p => p.X) + _size;
            double maxY = segment.Points.Max(p => p.Y) + _size;

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        // 绘制笔迹
        protected void DrawSegment(CanvasDrawingSession ds, List<Point> points) {
            if (points.Count == 0) return;

            ds.Blend = _canvasBlend;
            // 单点绘制模式
            if (points.Count == 1) {
                ds.DrawImage(_brush,
                    (float)(points[0].X - _size / 2),
                    (float)(points[0].Y - _size / 2));
                return;
            }

            // 使用优化的贝塞尔曲线连接点
            for (int i = 1; i < points.Count; i++) {
                var p0 = i > 1 ? points[i - 2] : points[i - 1];
                var p1 = points[i - 1];
                var p2 = points[i];
                var p3 = i < points.Count - 1 ? points[i + 1] : p2;

                // 优化的控制点计算
                var cp1 = new Point(
                    p1.X + (p2.X - p0.X) * GetDynamicTension(p0, p1, p2),
                    p1.Y + (p2.Y - p0.Y) * GetDynamicTension(p0, p1, p2));

                var cp2 = new Point(
                    p2.X - (p3.X - p1.X) * GetDynamicTension(p1, p2, p3),
                    p2.Y - (p3.Y - p1.Y) * GetDynamicTension(p1, p2, p3));

                // 更精细的曲线分段
                // 从0.1改为0.05增加细分
                for (double t = 0; t <= 1; t += 0.05) {
                    double x = Math.Pow(1 - t, 3) * p1.X +
                             3 * Math.Pow(1 - t, 2) * t * cp1.X +
                             3 * (1 - t) * t * t * cp2.X +
                             t * t * t * p2.X;

                    double y = Math.Pow(1 - t, 3) * p1.Y +
                             3 * Math.Pow(1 - t, 2) * t * cp1.Y +
                             3 * (1 - t) * t * t * cp2.Y +
                             t * t * t * p2.Y;

                    ds.DrawImage(_brush, (float)(x - _size / 2), (float)(y - _size / 2));
                }
            }
        }

        protected double GetDynamicTension(Point p0, Point p1, Point p2) {
            // 计算前后两点之间的距离
            double distancePrev = Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2));
            double distanceNext = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

            // 综合考虑前后两个距离，取平均值作为参考
            double averageDistance = (distancePrev + distanceNext) / 2;

            // 根据平均距离动态调整张力值
            return Math.Min(0.5, _baseTension * (averageDistance / 10));
        }

        protected void ProcessPointerQueue() {
            while (_pointerQueue.TryDequeue(out var newPoint)) {
                // 动态计算插值步数（基于移动速度）
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

                    // 添加到当前段
                    _currentSegment.Points.Add(smoothed);
                    _lastProcessedPoint = smoothed;

                    // 段大小控制 (关键修改点)
                    if (_currentSegment.Points.Count >= _maxSegmentPoints) {
                        RenderToTarget();
                        _currentSegment = new StrokeSegment(_lastProcessedPoint);
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

        #region dispose
        protected bool _disposed = false;
        public override void Dispose() {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
                // 释放托管资源
                ReleaseAllResources();
            }
            base.Dispose();

            _disposed = true;
        }

        protected void ReleaseAllResources() {
            foreach (var brush in _brushCache.Values) {
                brush?.Dispose();
            }
            _brushCache.Clear();
        }
        #endregion

        #region fileds & props
        // 渲染控制参数
        /// <summary>
        /// 渲染节流时间-控制渲染频率，避免UI线程过载
        /// <para>推荐值：</para>
        /// <para>普通设备：8-16ms（对应60Hz屏幕）</para>
        /// <para>高刷设备：4-8ms（120Hz/144Hz屏幕） </para>
        /// </summary>
        protected readonly int _renderThrottleMs = 8;

        /// <summary>
        /// 最大插值步数-限制高速移动时的最大插值点数
        /// <para>推荐值：</para>
        /// <para>手写笔记：8-12</para>
        /// <para>绘画涂鸦：12-20</para>
        /// </summary>
        protected readonly int _maxInterpolationSteps = 25;

        /// <summary>
        /// 基础曲线张力-控制曲线平滑度（值越小越尖锐，越大越平滑）
        /// <para>推荐值：</para>
        /// <para>钢笔效果：0.15-0.25</para>
        /// <para>毛笔效果：0.25-0.35  </para>
        /// </summary>
        protected readonly double _baseTension = 0.2;
        protected const int _historySize = 5; // 平滑历史点数

        /// <summary>
        /// 每段最大点数-合并多个点为一条线段
        /// <para>较大会导致绘制效果不跟手, 较小没什么用</para>
        /// <para>推荐值：6-12</para>
        /// </summary>
        protected const int _maxSegmentPoints = 6; // 每段最大点数

        // 绘制状态
        protected Color _blendedColor;
        protected bool _isDrawing;
        protected Point _lastProcessedPoint;
        protected int _size;

        // 数据结构
        protected readonly LinkedList<StrokeSegment> _strokeSegments = new();
        protected StrokeSegment _currentSegment = null!;
        protected readonly Queue<Point> _pointerQueue = new();
        protected readonly Queue<Point> _historyPoints = new(_historySize);
        protected readonly Dictionary<(int, Color), CanvasBitmap> _brushCache = [];
        protected CanvasBitmap? _brush;
        protected DateTime _lastRenderTime = DateTime.MinValue;
        protected CanvasBlend _canvasBlend = CanvasBlend.SourceOver;
        #endregion
    }
}
