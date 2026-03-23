using Microsoft.Graphics.Canvas;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.UndoRedo;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    sealed partial class SelectionTool : CanvasAreaSelector {
        public SelectionTool(InkCanvasData data) {
            _data = data;
            OnSelectRectChanged += SelectionTool_OnSelectRectChanged;
        }

        private void SelectionTool_OnSelectRectChanged(object? sender, Windows.Foundation.Rect e) {
            _data.SelectionRect = e;
        }

        protected override IUndoableCommand? BuildUndoCommand() {
            lock (_data) {
                if (SelectionContent == null || BaseContent == null) return null;

                int w = (int)_originalSelectionRect.Width;
                int h = (int)_originalSelectionRect.Height;
                int ox = (int)_originalSelectionRect.X;
                int oy = (int)_originalSelectionRect.Y;
                int nx = (int)_selectionRect.X;
                int ny = (int)_selectionRect.Y;

                byte[] selectionPixels = SelectionContent.GetPixelBytes().CompressPixels();
                byte[] targetOriginalPixels = BaseContent.GetPixelBytes(nx, ny, w, h).CompressPixels();

                using (var ds = BaseContent!.CreateDrawingSession()) {
                    ds.Blend = CanvasBlend.Copy;
                    ds.DrawImage(SelectionContent, (float)_selectionRect.X, (float)_selectionRect.Y);
                }

                return new LayerSelectionCommand(
                    LayerId,
                    _data,
                    _originalSelectionRect,
                    _selectionRect,
                    selectionPixels,
                    targetOriginalPixels,
                    "Selection Move",
                    (region) => HandleRender(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, region))
                );
            }
        }

        private readonly InkCanvasData _data;
    }
}
