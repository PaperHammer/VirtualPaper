using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common.Extensions;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Models.ToolItems.Utils {
    public sealed partial class DirtyRegionSnapshot : IDisposable {
        public Guid LayerId { get; }
        public Rect DirtyRegion { get; private set; }

        public DirtyRegionSnapshot(Guid layerId, CanvasRenderTarget source, Rect dirtyRegion) {
            LayerId = layerId;
            _sourceTarget = source;
            DirtyRegion = dirtyRegion;

            CaptureOriginalPixels();
        }

        /// <summary>
        /// 捕获原始像素数据
        /// </summary>
        private void CaptureOriginalPixels() {
            int pixelCount = (int)(DirtyRegion.Width * DirtyRegion.Height);
            _originalPixels = new byte[pixelCount * 4]; // RGBA
            _sourceTarget.GetPixelBytes(
                _originalPixels.AsBuffer(),
                (int)DirtyRegion.Left,
                (int)DirtyRegion.Top,
                (int)DirtyRegion.Width,
                (int)DirtyRegion.Height
            );
        }

        /// <summary>
        /// 更新脏区域并捕获当前状态
        /// </summary>
        public void UpdateDirtyRegion(Rect newDirtyRegion) {
            DirtyRegion = DirtyRegion.IsEmpty ?
                newDirtyRegion :
                RectExtensions.UnionRect(DirtyRegion, newDirtyRegion);

            int pixelCount = (int)(DirtyRegion.Width * DirtyRegion.Height);
            _currentPixels = new byte[pixelCount * 4];
            _sourceTarget.GetPixelBytes(
                _currentPixels.AsBuffer(),
                (int)DirtyRegion.Left,
                (int)DirtyRegion.Top,
                (int)DirtyRegion.Width,
                (int)DirtyRegion.Height
            );
        }

        /// <summary>
        /// 恢复到原始状态
        /// </summary>
        public void RestoreOriginal() {
            _sourceTarget.SetPixelBytes(
                _originalPixels.AsBuffer(),
                (int)DirtyRegion.Left,
                (int)DirtyRegion.Top,
                (int)DirtyRegion.Width,
                (int)DirtyRegion.Height
            );
        }

        /// <summary>
        /// 应用当前修改
        /// </summary>
        public void ApplyCurrent() {
            _sourceTarget.SetPixelBytes(
                _currentPixels.AsBuffer(),
                (int)DirtyRegion.Left,
                (int)DirtyRegion.Top,
                (int)DirtyRegion.Width,
                (int)DirtyRegion.Height
            );
        }

        public void Dispose() {
            _originalPixels = [];
            _currentPixels = [];
        }

        private byte[] _originalPixels = [];
        private byte[] _currentPixels = [];
        private readonly CanvasRenderTarget _sourceTarget;
    }
}
