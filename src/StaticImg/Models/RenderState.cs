using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Models {
    internal partial class RenderState : IDisposable {
        public event EventHandler RenderComposite;

        public bool NeedsFullRedraw { get; set; } = true;
        public List<Rect> DirtyRects { get; } = [];
        public CanvasRenderTarget LayerCache { get; private set; }

        public RenderState(CanvasRenderTarget rt) {
            InitializeCache(rt);
        }

        private void InitializeCache(CanvasRenderTarget rt) {
            LayerCache = new CanvasRenderTarget(
                rt.Device,
                (float)rt.Bounds.Width,
                (float)rt.Bounds.Height,
                rt.Dpi,
                rt.Format,
                rt.AlphaMode);
        }

        public void AddDirtyRect(Rect rect) {
            // 合并重叠或相邻的脏矩形
            if (rect.IsEmpty)
                return;

            // 合并重叠或相邻的脏矩形
            bool merged = false;
            for (int i = 0; i < DirtyRects.Count; i++) {
                if (AreRectsOverlapping(DirtyRects[i], rect)) {
                    // 合并两个矩形
                    double left = Math.Min(DirtyRects[i].Left, rect.Left);
                    double top = Math.Min(DirtyRects[i].Top, rect.Top);
                    double right = Math.Max(DirtyRects[i].Right, rect.Right);
                    double bottom = Math.Max(DirtyRects[i].Bottom, rect.Bottom);

                    DirtyRects[i] = new Rect(left, top, right - left, bottom - top);
                    merged = true;
                    break;
                }
            }

            if (!merged)
                DirtyRects.Add(rect);
        }

        private static bool AreRectsOverlapping(Rect a, Rect b) {
            // 添加1px的合并阈值，避免过多小矩形
            const double mergeThreshold = 1.0;

            return !(a.Left > b.Right + mergeThreshold ||
                     a.Right < b.Left - mergeThreshold ||
                     a.Top > b.Bottom + mergeThreshold ||
                     a.Bottom < b.Top - mergeThreshold);
        }

        public void Dispose() {
            LayerCache?.Dispose();
            GC.SuppressFinalize(this);            
        }

        internal Rect MergeDirtyRects() {
            if (DirtyRects.Count == 0) return Rect.Empty;

            double left = DirtyRects.Min(r => r.Left);
            double top = DirtyRects.Min(r => r.Top);
            double right = DirtyRects.Max(r => r.Right);
            double bottom = DirtyRects.Max(r => r.Bottom);

            return new Rect(left, top, right - left, bottom - top);
        }

        internal void Render() {
            RenderComposite?.Invoke(this, EventArgs.Empty);
        }
    }
}
