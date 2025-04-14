using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItemUtil {
    class EraserTool : Tool {
        public EraserTool(LayerManagerData managerData) {
            _managerData = managerData;
        }

        public override void OnPointerEntered(CanvasPointerEventArgs e) {
            _isDrawable = _managerData.SelectedLayerData.IsEnable;
            RenderTarget = e.CanvasResources.RenderTarget;
            _device = e.CanvasResources.Device;
            _canvasControl = e.CanvasResources.Control;
        }

        public override void OnPointerPressed(CanvasPointerEventArgs e) {
            PointerPoint pointerPoint = e.OriginalArgs.GetCurrentPoint(_canvasControl);
            if (!_isDrawable || pointerPoint.Properties.IsMiddleButtonPressed)
                return;

            _isDrawing = true;
            _blendedColor = BlendColor(pointerPoint.Properties.IsRightButtonPressed ?
                _managerData.BackgroundColor : Colors.Transparent, _managerData.BrushOpacity / 100);
            _lastClickPoint = pointerPoint.Position;
            _currentStroke.Clear();
            _pointerQueue.Clear();
            _currentStroke.Add(_lastClickPoint);
            _lastProcessedPoint = _lastClickPoint;

            RenderToTarget();
        }

        public override void OnPointerMoved(CanvasPointerEventArgs e) {
            if (!_isDrawable || !_isDrawing) return;

            _pointerQueue.Enqueue(e.OriginalArgs.GetCurrentPoint(_canvasControl).Position);
            ProcessPointerQueue();
        }

        public override void OnPointerReleased(CanvasPointerEventArgs e) {
            EndDrawing();
        }

        public override void OnPointerExited(CanvasPointerEventArgs e) {
            EndDrawing();
        }

        private void EndDrawing() {
            _isDrawing = false;

            if (_pointerQueue.Count > 0) {
                ProcessPointerQueue();
            }
            _pointerQueue.Clear();
            _historyPoints.Clear();
        }

        // 核心绘制逻辑
        private void RenderToTarget() {
            try {
                if (RenderTarget == null) {
                    RenderTarget = new CanvasRenderTarget(
                        CanvasDevice.GetSharedDevice(),
                        (float)_canvasControl.ActualWidth,
                        (float)_canvasControl.ActualHeight,
                        _managerData.Size.Dpi);
                }

                using (var ds = RenderTarget.CreateDrawingSession()) {
                    DrawStroke(ds);
                }
                _canvasControl.Invalidate();
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
        }

        private static bool IsDeviceLost(Exception ex) {
            return ex.HResult == unchecked((int)0x8899000C); // DXGI_ERROR_DEVICE_REMOVED
        }

        private void HandleDeviceLost() {
            _brushCache.Clear();
            RenderTarget?.Dispose();
            RenderTarget = null;
            _backBuffer?.Dispose();
            _backBuffer = null;
        }

        // 绘制笔迹 方形
        private void DrawStroke(CanvasDrawingSession ds) {
            if (_currentStroke.Count == 0) return;

            ds.Blend = CanvasBlend.Copy;
            int size = (int)_managerData.EraserSize;
            if (!_brushCache.TryGetValue((size, _blendedColor), out var brush)) {
                // 创建方形笔刷纹理
                var renderTarget = new CanvasRenderTarget(_device, size, size, _managerData.Size.Dpi);
                using (var _ds = renderTarget.CreateDrawingSession()) {
                    _ds.Blend = CanvasBlend.Copy;
                    _ds.Clear(Colors.Transparent);
                    _ds.FillRectangle(0, 0, size, size, _blendedColor); // 修改为矩形填充
                }
                _brushCache[(size, _blendedColor)] = renderTarget;
                brush = _brushCache[(size, _blendedColor)];
            }

            // 单点绘制模式
            if (_currentStroke.Count == 1) {
                var point = _currentStroke[0];
                ds.DrawImage(brush,
                    (float)(point.X - size / 2),
                    (float)(point.Y - size / 2));
                return;
            }

            // 保持相同的贝塞尔曲线逻辑
            for (int i = 1; i < _currentStroke.Count; i++) {
                var p0 = i > 1 ? _currentStroke[i - 2] : _currentStroke[i - 1];
                var p1 = _currentStroke[i - 1];
                var p2 = _currentStroke[i];
                var p3 = i < _currentStroke.Count - 1 ? _currentStroke[i + 1] : p2;

                var cp1 = new Point(
                    p1.X + (p2.X - p0.X) * GetDynamicTension(p0, p1, p2),
                    p1.Y + (p2.Y - p0.Y) * GetDynamicTension(p0, p1, p2));

                var cp2 = new Point(
                    p2.X - (p3.X - p1.X) * GetDynamicTension(p1, p2, p3),
                    p2.Y - (p3.Y - p1.Y) * GetDynamicTension(p1, p2, p3));

                // 相同插值逻辑
                for (double t = 0; t <= 1; t += 0.05) {
                    double x = Math.Pow(1 - t, 3) * p1.X +
                             3 * Math.Pow(1 - t, 2) * t * cp1.X +
                             3 * (1 - t) * t * t * cp2.X +
                             t * t * t * p2.X;

                    double y = Math.Pow(1 - t, 3) * p1.Y +
                             3 * Math.Pow(1 - t, 2) * t * cp1.Y +
                             3 * (1 - t) * t * t * cp2.Y +
                             t * t * t * p2.Y;

                    ds.DrawImage(brush, (float)(x - size / 2), (float)(y - size / 2));
                }
            }
        }

        private double GetDynamicTension(Point p0, Point p1, Point p2) {
            // 计算前后两点之间的距离
            double distancePrev = Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2));
            double distanceNext = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

            // 综合考虑前后两个距离，取平均值作为参考
            double averageDistance = (distancePrev + distanceNext) / 2;

            // 根据平均距离动态调整张力值
            return Math.Min(0.5, _baseTension * (averageDistance / 10));
        }

        private void ProcessPointerQueue() {
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
                    Point smoothed = CalculateCatmullRomSmooth();
                    _currentStroke.Add(smoothed);
                    _lastProcessedPoint = smoothed;
                }
            }

            RenderToTarget();
        }

        private static double Distance(Point newPoint, Point lastProcessedPoint) {
            return Math.Sqrt(Math.Pow(newPoint.X - lastProcessedPoint.X, 2) +
                             Math.Pow(newPoint.Y - lastProcessedPoint.Y, 2));
        }

        // Catmull-Rom样条曲线平滑算法
        private Point CalculateCatmullRomSmooth() {
            if (_historyPoints.Count < 4)
                return _historyPoints.LastOrDefault();

            var points = _historyPoints.ToArray();
            double tension = 0.5; // 可调节张力参数

            return new Point(
                CalculateCatmullRom(points[0].X, points[1].X, points[2].X, points[3].X, tension),
                CalculateCatmullRom(points[0].Y, points[1].Y, points[2].Y, points[3].Y, tension)
            );
        }

        private static double CalculateCatmullRom(double p0, double p1, double p2, double p3, double t) {
            return 0.5 * ((2 * p1) +
                         (-p0 + p2) * t +
                         (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                         (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t);
        }

        //作用​​：控制渲染频率，避免UI线程过载
        //​​推荐值​​：
        //  普通设备：8-16ms（对应60Hz屏幕）
        //  高刷设备：4-8ms（120Hz/144Hz屏幕） 
        private int _renderThrottleMs = 8;       // 渲染节流时间

        //​​作用​​：限制高速移动时的最大插值点数
        //        ​​推荐值​​：
        //手写笔记：8-12
        //绘画涂鸦：12-20
        private int _maxInterpolationSteps = 25; // 最大插值步数

        //        控制曲线平滑度（值越小越尖锐，越大越平滑）
        //            ​​推荐值​​：
        //钢笔效果：0.15-0.25
        //毛笔效果：0.25-0.35 
        private double _baseTension = 0.2;       // 基础曲线张力
        private Color _blendedColor;
        private bool _isDrawable = false, _isDrawing = false;
        private Point _lastClickPoint;
        private readonly List<Point> _currentStroke = [];
        private readonly Queue<Point> _pointerQueue = new();
        private readonly Queue<Point> _historyPoints = new(5);
        private const int _historySize = 5;
        private Point _lastProcessedPoint;
        private CanvasDevice _device;
        private CanvasControl _canvasControl;
        private CanvasRenderTarget _backBuffer;
        private readonly LayerManagerData _managerData;
        private readonly Dictionary<(int, Color), CanvasBitmap> _brushCache = [];
    }
}
