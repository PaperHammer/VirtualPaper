using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Logging;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;
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

        // 裁剪预览由 InkCanvas 的独立 Overlay 绘制，不修改任何图层像素，
        // 因此无需为交互过程保留完整尺寸的图层快照。
        protected override bool RequiresOriginalContentSnapshot => false;

        // 裁剪工具在框选过程中不截取像素，直到 Commit 再处理
        protected override void CaptureSelectedRegionSnapshot() {
        }

        public override LayerRebuildCommand? CommitSelection() {
            if (_currentState != SelectionState.Selected) return null;

            var command = BuildUndoCommand();
            if (command != null) {
                Reset();
                ExecuteAndRecordCommand(command);
            }

            return command;
        }

        private async void ExecuteAndRecordCommand(LayerRebuildCommand command) {
            try {
                await command.ExecuteAsync();
                ViewModel.Session.UnReUtil.RecordCommand(command);
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowError(ex.Message);
                ArcLog.GetLogger<CropTool>().Error(ex);
            }
        }

        protected override LayerRebuildCommand? BuildUndoCommand() {
            ArcSize originalSize;
            ArcSize newSize;
            var rawPixelDataList = new List<(Guid Tag, byte[] OldPixels, byte[] NewPixels)>();

            lock (_data) {
                originalSize = _data.CanvasSize;
                Rect canvasBounds = new(0, 0, originalSize.Width, originalSize.Height);
                Rect cropRect = _selectionRect.RoundOutwardAsInt().IntersectRect(canvasBounds);

                if (cropRect.Width <= 0 || cropRect.Height <= 0) return null;
                newSize = new ArcSize(
                    (float)cropRect.Width,
                    (float)cropRect.Height,
                    originalSize.Dpi,
                    RebuildMode.None);

                foreach (var layer in ViewModel.Data.Layers) {
                    if (layer.RenderData?.RenderTarget == null) continue;

                    CanvasRenderTarget baseRender = layer.RenderData.RenderTarget;
                    byte[] oldPixels = baseRender.GetPixelBytes();
                    byte[] newPixels = baseRender.GetPixelBytes(
                        (int)cropRect.X,
                        (int)cropRect.Y,
                        (int)cropRect.Width,
                        (int)cropRect.Height);

                    rawPixelDataList.Add((layer.Tag, oldPixels, newPixels));
                }
            }

            var originalPixelsDict = new System.Collections.Concurrent.ConcurrentDictionary<Guid, byte[]>();
            var newPixelsDict = new System.Collections.Concurrent.ConcurrentDictionary<Guid, byte[]>();

            Parallel.ForEach(rawPixelDataList, item => {
                byte[] compressedOld = item.OldPixels.CompressPixels();
                byte[] compressedNew = item.NewPixels.CompressPixels();

                originalPixelsDict.TryAdd(item.Tag, compressedOld);
                newPixelsDict.TryAdd(item.Tag, compressedNew);
            });
            //foreach (var (Tag, OldPixels, NewPixels) in rawPixelDataList) {
            //    byte[] compressedOld = OldPixels.CompressPixels();
            //    byte[] compressedNew = NewPixels.CompressPixels();

            //    originalPixelsDict.TryAdd(Tag, compressedOld);
            //    newPixelsDict.TryAdd(Tag, compressedNew);
            //}

            return new LayerRebuildCommand(
                canvasData: ViewModel.Data,
                originalSize: originalSize,
                newSize: newSize,
                compressedOriginalPixels: new Dictionary<Guid, byte[]>(originalPixelsDict),
                compressedNewPixels: new Dictionary<Guid, byte[]>(newPixelsDict),
                requestRenderAction: () => {
                    HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
                }
            );
        }

        // 选区矩形变化时 InkCanvas 会单独刷新 Overlay；裁剪预览不再写入图层 RenderTarget。
        protected override void RenderToTarget(CanvasRenderTarget? target = null) {
        }

        public void ApplyAspectRatio(double ratio) {
            if (ratio <= 0 || _data.CanvasSize.Width <= 0 || _data.CanvasSize.Height <= 0) return;
            _ratioController.ApplyRatio(ratio, _data.CanvasSize.ToSize());
        }

        public override bool RestoreOriginalContent() {
            bool hadSelection = _currentState != SelectionState.None || !_selectionRect.IsEmpty;
            if (hadSelection) Reset();
            return hadSelection;
        }

        private readonly AspectRatioController _ratioController;
        private readonly InkCanvasData _data;

        private class AspectRatioController(CropTool cropTool) {
            public void ApplyRatio(double ratio, Size canvas) {
                if (ratio <= 0) return;
                cropTool._selectionRect = CalculateAspectRatioRect(canvas, ratio);
                cropTool.UpdateSelectionRect(cropTool._selectionRect);
                cropTool._currentState = SelectionState.Selected;
            }
        }

        internal static Rect CalculateAspectRatioRect(Size canvas, double ratio) {
            const double maxScale = 0.8;

            Size size;
            if (ratio == 0) {
                size = new Size(canvas.Width * maxScale, canvas.Height * maxScale);
            }
            else {
                double maxW = canvas.Width * maxScale;
                double maxH = canvas.Height * maxScale;

                Size optionA = new(maxH * ratio, maxH);
                bool isOptionAValid = optionA.Width <= maxW;

                Size optionB = new(maxW, maxW / ratio);
                bool isOptionBValid = optionB.Height <= maxH;

                size = (isOptionAValid, isOptionBValid) switch {
                    (true, true) => ArcSize.Area(optionA) > ArcSize.Area(optionB) ? optionA : optionB, // 两者有效选面积大的
                    (true, false) => optionA, // 只有A有效
                    (false, true) => optionB, // 只有B有效
                    _ => GetFallbackSize(ratio, maxW, maxH) // 双重越界时的降级方案
                };
            }

            return new Rect(
                (canvas.Width - size.Width) / 2,
                (canvas.Height - size.Height) / 2,
                size.Width,
                size.Height);
        }

        private static Size GetFallbackSize(double ratio, double maxW, double maxH) {
            double scale = Math.Min(maxW / ratio, maxH) / maxH;
            return new Size(maxH * ratio * scale, maxH * scale);
        }
    }
}
