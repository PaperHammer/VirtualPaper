using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Microsoft.UI.Input;
using Workloads.Creation.StaticImg.Models.EventArg;
using Workloads.Creation.StaticImg.Models.ToolItems.BaseTool;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    partial class EraserTool(InkCanvasConfigData data) : SegementTool(data) {
        public override void OnPointerPressed(CanvasPointerEventArgs e) {
            if (!IsPointerOverTarget(e)) return;

            PointerPoint pointerPoint = e.Pointer;
            if (pointerPoint.Properties.IsMiddleButtonPressed)
                return;

            // 初始化绘制状态
            _isDrawing = true;
            _blendedColor = BlendColor(pointerPoint.Properties.IsRightButtonPressed ?
                data.BackgroundColor : Colors.Transparent, data.EraserOpacity / 100);
            _size = (int)data.BrushThickness;
            _lastProcessedPoint = pointerPoint.Position;

            // 初始化分段数据
            _strokeSegments.Clear();
            _currentSegment = new StrokeSegment(e.Pointer.Position);
            _pointerQueue.Clear();
            _lastProcessedPoint = e.Pointer.Position;

            // 获取或创建笔刷
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

            RenderToTarget();
        }
    }
}
