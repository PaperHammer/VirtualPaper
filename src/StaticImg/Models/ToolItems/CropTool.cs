using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Logging;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;
using Windows.UI;
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

        // 裁剪工具在框选过程中不截取像素，直到 Commit 再处理
        protected override void CaptureSelectedRegionSnapshot() {
        }

        public override LayerRebuildCommand? CommitSelection() {
            if (_currentState != SelectionState.Selected || OriginalContentSnapshot == null)
                return null;

            LayerId = _data.SelectedLayer.Tag;
            var command = BuildUndoCommand();
            if (command != null) {
                CanvasRenderTarget? originalSnapshot = OriginalContentSnapshot;
                Reset();
                // 撤销命令已经持有所需像素，异步裁剪不再需要完整画布副本。
                ReleaseOriginalContentSnapshot(originalSnapshot);
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
                if (OriginalContentSnapshot == null) return null;

                originalSize = _data.CanvasSize;
                Rect cropRect = _selectionRect.RoundOutwardAsInt().IntersectRect(OriginalContentSnapshot.Bounds);

                if (cropRect.Width <= 0 || cropRect.Height <= 0) return null;
                newSize = new ArcSize((float)cropRect.Width, (float)cropRect.Height, (uint)OriginalContentSnapshot.Dpi, RebuildMode.None);

                foreach (var layer in ViewModel.Data.Layers) {
                    if (layer.RenderData?.RenderTarget == null) continue;

                    var baseRender = layer.Tag == LayerId ? OriginalContentSnapshot : layer.RenderData.RenderTarget;
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

        protected override void RenderToTarget(CanvasRenderTarget? target = null) {
            target ??= SourceRenderTarget ?? RenderTarget;
            if (target == null) return;

            try {
                using (var ds = target.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);

                    // 只绘制基础内容
                    if (OriginalContentSnapshot != null) {
                        ds.DrawImage(OriginalContentSnapshot);
                    }

                    // 区分 selectiontool，croptool 仅在 commit 时更新选区

                    if (_currentState != SelectionState.None) {
                        // 绘制半透明遮罩（选区外区域变暗）
                        using (var overlayBrush = new CanvasSolidColorBrush(target, Color.FromArgb(180, 0, 0, 0))) {
                            var outer = CanvasGeometry.CreateRectangle(ds, new Rect(0, 0, target.Size.Width, target.Size.Height));
                            var inner = CanvasGeometry.CreateRectangle(ds, _selectionRect);
                            ds.FillGeometry(outer.CombineWith(inner, Matrix3x2.Identity, CanvasGeometryCombine.Exclude), overlayBrush);
                        }

                        // 绘制选择框辅助线
                        using (var borderBrush = new CanvasSolidColorBrush(target, _selectionBorderColor)) {
                            ds.DrawRectangle(_selectionRect, borderBrush, _selectionBorderWidth, _selectionStrokeStyle);
                        }
                    }
                }

                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SEHException) {
                Reset();
                ReleaseOriginalContentSnapshot();
                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
            }
        }

        public void ApplyAspectRatio(double ratio) {
            if (ratio <= 0 || !IsCanvasReady) return;

            try {
                SaveOriginalContentSnapshot();

                CanvasRenderTarget? target = SourceRenderTarget;
                if (target == null || !IsTargetStillAttached(target)) {
                    Reset();
                    ReleaseOriginalContentSnapshot();
                    HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
                    return;
                }

                // SaveOriginalContentSnapshot 已经捕获了完整底图，无需再把相同像素写回目标。
                // 后续比例计算和预览绘制使用同一个目标实例，避免撤销/重做
                // 替换 RenderTarget 时混用新旧 WinRT 资源。
                _ratioController.ApplyRatio(ratio, target);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SEHException) {
                Reset();
                ReleaseOriginalContentSnapshot();
                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
            }
        }

        public override bool RestoreOriginalContent() {
            if (OriginalContentSnapshot == null || SourceRenderTarget == null) return false;

            CanvasRenderTarget originalSnapshot = OriginalContentSnapshot;
            CanvasRenderTarget target = SourceRenderTarget;
            bool isReset = false;
            try {
                if (IsTargetStillAttached(target)) {
                    using var ds = target.CreateDrawingSession();
                    ds.Blend = CanvasBlend.Copy;
                    ds.DrawImage(originalSnapshot);
                }
                Reset();
                isReset = true;
                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SEHException) {
                // 撤销/重做可能在校验后立即替换目标；此时只清理旧预览。
                if (!isReset) {
                    Reset();
                    isReset = true;
                }
                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
            }
            finally {
                if (!isReset) Reset();
                ReleaseOriginalContentSnapshot(originalSnapshot);
            }

            return true;
        }

        private readonly AspectRatioController _ratioController;
        private readonly InkCanvasData _data;

        private class AspectRatioController(CropTool cropTool) {
            public void ApplyRatio(double ratio, CanvasRenderTarget target) {
                if (ratio <= 0) return;
                _currentRatio = ratio;
                CreateCenteredCrop(target);
                cropTool.RenderToTarget(target);
            }

            private void CreateCenteredCrop(CanvasRenderTarget target) {
                var canvas = target.Size;
                var size = CalculateInitialSize(canvas);
                cropTool._selectionRect = new Rect(
                    (canvas.Width - size.Width) / 2,
                    (canvas.Height - size.Height) / 2,
                    size.Width,
                    size.Height);
                cropTool.UpdateSelectionRect(cropTool._selectionRect);
                cropTool._currentState = SelectionState.Selected;
            }

            private Size CalculateInitialSize(Size canvas) {
                const double maxScale = 0.8;

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
