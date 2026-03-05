using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.UndoRedo;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    partial class CropTool : CanvasAreaSelector {
        public CropTool(InkCanvasData data) {
            _data = data;
            _ratioController = new AspectRatioController(this);
            OnSelectRectChanged += CropTool_OnSelectRectChanged;
        }

        private void CropTool_OnSelectRectChanged(object? sender, Rect e) {
            _data.SelectionRect = e;
        }

        // 裁剪工具在框选过程中不截取像素，直到 Commit 再处理
        protected override void CaptureSelectionContent() {
        }

        public override IUndoableCommand? CommitSelection() {
            if (_currentState != SelectionState.Selected || _baseContent == null)
                return null;

            var command = BuildUndoCommand();
            if (command != null) {
                Reset();
                command.ExecuteAsync().Wait();
                ViewModel.Session.UnReUtil.RecordCommand(command);

                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
            }

            return command;
        }

        protected override IUndoableCommand? BuildUndoCommand() {
            if (_baseContent == null) return null;

            ArcSize originalSize = _data.CanvasSize;
            Rect cropRect = _selectionRect.RoundOutwardAsInt();

            if (cropRect.Width <= 0 || cropRect.Height <= 0) return null;

            var newSize = new ArcSize((float)cropRect.Width, (float)cropRect.Height, (uint)_baseContent.Dpi, RebuildMode.None);

            byte[] compressedOriginal = _baseContent.GetPixelBytes().CompressPixels();
            byte[] compressedNew = _baseContent.GetPixelBytes(
                (int)cropRect.X,
                (int)cropRect.Y,
                (int)cropRect.Width,
                (int)cropRect.Height).CompressPixels();

            return new LayerRebuildCommand(
                _data,
                LayerId,
                originalSize,
                newSize,
                compressedOriginal,
                compressedNew,
                () => HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion)));
        }

        protected override void RenderToTarget() {
            if (RenderTarget == null) return;

            try {
                using (var ds = RenderTarget.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);

                    // 只绘制基础内容
                    if (_baseContent != null) {
                        ds.DrawImage(_baseContent);
                    }

                    // 区分 selectiontool，croptool 仅在 commit 时更新选区

                    if (_currentState != SelectionState.None) {
                        // 绘制半透明遮罩（选区外区域变暗）
                        using (var overlayBrush = new CanvasSolidColorBrush(RenderTarget, Color.FromArgb(180, 0, 0, 0))) {
                            var outer = CanvasGeometry.CreateRectangle(ds, new Rect(0, 0, RenderTarget.Size.Width, RenderTarget.Size.Height));
                            var inner = CanvasGeometry.CreateRectangle(ds, _selectionRect);
                            ds.FillGeometry(outer.CombineWith(inner, Matrix3x2.Identity, CanvasGeometryCombine.Exclude), overlayBrush);
                        }

                        // 绘制选择框辅助线
                        using (var borderBrush = new CanvasSolidColorBrush(RenderTarget, _selectionBorderColor)) {
                            ds.DrawRectangle(_selectionRect, borderBrush, _selectionBorderWidth, _selectionStrokeStyle);
                        }
                    }
                }

                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
        }

        public void ApplyAspectRatio(double ratio) {
            RenderTarget = _data.SelectedLayer.RenderData.RenderTarget;
            RestoreOriginalContent();
            SaveBaseContent();
            _ratioController.ApplyRatio(ratio);
        }

        public override bool RestoreOriginalContent() {
            if (_baseContent == null || RenderTarget == null) return false;

            using (var ds = RenderTarget.CreateDrawingSession()) {
                ds.Blend = CanvasBlend.Copy;
                ds.DrawImage(_baseContent);
            }
            Reset();
            _baseContent?.Dispose();
            _baseContent = null;
            HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));

            return true;
        }

        private readonly AspectRatioController _ratioController;
        private readonly InkCanvasData _data;

        private class AspectRatioController(CropTool parent) {
            public void ApplyRatio(double ratio) {
                if (parent.RenderTarget == null || ratio <= 0) return;
                _currentRatio = ratio;
                CreateCenteredCrop();
                parent.RenderToTarget();
            }

            private void CreateCenteredCrop() {
                var size = CalculateInitialSize();
                parent._selectionRect = new Rect(
                    (parent.RenderTarget!.SizeInPixels.Width - size.Width) / 2,
                    (parent.RenderTarget.SizeInPixels.Height - size.Height) / 2,
                    size.Width,
                    size.Height);
                parent.UpdateSelectionRect(parent._selectionRect);
                parent._currentState = SelectionState.Selected;
            }

            private Size CalculateInitialSize() {
                const double maxScale = 0.8;
                var canvas = parent.RenderTarget!.SizeInPixels;

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
            
            private double _currentRatio;
        }
    }
}
