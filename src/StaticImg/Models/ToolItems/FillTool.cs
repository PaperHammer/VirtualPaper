using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    partial class FillTool(InkCanvasData data) : RenderBase {
        public override async void HandlePressed(CanvasPointerEventArgs e) {
            if (e.PointerPos != PointerPosition.InsideCanvas || !IsCanvasReady) return;

            PointerPoint pointerPoint = e.Pointer;
            Color fillColor = pointerPoint.Properties.IsRightButtonPressed
                ? data.BackgroundColor
                : data.ForegroundColor;
            Point startPoint = pointerPoint.Position;
            LayerInfo targetLayer = data.SelectedLayer;
            CanvasRenderTarget target = e.RenderTarget;
            Guid layerId = e.LayerId;
            if (targetLayer.Tag != layerId ||
                targetLayer.IsDeleted ||
                !ReferenceEquals(targetLayer.RenderData.RenderTarget, target)) return;

            var (requestVersion, requestCts) = BeginFillRequest();

            try {
                FillResult? result = await Task.Run(
                    () => CreateFillResult(startPoint, fillColor, target, requestCts.Token),
                    requestCts.Token);
                requestCts.Token.ThrowIfCancellationRequested();
                if (result == null ||
                    !IsFillRequestCurrent(requestVersion, requestCts, targetLayer, target)) return;

                int x = (int)result.DirtyRegion.X;
                int y = (int)result.DirtyRegion.Y;
                int width = (int)result.DirtyRegion.Width;
                int height = (int)result.DirtyRegion.Height;

                // Win2D 写回和界面刷新留在捕获的UI上下文；读取、扫描和压缩均已在后台完成。
                target.SetPixelBytes(result.CurrentPixels, x, y, width, height);

                var fillCommand = new RegionPixelSnapshotCommand(
                    layerId: layerId,
                    canvasData: data,
                    dirtyRegion: result.DirtyRegion,
                    originalPixels: result.CompressedOriginalPixels,
                    currentPixels: result.CompressedCurrentPixels,
                    isCompressed: true,
                    description: "Fill",
                    requestRenderAction: rect =>
                        HandleRender(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, rect)));
                ViewModel.Session.UnReUtil.RecordCommand(fillCommand);
                HandleRender(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, result.DirtyRegion));
                targetLayer.RenderData.HandleOnceRenderCompleted();
            }
            catch (OperationCanceledException) when (requestCts.IsCancellationRequested) {
                // 新填充或画布释放已经替代本次操作。
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
            catch (ObjectDisposedException) {
                // 图层或画布已经释放，后台结果直接失效。
            }
            catch (Exception ex) {
                ReportFatalError(ex);
            }
            finally {
                CompleteFillRequest(requestCts);
            }
        }

        /// <summary>
        /// 在后台线程完成GPU像素读取、扫描线填充、脏区域提取和撤销数据压缩。
        /// </summary>
        private static FillResult? CreateFillResult(
            Point startPoint,
            Color fillColor,
            CanvasRenderTarget target,
            CancellationToken token) {
            token.ThrowIfCancellationRequested();
            int width = (int)target.SizeInPixels.Width;
            int height = (int)target.SizeInPixels.Height;
            int startX = (int)Math.Clamp(startPoint.X, 0, width - 1);
            int startY = (int)Math.Clamp(startPoint.Y, 0, height - 1);

            byte[] fullPixels = target.GetPixelBytes();
            token.ThrowIfCancellationRequested();
            Span<uint> pixels32 = MemoryMarshal.Cast<byte, uint>(fullPixels.AsSpan());

            uint targetColor = pixels32[startY * width + startX];
            uint fillColor32 = ColorToBgra32(fillColor);
            if (targetColor == fillColor32) return null;

            var filledSpans = new List<FillSpan>();
            ScanlineFill(
                pixels32,
                width,
                height,
                startX,
                startY,
                targetColor,
                fillColor32,
                token,
                filledSpans,
                out int minX,
                out int minY,
                out int dirtyWidth,
                out int dirtyHeight);
            token.ThrowIfCancellationRequested();

            byte[] currentDirtyPixels = ExtractModifiedPixels(
                fullPixels,
                width,
                minX,
                minY,
                dirtyWidth,
                dirtyHeight,
                token);
            byte[] originalDirtyPixels = RestoreOriginalDirtyPixels(
                currentDirtyPixels,
                dirtyWidth,
                minX,
                minY,
                targetColor,
                filledSpans,
                token);
            var dirtyRect = new Rect(minX, minY, dirtyWidth, dirtyHeight);

            return new FillResult(
                dirtyRect,
                currentDirtyPixels,
                CompressPixels(originalDirtyPixels, token),
                CompressPixels(currentDirtyPixels, token));
        }

        private static void ScanlineFill(
            Span<uint> pixels32,
            int width,
            int height,
            int startX,
            int startY,
            uint targetColor,
            uint fillColor32,
            CancellationToken token,
            List<FillSpan> filledSpans,
            out int minX,
            out int minY,
            out int dirtyWidth,
            out int dirtyHeight) {
            int maxX = startX;
            int maxY = startY;
            minX = startX;
            minY = startY;

            var stack = new Stack<(int X, int Y)>(10000);
            stack.Push((startX, startY));

            while (stack.Count > 0) {
                token.ThrowIfCancellationRequested();
                var (cx, cy) = stack.Pop();
                int x = cx;

                while (x >= 0 && pixels32[cy * width + x] == targetColor) {
                    if ((x & CancellationCheckMask) == 0)
                        token.ThrowIfCancellationRequested();
                    x--;
                }
                x++;

                bool scanAbove = false;
                bool scanBelow = false;
                int spanStart = x;
                while (x < width && pixels32[cy * width + x] == targetColor) {
                    if ((x & CancellationCheckMask) == 0)
                        token.ThrowIfCancellationRequested();
                    pixels32[cy * width + x] = fillColor32;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (cy < minY) minY = cy;
                    if (cy > maxY) maxY = cy;

                    if (cy > 0) {
                        bool isTarget = pixels32[(cy - 1) * width + x] == targetColor;
                        if (!scanAbove && isTarget) {
                            stack.Push((x, cy - 1));
                            scanAbove = true;
                        }
                        else if (scanAbove && !isTarget) {
                            scanAbove = false;
                        }
                    }

                    if (cy < height - 1) {
                        bool isTarget = pixels32[(cy + 1) * width + x] == targetColor;
                        if (!scanBelow && isTarget) {
                            stack.Push((x, cy + 1));
                            scanBelow = true;
                        }
                        else if (scanBelow && !isTarget) {
                            scanBelow = false;
                        }
                    }
                    x++;
                }
                if (x > spanStart)
                    filledSpans.Add(new FillSpan(cy, spanStart, x - 1));
            }

            dirtyWidth = maxX - minX + 1;
            dirtyHeight = maxY - minY + 1;
        }

        private static byte[] ExtractModifiedPixels(
            byte[] fullPixels,
            int fullWidth,
            int dirtyX,
            int dirtyY,
            int dirtyWidth,
            int dirtyHeight,
            CancellationToken token) {
            byte[] currentDirtyPixels = new byte[dirtyWidth * dirtyHeight * 4];
            int rowBytes = dirtyWidth * 4;

            for (int row = 0; row < dirtyHeight; row++) {
                token.ThrowIfCancellationRequested();
                int srcOffset = ((dirtyY + row) * fullWidth + dirtyX) * 4;
                int dstOffset = row * rowBytes;
                Buffer.BlockCopy(fullPixels, srcOffset, currentDirtyPixels, dstOffset, rowBytes);
            }

            return currentDirtyPixels;
        }

        private static byte[] RestoreOriginalDirtyPixels(
            byte[] currentDirtyPixels,
            int dirtyWidth,
            int dirtyX,
            int dirtyY,
            uint targetColor,
            IReadOnlyList<FillSpan> filledSpans,
            CancellationToken token) {
            byte[] originalDirtyPixels = (byte[])currentDirtyPixels.Clone();
            Span<uint> original32 = MemoryMarshal.Cast<byte, uint>(originalDirtyPixels.AsSpan());

            foreach (FillSpan span in filledSpans) {
                token.ThrowIfCancellationRequested();
                int rowOffset = (span.Y - dirtyY) * dirtyWidth;
                int localStart = span.StartX - dirtyX;
                int localEnd = span.EndX - dirtyX;
                original32.Slice(rowOffset + localStart, localEnd - localStart + 1).Fill(targetColor);
            }

            return originalDirtyPixels;
        }

        private static byte[] CompressPixels(byte[] pixels, CancellationToken token) {
            if (pixels.Length == 0) return pixels;

            using var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, true)) {
                for (int offset = 0; offset < pixels.Length; offset += CompressionChunkSize) {
                    token.ThrowIfCancellationRequested();
                    int count = Math.Min(CompressionChunkSize, pixels.Length - offset);
                    deflate.Write(pixels, offset, count);
                }
            }
            token.ThrowIfCancellationRequested();
            return output.ToArray();
        }

        private static uint ColorToBgra32(Color color) {
            return (uint)(color.B | (color.G << 8) | (color.R << 16) | (color.A << 24));
        }

        private (long Version, CancellationTokenSource Cts) BeginFillRequest() {
            CancellationTokenSource? previousCts;
            CancellationTokenSource requestCts = new();
            long requestVersion;
            lock (_fillRequestLock) {
                previousCts = _fillRequestCts;
                _fillRequestCts = requestCts;
                requestVersion = Interlocked.Increment(ref _fillRequestVersion);
            }
            CancelSafely(previousCts);
            return (requestVersion, requestCts);
        }

        private bool IsFillRequestCurrent(
            long requestVersion,
            CancellationTokenSource requestCts,
            LayerInfo targetLayer,
            CanvasRenderTarget target) {
            lock (_fillRequestLock) {
                return requestVersion == _fillRequestVersion &&
                    ReferenceEquals(_fillRequestCts, requestCts) &&
                    !requestCts.IsCancellationRequested &&
                    !targetLayer.IsDeleted &&
                    data.Layers.Contains(targetLayer) &&
                    ReferenceEquals(targetLayer.RenderData.RenderTarget, target);
            }
        }

        private void CompleteFillRequest(CancellationTokenSource requestCts) {
            lock (_fillRequestLock) {
                if (ReferenceEquals(_fillRequestCts, requestCts))
                    _fillRequestCts = null;
            }
            requestCts.Dispose();
        }

        internal override void CancelPendingOperation() {
            CancellationTokenSource? requestCts;
            lock (_fillRequestLock) {
                Interlocked.Increment(ref _fillRequestVersion);
                requestCts = _fillRequestCts;
                _fillRequestCts = null;
            }
            CancelSafely(requestCts);
        }

        public override void Dispose() {
            CancelPendingOperation();
            base.Dispose();
        }

        private static void CancelSafely(CancellationTokenSource? cts) {
            try {
                cts?.Cancel();
            }
            catch (ObjectDisposedException) {
                // 已完成的请求可能正在释放自己的令牌源。
            }
        }

        private sealed record FillResult(
            Rect DirtyRegion,
            byte[] CurrentPixels,
            byte[] CompressedOriginalPixels,
            byte[] CompressedCurrentPixels);

        private readonly record struct FillSpan(int Y, int StartX, int EndX);

        private readonly object _fillRequestLock = new();
        private CancellationTokenSource? _fillRequestCts;
        private long _fillRequestVersion;
        private const int CancellationCheckMask = 4095;
        private const int CompressionChunkSize = 1024 * 1024;
    }
}
