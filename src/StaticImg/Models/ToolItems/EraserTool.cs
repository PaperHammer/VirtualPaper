using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Microsoft.UI.Input;
using Workloads.Creation.StaticImg.Models.ToolItems.BaseTool;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    partial class EraserTool(InkCanvasConfigData data) : DrawingTool(data) {
        protected override void InitDrawState(PointerPoint pointerPoint) {
            _canvasBlend = CanvasBlend.Copy;
            _isDrawing = true;
            _blendedColor = BlendColor(pointerPoint.Properties.IsRightButtonPressed ?
                _data.BackgroundColor : Colors.Transparent, _data.EraserOpacity / 100);
            _size = (int)_data.EraserSize;
            _lastProcessedPoint = pointerPoint.Position;
        }

        protected override void InitBrush() {
            if (!_brushCache.TryGetValue((_size, _blendedColor), out _brush)) {
                // 创建方形笔刷纹理
                var renderTarget = new CanvasRenderTarget(
                    MainPage.Instance.SharedDevice, _size, _size, _data.Size.Dpi);
                using (var _tmpDs = renderTarget.CreateDrawingSession()) {
                    _tmpDs.Blend = CanvasBlend.Copy;
                    _tmpDs.Clear(Colors.Transparent);
                    _tmpDs.FillRectangle(0, 0, _size, _size, _blendedColor);
                }
                _brushCache[(_size, _blendedColor)] = renderTarget;
                _brush = _brushCache[(_size, _blendedColor)];
            }
        }
        
        private readonly InkCanvasConfigData _data = data;
    }
}
