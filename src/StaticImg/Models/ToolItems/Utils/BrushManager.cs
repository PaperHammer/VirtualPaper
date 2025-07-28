using System.Collections.Generic;
using System.Linq;
using Microsoft.Graphics.Canvas;
using Windows.Graphics.DirectX;
using Windows.UI;

namespace Workloads.Creation.StaticImg.Models.ToolItems.Utils {
    public static class BrushManager {
        public static ICanvasImage GetBrush(
            Color color, 
            float thickness, 
            BrushShape brushShape,
            DirectXPixelFormat format, 
            CanvasAlphaMode alphaMode) {
            if (!_brushCache.TryGetValue((thickness, color), out CanvasBitmap? brush)) {
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

                var renderTarget = new CanvasRenderTarget(
                    MainPage.Instance.SharedDevice, 
                    thickness, 
                    thickness, 
                    MainPage.Instance.Bridge.GetHardwareDpi(),
                    format,
                    alphaMode);
                using (var _tmpDs = renderTarget.CreateDrawingSession()) {
                    _tmpDs.Blend = CanvasBlend.Copy;
                    if (brushShape == BrushShape.Circle) {
                        _tmpDs.FillCircle(thickness / 2, thickness / 2, thickness / 2, color);
                    }
                    else {
                        _tmpDs.FillRectangle(0, 0, thickness, thickness, color);
                    }
                }
                _brushCache[(thickness, color)] = renderTarget;
                brush = renderTarget;
            }

            return brush;
        }

        private static readonly Dictionary<(float, Color), CanvasBitmap> _brushCache = [];
    }

    public enum BrushShape {
        Circle, Rectangle,
    }
}
