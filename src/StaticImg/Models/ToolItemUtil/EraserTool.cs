using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using VirtualPaper.UIComponent.Utils.Extensions;
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
            _canvasControl = e.CanvasResources.Control;
        }

        public override void OnPointerPressed(CanvasPointerEventArgs e) {
            var pointerPoint = e.OriginalArgs.GetCurrentPoint(_canvasControl);
            if (!_isDrawable || pointerPoint.Properties.IsMiddleButtonPressed)
                return;

            _isDrawing = true;
            _lastPosition = pointerPoint.Position;
            _currentErasePath.Clear();
            _currentErasePath.Add(_lastPosition);

            RenderToTarget();
        }

        public override void OnPointerMoved(CanvasPointerEventArgs e) {
            if (!_isDrawable || !_isDrawing) return;

            var newPos = e.OriginalArgs.GetCurrentPoint(_canvasControl).Position;

            // 移动距离检查（1像素阈值）
            if (newPos.DistanceTo(_lastPosition) < 1.0)
                return;

            _currentErasePath.Add(newPos);
            _lastPosition = newPos;

            // 渲染节流（8ms）
            long now = Stopwatch.GetTimestamp();
            double elapsedMs = (now - _lastRenderTime) * 1000.0 / Stopwatch.Frequency;
            if (elapsedMs < 8) return;

            ProcessErasePath();
            _lastRenderTime = now;
        }

        public override void OnPointerReleased(CanvasPointerEventArgs e) => EndErasering();
        public override void OnPointerExited(CanvasPointerEventArgs e) => EndErasering();

        private void EndErasering() {
            _isDrawing = false;
            if (_currentErasePath.Count > 0)
                ProcessErasePath();
            _currentErasePath.Clear();
        }

        // 核心擦除逻辑
        private void RenderToTarget() {
            if (RenderTarget == null || _canvasControl.ActualWidth <= 0 || _canvasControl.ActualHeight <= 0)
                return;

            using (var ds = RenderTarget.CreateDrawingSession()) {
                // 方形擦除实现
                var rect = new Rect(
                    _lastPosition.X - _managerData.EraserSize / 2,
                    _lastPosition.Y - _managerData.EraserSize / 2,
                    _managerData.EraserSize,
                    _managerData.EraserSize);

                // 使用透明色"擦除"（实际是绘制透明矩形）
                ds.FillRectangle(rect, Colors.Transparent);

                // 羽化效果实现
                if (_managerData.EraserFeather > 0) {
                    // 计算动态透明度（随羽化半径变化）
                    byte alpha = (byte)(50 * Math.Min(1, _managerData.EraserFeather / 3));
                    var featherColor = Color.FromArgb(alpha, 255, 255, 255);

                    // 分两次绘制实现内外羽化
                    ds.DrawRectangle(rect, featherColor, (float)_managerData.EraserFeather);
                    // 内羽化（手动内缩1像素）
                    var innerRect = new Rect(
                        rect.X + 1,
                        rect.Y + 1,
                        rect.Width - 2,
                        rect.Height - 2);
                    ds.DrawRectangle(innerRect, featherColor, (float)(_managerData.EraserFeather * 0.7));
                }
            }
            _canvasControl.Invalidate();
        }

        // 处理连续擦除路径
        private void ProcessErasePath() {
            if (RenderTarget == null || _currentErasePath.Count < 2)
                return;

            using (var ds = RenderTarget.CreateDrawingSession()) {
                double halfSize = _managerData.EraserSize / 2;

                for (int i = 1; i < _currentErasePath.Count; i++) {
                    var prev = _currentErasePath[i - 1];
                    var current = _currentErasePath[i];

                    double dx = current.X - prev.X;
                    double dy = current.Y - prev.Y;
                    double length = Math.Sqrt(dx * dx + dy * dy);

                    int segments = Math.Max(1, (int)Math.Ceiling(length / halfSize));

                    for (int j = 0; j <= segments; j++) {
                        double t = j / (double)segments;
                        var eraseRect = new Rect(
                            prev.X + dx * t - halfSize,
                            prev.Y + dy * t - halfSize,
                            _managerData.EraserSize,
                            _managerData.EraserSize);

                        ds.FillRectangle(eraseRect, Colors.Transparent);
                    }
                }
            }
            _canvasControl.Invalidate();
        }

        private bool _isDrawable, _isDrawing;
        private Point _lastPosition;
        private readonly List<Point> _currentErasePath = [];
        private long _lastRenderTime;
        private CanvasControl _canvasControl;
        private readonly LayerManagerData _managerData;
    }
}
