using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using VirtualPaper.Common.Extensions;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    partial class FillTool(InkCanvasData data) : RenderBase {
        public override void HandlePressed(CanvasPointerEventArgs e) {
            if (e.PointerPos != PointerPosition.InsideCanvas) return;

            PointerPoint pointerPoint = e.Pointer;
            _blendedColor = pointerPoint.Properties.IsRightButtonPressed ? data.BackgroundColor : data.ForegroundColor;
            _lastClickPoint = pointerPoint.Position;
            RenderToTarget();
        }

        // 核心绘制逻辑
        private void RenderToTarget() {
            try {
                if (RenderTarget == null) {
                    return;
                }

                var fillCommand = OptimizedScanlineFill(_lastClickPoint, _blendedColor, RenderTarget);
                if (fillCommand != null) {
                    fillCommand.ExecuteAsync().Wait();
                    ViewModel.Session.UnReUtil.RecordCommand(fillCommand);
                }
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
        }

        /// <summary>
        /// Span<uint> 内存直读 + 扫描线算法 + 脏区域截取
        /// </summary>
        private RegionPixelSnapshotCommand? OptimizedScanlineFill(Point startPoint, Color fillColor, CanvasRenderTarget target) {
            int width = (int)target.SizeInPixels.Width;
            int height = (int)target.SizeInPixels.Height;
            int startX = (int)Math.Clamp(startPoint.X, 0, width - 1);
            int startY = (int)Math.Clamp(startPoint.Y, 0, height - 1);

            byte[]? fullPixels = target.GetPixelBytes();
            Span<uint> pixels32 = MemoryMarshal.Cast<byte, uint>(fullPixels.AsSpan());

            uint targetColor = pixels32[startY * width + startX];
            uint fillColor32 = ColorToBgra32(fillColor);

            if (targetColor == fillColor32) {
                fullPixels = null;
                return null;
            }

            // 执行核心扫描线算法，原地修改 Span，并输出脏区域边界
            ScanlineFill(
                pixels32, width, height, startX, startY,
                targetColor, fillColor32,
                out int minX, out int minY, out int dirtyWidth, out int dirtyHeight);

            byte[] originalDirtyPixels = target.GetPixelBytes(minX, minY, dirtyWidth, dirtyHeight).CompressPixels();
            byte[] currentDirtyPixels = ExtractModifiedPixels(fullPixels, width, minX, minY, dirtyWidth, dirtyHeight).CompressPixels();
            var dirtyRect = new Rect(minX, minY, dirtyWidth, dirtyHeight);

            pixels32 = null;
            fullPixels = null;

            return new RegionPixelSnapshotCommand(
                layerId: LayerId,
                canvasData: data,
                dirtyRegion: dirtyRect,
                originalPixels: originalDirtyPixels,
                currentPixels: currentDirtyPixels,
                isCompressed: true,
                description: "Fill",
                requestRenderAction: (rect) => HandleRender(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, dirtyRect))
            );
        }

        /// <summary>
        /// 扫描线填充
        /// </summary>
        private void ScanlineFill(
            Span<uint> pixels32, int width, int height,
            int startX, int startY, uint targetColor, uint fillColor32,
            out int minX, out int minY, out int dirtyWidth, out int dirtyHeight) {
            // 初始化边界
            int maxX = startX, maxY = startY;
            minX = startX; minY = startY;

            // 扫描线栈
            Stack<(int x, int y)> stack = new Stack<(int, int)>(10000);
            stack.Push((startX, startY));

            while (stack.Count > 0) {
                var (cx, cy) = stack.Pop();
                int x = cx;

                // 向左寻找边界
                while (x >= 0 && pixels32[cy * width + x] == targetColor) {
                    x--;
                }
                x++; // 回退到有效起始点

                bool scanAbove = false;
                bool scanBelow = false;

                // 向右填充并扫描上下相邻行
                while (x < width && pixels32[cy * width + x] == targetColor) {
                    pixels32[cy * width + x] = fillColor32; // 原地修改

                    // 动态扩大脏区域边界
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (cy < minY) minY = cy;
                    if (cy > maxY) maxY = cy;

                    // 检查上方行
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

                    // 检查下方行
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
            }

            // 计算精准的包围盒尺寸
            dirtyWidth = maxX - minX + 1;
            dirtyHeight = maxY - minY + 1;
        }

        /// <summary>
        /// 返回局部修改后的像素块
        /// </summary>
        private byte[] ExtractModifiedPixels(byte[] fullPixels, int fullWidth, int dirtyX, int dirtyY, int dirtyWidth, int dirtyHeight) {
            byte[] currentDirtyPixels = new byte[dirtyWidth * dirtyHeight * 4];
            int rowBytes = dirtyWidth * 4;

            for (int row = 0; row < dirtyHeight; row++) {
                // 全量数组中的起点：(当前行 y 坐标 * 全图宽度 + x 坐标) * 4字节
                int srcOffset = ((dirtyY + row) * fullWidth + dirtyX) * 4;
                // 小数组中的起点：当前行 * 小数组宽度字节
                int dstOffset = row * rowBytes;

                Buffer.BlockCopy(fullPixels, srcOffset, currentDirtyPixels, dstOffset, rowBytes);
            }

            return currentDirtyPixels;
        }

        // 辅助方法：将 Color 转换为 Bgra8 格式的 uint
        private uint ColorToBgra32(Color color) {
            return (uint)(color.B | (color.G << 8) | (color.R << 16) | (color.A << 24));
        }

        private Color _blendedColor;
        private Point _lastClickPoint;
    }
}
