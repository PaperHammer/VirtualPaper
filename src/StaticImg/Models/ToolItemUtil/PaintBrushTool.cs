using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItemUtil {
    class PaintBrushTool : Tool {
        public PaintBrushTool(LayerManagerData managerData) {
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
                _managerData.BackgroundColor : _managerData.ForegroundColor, _managerData.BrushOpacity / 100);
            _lastClickPoint = pointerPoint.Position;
            _currentStroke.Clear();
            _pointerQueue.Clear();
            _currentStroke.Add(_lastClickPoint);
            _lastProcessedPoint = _lastClickPoint;

            RenderToTarget();
        }

        public override void OnPointerMoved(CanvasPointerEventArgs e) {
            if (!_isDrawable || !_isDrawing) return;

            var newPos = e.OriginalArgs.GetCurrentPoint(_canvasControl).Position;

            // 添加最小移动距离检查（0.5像素）
            if (Math.Abs(newPos.X - _lastProcessedPoint.X) < 0.5 &&
                Math.Abs(newPos.Y - _lastProcessedPoint.Y) < 0.5) {
                return;
            }

            _pointerQueue.Enqueue(newPos);

            long now = Stopwatch.GetTimestamp();
            double elapsedMs = (now - _lastRenderTime) * 1000.0 / Stopwatch.Frequency;

            if (elapsedMs < _renderThrottleMs) return;

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

        // 绘制笔迹
        private void DrawStroke(CanvasDrawingSession ds) {
            if (_currentStroke.Count == 0) return;

            int size = (int)_managerData.BrushThickness;
            if (!_brushCache.TryGetValue((size, _blendedColor), out var brush)) {
                var renderTarget = new CanvasRenderTarget(_device, size, size, _managerData.Size.Dpi);
                using (var _ds = renderTarget.CreateDrawingSession()) {
                    _ds.Clear(Colors.Transparent);
                    _ds.FillCircle(size / 2, size / 2, size / 2, _blendedColor);
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

            // 使用优化的贝塞尔曲线连接点
            for (int i = 1; i < _currentStroke.Count; i++) {
                var p0 = i > 1 ? _currentStroke[i - 2] : _currentStroke[i - 1];
                var p1 = _currentStroke[i - 1];
                var p2 = _currentStroke[i];
                var p3 = i < _currentStroke.Count - 1 ? _currentStroke[i + 1] : p2;

                // 优化的控制点计算
                //double tension = 0.3; // 降低张力值减少曲线波动
                var cp1 = new Point(
                    p1.X + (p2.X - p0.X) * _baseTension,
                    p1.Y + (p2.Y - p0.Y) * _baseTension);

                var cp2 = new Point(
                    p2.X - (p3.X - p1.X) * _baseTension,
                    p2.Y - (p3.Y - p1.Y) * _baseTension);

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

                    ds.DrawImage(brush, (float)(x - size / 2), (float)(y - size / 2));
                }
            }
        }

        private void ProcessPointerQueue() {
            while (_pointerQueue.Count > 0) {
                var newPoint = _pointerQueue.Dequeue();

                // 添加到历史点
                _historyPoints.Enqueue(newPoint);
                if (_historyPoints.Count > _historySize)
                    _historyPoints.Dequeue();

                // 计算加权平均点
                Point smoothedPoint = CalculateWeightedAverage();
                _currentStroke.Add(smoothedPoint);
                _lastProcessedPoint = smoothedPoint;
            }

            _lastRenderTime = Stopwatch.GetTimestamp();
            RenderToTarget();
        }

        private Point CalculateWeightedAverage() {
            if (_historyPoints.Count == 0) return _lastProcessedPoint;

            double totalWeight = 0;
            double sumX = 0;
            double sumY = 0;
            int count = _historyPoints.Count;

            // 使用指数衰减权重（最近的点权重更大）
            foreach (var point in _historyPoints) {
                double weight = Math.Pow(0.7, count--); // 0.7是衰减因子
                sumX += point.X * weight;
                sumY += point.Y * weight;
                totalWeight += weight;
            }

            return new Point(sumX / totalWeight, sumY / totalWeight);
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
        private int _maxInterpolationSteps = 12; // 最大插值步数

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
        private long _lastRenderTime;
        private Point _lastProcessedPoint;
        private CanvasDevice _device;
        private CanvasControl _canvasControl;
        private readonly LayerManagerData _managerData;
        private readonly Dictionary<(int, Color), CanvasBitmap> _brushCache = [];
    }
}
