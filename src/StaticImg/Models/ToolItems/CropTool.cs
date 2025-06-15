using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.ToolItems.BaseTool;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    partial class CropTool : AreaSelector {
        public CropTool(InkCanvasConfigData data) : base(data) {
            this._ratioController = new AspectRatioController(this);
            this._data = data;
        }

        public override bool CommitSelection() {
            if (_currentState != SelectionState.Selected || _selectionContent == null)
                return false;

            // 创建临时绘图目标
            CanvasRenderTarget newBaseContent = null;
            try {
                newBaseContent = new CanvasRenderTarget(
                    RenderTarget,
                    RenderTarget.SizeInPixels.Width,
                    RenderTarget.SizeInPixels.Height,
                    RenderTarget.Dpi);

                using (var ds = newBaseContent.CreateDrawingSession()) {
                    // 清空整个画布
                    ds.Clear(Colors.Transparent);

                    // 将选区内容绘制到原始位置
                    ds.DrawImage(_selectionContent,
                        new Rect(_selectionRect.X, _selectionRect.Y,
                                _selectionRect.Width, _selectionRect.Height));
                }

                // 安全替换基础内容
                var oldContent = _baseContent;
                _baseContent = newBaseContent;
                oldContent?.Dispose();

                // 重置选区状态
                _currentState = SelectionState.None;
                SafeDispose(ref _selectionContent);
                RenderToTarget();

                return true;
            }
            catch {
                newBaseContent?.Dispose();
                throw;
            }
        }

        protected override void RenderToTarget() {
            try {
                using (var ds = RenderTarget.CreateDrawingSession()) {
                    // 完全清空画布
                    ds.Clear(Colors.Transparent);

                    // 只绘制基础内容（提交后就是选区内容+透明背景）
                    if (_baseContent != null) {
                        ds.DrawImage(_baseContent);
                    }

                    // 绘制进行中的选区状态
                    if (_currentState != SelectionState.None) {
                        // 绘制选区内容（拖拽预览）
                        if (_selectionContent != null) {
                            ds.DrawImage(_selectionContent, (float)_selectionRect.X, (float)_selectionRect.Y);
                        }

                        // 绘制半透明遮罩（选区外区域）
                        using (var overlayBrush = new CanvasSolidColorBrush(RenderTarget, Color.FromArgb(180, 0, 0, 0))) {
                            var outer = CanvasGeometry.CreateRectangle(ds,
                                new Rect(0, 0, RenderTarget.Size.Width, RenderTarget.Size.Height));
                            var inner = CanvasGeometry.CreateRectangle(ds, _selectionRect);
                            ds.FillGeometry(outer.CombineWith(inner, Matrix3x2.Identity, CanvasGeometryCombine.Exclude), overlayBrush);
                        }

                        // 绘制选择框
                        using (var borderBrush = new CanvasSolidColorBrush(RenderTarget, _selectionBorderColor)) {
                            ds.DrawRectangle(_selectionRect, borderBrush, _selectionBorderWidth, _selectionStrokeStyle);
                        }
                    }
                }

                Render();
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
        }

        public void ApplyAspectRatio(double ratio) {
            RenderTarget = _data.SelectedInkCanvas.RenderData.RenderTarget;
            RestoreOriginalContent();
            SaveBaseContent();
            _ratioController.ApplyRatio(ratio);
        }

        private readonly AspectRatioController _ratioController;
        private readonly InkCanvasConfigData _data;

        internal class AspectRatioController(CropTool parent) {
            private double _currentRatio;

            public void ApplyRatio(double ratio) {
                _currentRatio = ratio;
                if (ratio <= 0) return;
                CreateCenteredCrop();
                parent.RenderToTarget();
            }

            private void CreateCenteredCrop() {
                var size = CalculateInitialSize();
                parent._selectionRect = new Rect(
                    (parent.RenderTarget.SizeInPixels.Width - size.Width) / 2,
                    (parent.RenderTarget.SizeInPixels.Height - size.Height) / 2,
                    size.Width,
                    size.Height);
                parent.UpdateSelectionRect(parent._selectionRect);
                parent._currentState = SelectionState.Selected;
                parent.CaptureSelectionContent();
            }

            private Size CalculateInitialSize() {
                const double maxScale = 0.8;
                var canvas = parent.RenderTarget.SizeInPixels;

                // 自由比例模式（使用图片中的默认值）
                if (_currentRatio == 0)
                    return new Size(canvas.Width * maxScale, canvas.Height * maxScale);

                // 预设比例模式
                double ratio = _currentRatio;
                double maxW = canvas.Width * maxScale;
                double maxH = canvas.Height * maxScale;

                // 高度优先
                Size optionA = new(maxH * ratio, maxH);
                bool isOptionAValid = optionA.Width <= maxW;

                // 宽度优先
                Size optionB = new(maxW, maxW / ratio);
                bool isOptionBValid = optionB.Height <= maxH;

                return (isOptionAValid, isOptionBValid) switch {
                    (true, true) => ArcSize.Area(optionA) > ArcSize.Area(optionB) ? optionA : optionB, // 两者有效选面积大的
                    (true, false) => optionA, // 只有A有效
                    (false, true) => optionB, // 只有B有效
                    _ => GetFallbackSize(ratio, maxW, maxH) // 双重越界时的降级方案
                };
            }

            private static Size GetFallbackSize(double ratio, double maxW, double maxH) {
                // 比例缩放
                double scale = Math.Min(maxW / ratio, maxH) / maxH;
                return new Size(maxH * ratio * scale, maxH * scale);
            }
        }
    }
}
