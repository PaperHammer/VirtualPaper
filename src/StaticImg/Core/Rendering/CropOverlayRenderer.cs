using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;

namespace Workloads.Creation.StaticImg.Core.Rendering {
    /// <summary>
    /// 在最终合成图像上方绘制裁剪辅助层，不读取或修改任何图层 RenderTarget。
    /// </summary>
    internal sealed partial class CropOverlayRenderer : IDisposable {
        public void Draw(CanvasDrawingSession drawingSession, Rect canvasBounds, Rect cropRect) {
            if (canvasBounds.IsEmpty || cropRect.IsEmpty) return;

            using var canvasGeometry = CanvasGeometry.CreateRectangle(drawingSession, canvasBounds);
            using var cropGeometry = CanvasGeometry.CreateRectangle(drawingSession, cropRect);
            using var outsideGeometry = canvasGeometry.CombineWith(
                cropGeometry,
                Matrix3x2.Identity,
                CanvasGeometryCombine.Exclude);

            drawingSession.FillGeometry(outsideGeometry, OverlayColor);
            drawingSession.DrawRectangle(cropRect, BorderColor, BorderWidth, _strokeStyle);
        }

        public void Dispose() {
            _strokeStyle.Dispose();
        }

        private static readonly Color OverlayColor = Color.FromArgb(180, 0, 0, 0);
        private static readonly Color BorderColor = Colors.Black;
        private const float BorderWidth = 5f;

        private readonly CanvasStrokeStyle _strokeStyle = new() {
            DashStyle = CanvasDashStyle.Dash,
        };
    }
}
