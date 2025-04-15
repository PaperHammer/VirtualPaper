using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItemUtil {
    class FillTool(LayerBasicData data) : Tool {
        public override void OnPointerEntered(CanvasPointerEventArgs e) {
            RenderTarget = e.RenderData.RenderTarget;
            //_device = e.CanvasResources.Device;
            //_canvasControl = e.CanvasResources.Control;

            if (RenderTarget == null) {
                RenderTarget = new CanvasRenderTarget(
                    MainPage.Instance.SharedDevice,
                    (float)_canvasControl.ActualWidth,
                    (float)_canvasControl.ActualHeight,
                    data.Size.Dpi);
            }
        }

        public override void OnPointerMoved(CanvasPointerEventArgs e) { }

        public override void OnPointerPressed(CanvasPointerEventArgs e) {
            PointerPoint pointerPoint = e.Pointer;
            _blendedColor = pointerPoint.Properties.IsRightButtonPressed ? data.BackgroundColor : data.ForegroundColor;
            _lastClickPoint = pointerPoint.Position;
            RenderToTarget();
        }

        public override void OnPointerReleased(CanvasPointerEventArgs e) { }

        public override void OnPointerExited(CanvasPointerEventArgs e) { }

        // 核心绘制逻辑
        private void RenderToTarget() {
            try {
                if (RenderTarget == null) {
                    return;
                }

                using (var ds = RenderTarget.CreateDrawingSession()) {
                    FloodFill(_lastClickPoint, _blendedColor, ds);
                }
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
        }

        private static bool IsDeviceLost(Exception ex) {
            return ex.HResult == unchecked((int)0x8899000C); // DXGI_ERROR_DEVICE_REMOVED
        }

        private void HandleDeviceLost() {
            RenderTarget?.Dispose();
            RenderTarget = null;
        }

        public void FloodFill(Point startPoint, Color fillColor, CanvasDrawingSession ds) {
            if (RenderTarget == null || ds == null) return;

            // 获取画布尺寸和像素数据
            int width = (int)RenderTarget.SizeInPixels.Width;
            int height = (int)RenderTarget.SizeInPixels.Height;
            byte[] pixels = RenderTarget.GetPixelBytes();

            // 边界检查（带坐标钳位）
            int startX = (int)Math.Clamp(startPoint.X, 0, width - 1);
            int startY = (int)Math.Clamp(startPoint.Y, 0, height - 1);

            // 获取目标颜色
            int startIndex = (startY * width + startX) * 4;
            Color targetColor = Color.FromArgb(
                pixels[startIndex + 3],
                pixels[startIndex + 2],
                pixels[startIndex + 1],
                pixels[startIndex]);

            // 快速跳过相同颜色
            if (fillColor.Equals(targetColor)) return;

            // 创建访问标记数组（比ConcurrentQueue更高效）
            bool[] visited = new bool[width * height];
            var stack = new Stack<(int left, int right, int y)>();

            // 初始扫描线
            var firstSpan = FindHorizontalSpan(startX, startY, width, pixels, targetColor, visited);
            stack.Push(firstSpan);

            // 设置混合模式（确保覆盖原有像素）
            ds.Blend = CanvasBlend.Copy;

            // 处理扫描线
            while (stack.Count > 0) {
                var (left, right, y) = stack.Pop();

                // 直接绘制当前扫描线
                ds.FillRectangle(left, y, right - left + 1, 1, fillColor);

                // 检查上下行
                ScanAdjacentRow(y - 1, left, right, width, height, pixels, targetColor, visited, stack); // 上一行
                ScanAdjacentRow(y + 1, left, right, width, height, pixels, targetColor, visited, stack); // 下一行
            }
        }

        // 辅助方法：查找水平连续区域
        private (int left, int right, int y) FindHorizontalSpan(int x, int y, int width, byte[] pixels, Color targetColor, bool[] visited) {
            int left = x;
            while (left > 0 && !visited[y * width + left - 1] && IsPixelMatch(left - 1, y, width, pixels, targetColor))
                left--;

            int right = x;
            while (right < width - 1 && !visited[y * width + right + 1] && IsPixelMatch(right + 1, y, width, pixels, targetColor))
                right++;

            // 标记已访问
            for (int i = left; i <= right; i++)
                visited[y * width + i] = true;

            return (left, right, y);
        }

        // 辅助方法：扫描相邻行
        private void ScanAdjacentRow(int y, int left, int right, int width, int height, byte[] pixels, Color targetColor, bool[] visited, Stack<(int left, int right, int y)> stack) {
            if (y < 0 || y >= height) return;

            for (int x = left; x <= right; x++) {
                if (!visited[y * width + x] && IsPixelMatch(x, y, width, pixels, targetColor)) {
                    var span = FindHorizontalSpan(x, y, width, pixels, targetColor, visited);
                    stack.Push(span);
                    x = span.right; // 跳过已处理区域
                }
            }
        }

        // 优化后的像素检查（避免重复计算索引）
        private static bool IsPixelMatch(int x, int y, int width, byte[] pixels, Color targetColor) {
            int index = (y * width + x) * 4;
            return index + 3 < pixels.Length &&
                   Math.Abs(pixels[index + 3] - targetColor.A) < 5 &&
                   Math.Abs(pixels[index + 2] - targetColor.R) < 5 &&
                   Math.Abs(pixels[index + 1] - targetColor.G) < 5 &&
                   Math.Abs(pixels[index] - targetColor.B) < 5;
        }

        private Color _blendedColor;
        private Point _lastClickPoint;
        //private CanvasDevice _device;
        private CanvasControl _canvasControl;
    }
}
