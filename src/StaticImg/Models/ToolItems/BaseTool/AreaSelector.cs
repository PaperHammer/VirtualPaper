using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas;
using System;
using Workloads.Creation.StaticImg.Models.EventArg;
using Windows.Foundation;
using Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Input;
using System.Diagnostics;

namespace Workloads.Creation.StaticImg.Models.ToolItems.BaseTool {
    internal abstract class AreaSelector(InkCanvasConfigData data) : Tool {
        public Rect SelectionRect => _selectionRect;
        protected override bool HandlesPointerOutsideContentArea => true;
        public SelectionState CurrentState => _currentState;

        public override void OnPointerPressed(CanvasPointerEventArgs e) {
            var position = e.Pointer.Position;
            switch (_currentState) {
                case SelectionState.None:
                    if (e.PointerPos != PointerPosition.InsideCanvas || 
                        !e.Pointer.Properties.IsLeftButtonPressed) return;
                    StartNewSelection(position);
                    break;

                case SelectionState.Selected:
                    if (e.Pointer.Properties.IsRightButtonPressed) {
                        RestoreOriginalContent();
                    }
                    // 当且仅当在可视区域可以拖动选区
                    else if (e.PointerPos == PointerPosition.InsideCanvas && _selectionRect.Contains(position)) {
                        StartDragSelection(position);
                    }
                    else {
                        CommitSelection();
                    }
                    break;
            }
        }

        public override void OnPointerMoved(CanvasPointerEventArgs e) {
            var position = e.Pointer.Position;
            if (_isDragging || (e.PointerPos == PointerPosition.InsideCanvas && _selectionRect.Contains(position))) {
                ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.SizeAll));
            }
            else {
                ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.Cross));
            }

            if (!e.Pointer.Properties.IsLeftButtonPressed ||
                _currentState != SelectionState.Selecting) return;

            var currentPos = new Point(
                Math.Min(RenderTarget.SizeInPixels.Width, Math.Max(0, e.Pointer.Position.X)),
                Math.Min(RenderTarget.SizeInPixels.Height, Math.Max(0, e.Pointer.Position.Y)));

            if (_isDragging) {
                double offsetX = currentPos.X - _moveStartPoint.X;
                double offsetY = currentPos.Y - _moveStartPoint.Y;

                Rect newRect = new(
                    _currentDragStartRect.X + offsetX,
                    _currentDragStartRect.Y + offsetY,
                    _currentDragStartRect.Width,
                    _currentDragStartRect.Height);

                // 计算强制保留的最小可见比例                
                double minVisibleWidth = newRect.Width * MIN_VISIBLE_RATIO;
                double minVisibleHeight = newRect.Height * MIN_VISIBLE_RATIO;

                // 调整位置确保至少保留最小可见部分
                if (newRect.Right < minVisibleWidth)
                    offsetX += minVisibleWidth - newRect.Right;
                if (newRect.Bottom < minVisibleHeight)
                    offsetY += minVisibleHeight - newRect.Bottom;
                if (newRect.Left > Viewport.Width - minVisibleWidth)
                    offsetX -= newRect.Left - (Viewport.Width - minVisibleWidth);
                if (newRect.Top > Viewport.Height - minVisibleHeight)
                    offsetY -= newRect.Top - (Viewport.Height - minVisibleHeight);

                Rect adjustedRect = new(
                    _currentDragStartRect.X + offsetX,
                    _currentDragStartRect.Y + offsetY,
                    _currentDragStartRect.Width,
                    _currentDragStartRect.Height);

                UpdateSelectionRect(adjustedRect);
            }
            else {
                UpdateSelectionRect(new Rect(
                    Math.Min(_startPoint.X, currentPos.X),
                    Math.Min(_startPoint.Y, currentPos.Y),
                    Math.Abs(currentPos.X - _startPoint.X),
                    Math.Abs(currentPos.Y - _startPoint.Y)));
            }

            RenderToTarget();
        }

        public override void OnPointerReleased(CanvasPointerEventArgs e) {
            EndSelection();
        }

        public override void OnPointerExited(CanvasPointerEventArgs e) {
            if (e.PointerPos == PointerPosition.InsideCanvas || 
                e.PointerPos == PointerPosition.InsideContainer) return;
            base.OnPointerExited(e);
            EndSelection();
        }

        private void EndSelection() {
            if (_currentState == SelectionState.Selecting) {
                if (_isDragging) {
                    // 完成拖动
                    _isDragging = false;
                    _currentState = SelectionState.Selected;
                }
                else if (_selectionRect.Width > 5 && _selectionRect.Height > 5) {
                    // 完成新选区创建
                    _currentState = SelectionState.Selected;
                    CaptureSelectionContent();
                }
                else {
                    // 无效选区
                    CaptureSelectionContent();
                    RestoreOriginalContent();
                    UpdateSelectionRect(Rect.Empty);
                }
            }
        }

        public bool RestoreOriginalContent() {
            if (_baseContent == null || _selectionContent == null) return false;

            // 恢复原位置内容
            using (var ds = _baseContent.CreateDrawingSession()) {
                // 先清除当前选区位置（防止重叠）
                ds.FillRectangle(_selectionRect, Colors.Transparent);

                // 恢复到原始位置
                ds.DrawImage(_selectionContent,
                    (float)_originalSelectionRect.X,
                    (float)_originalSelectionRect.Y);
            }

            // 重置状态
            StopSelection();
            RenderToTarget();

            return true;
        }

        private void StopSelection() {
            _selectionContent?.Dispose();
            _selectionContent = null;
            _currentState = SelectionState.None;
            UpdateSelectionRect(Rect.Empty);
        }

        private void StartNewSelection(Point position) {
            SaveBaseContent();
            _currentState = SelectionState.Selecting;
            _startPoint = position;
            UpdateSelectionRect(new Rect(position, new Size(0, 0)));
            _isDragging = false;
            _originalSelectionRect = Rect.Empty;
        }

        private void StartDragSelection(Point position) {
            _currentState = SelectionState.Selecting;
            _moveStartPoint = position;
            _isDragging = true;
            _currentDragStartRect = _selectionRect; // 记录当前拖动开始时的位置
        }

        protected void SaveBaseContent() {
            _baseContent ??= new CanvasRenderTarget(
                RenderTarget,
                RenderTarget.SizeInPixels.Width,
                RenderTarget.SizeInPixels.Height,
                RenderTarget.Dpi);

            using (var ds = _baseContent.CreateDrawingSession()) {
                ds.Clear(Colors.Transparent);
                ds.DrawImage(RenderTarget);
            }
        }

        protected void CaptureSelectionContent() {
            if (_selectionRect.IsEmpty) return;

            // 保存原始位置（在首次捕获时记录）
            if (!_isDragging) {
                _originalSelectionRect = _selectionRect;
            }

            //_selectionContent?.Dispose();
            _selectionContent ??= new CanvasRenderTarget(
                RenderTarget,
                (float)_selectionRect.Width,
                (float)_selectionRect.Height,
                RenderTarget.Dpi);

            //捕获选区内容
            using (var ds = _selectionContent.CreateDrawingSession()) {
                ds.Blend = CanvasBlend.Copy;
                ds.Clear(Colors.Transparent);
                ds.DrawImage(_baseContent,
                    new Rect(0, 0, _selectionRect.Width, _selectionRect.Height),
                    _selectionRect);
            }

            //剪切原位置
            using (var ds = _baseContent.CreateDrawingSession()) {
                ds.Blend = CanvasBlend.Copy;
                ds.FillRectangle(_selectionRect, Colors.Transparent);
            }
        }

        public virtual bool CommitSelection() {
            if (_currentState != SelectionState.Selected || _selectionContent == null) return false;

            // 将选区内容合并到基础层
            using (var ds = _baseContent.CreateDrawingSession()) {
                ds.DrawImage(_selectionContent, (float)_selectionRect.X, (float)_selectionRect.Y);
            }
            StopSelection();
            RenderToTarget();

            return true;
        }

        protected virtual void RenderToTarget() {
            try {
                if (RenderTarget == null) return;

                using (var ds = RenderTarget.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);

                    // 绘制基准内容
                    if (_baseContent != null) {
                        ds.DrawImage(_baseContent);
                    }

                    // 绘制选区内容（自动裁剪到画布边界）
                    if (_selectionContent != null && _currentState != SelectionState.None) {
                        DrawSelectionContentWithClipping(ds);
                    }

                    // 绘制完整的选择框（包括延伸到画布外的部分）
                    if (_currentState != SelectionState.None) {
                        DrawFullSelectionBorder(ds);
                    }
                }

                Render();
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
        }

        private void DrawSelectionContentWithClipping(CanvasDrawingSession ds) {
            // 只绘制画布内的选区内容部分
            float srcX = (float)Math.Max(0, -_selectionRect.X);
            float srcY = (float)Math.Max(0, -_selectionRect.Y);
            float destX = (float)Math.Max(0, _selectionRect.X);
            float destY = (float)Math.Max(0, _selectionRect.Y);

            float drawWidth = (float)Math.Min(
                _selectionContent.Size.Width - srcX,
                RenderTarget.SizeInPixels.Width - destX);

            float drawHeight = (float)Math.Min(
                _selectionContent.Size.Height - srcY,
                RenderTarget.SizeInPixels.Height - destY);

            if (drawWidth > 0 && drawHeight > 0) {
                var sourceRect = new Rect(srcX, srcY, drawWidth, drawHeight);
                var destRect = new Rect(destX, destY, drawWidth, drawHeight);
                ds.DrawImage(_selectionContent, destRect, sourceRect);
            }
        }

        private void DrawFullSelectionBorder(CanvasDrawingSession ds) {
            using (var borderBrush = new CanvasSolidColorBrush(RenderTarget, _selectionBorderColor)) {
                // 直接绘制完整的选择框，不进行边界裁剪
                ds.DrawRectangle(_selectionRect, borderBrush, _selectionBorderWidth, _borderStrokeStyle);

                // 可视区域边界指示
                DrawViewportIndicator(ds);
            }
        }

        private void DrawViewportIndicator(CanvasDrawingSession ds) {
            using (var viewportBrush = new CanvasSolidColorBrush(RenderTarget, Color.FromArgb(255, 80, 80, 80))) {
                var viewport = new Rect(
                    0, 0,
                    RenderTarget.SizeInPixels.Width,
                    RenderTarget.SizeInPixels.Height);

                ds.DrawRectangle(viewport, viewportBrush, 2f);
            }
        }

        protected void HandleDeviceLost() {
            _baseContent?.Dispose();
            _baseContent = null;
            _selectionContent?.Dispose();
            _selectionContent = null;
            RenderTarget?.Dispose();
            RenderTarget = null;
        }

        protected void UpdateSelectionRect(Rect rect) {
            _selectionRect = rect;
            data.SelectionRect = rect;
        }

        #region dispose
        private bool _disposed = false;
        public override void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
                // 释放托管资源
                ReleaseAllResources();
            }

            _disposed = true;
        }

        private void ReleaseAllResources() {
            SafeDispose(ref _baseContent);
            SafeDispose(ref _selectionContent);
            // RenderTarget由外部管理，此处不释放
        }

        protected static void SafeDispose<T>(ref T resource) where T : IDisposable {
            try {
                resource?.Dispose();
                resource = default;
            }
            catch { }
        }
        #endregion

        public enum SelectionState {
            None, // 无选择
            Selecting, // 正在选择/拖动区域
            Selected, // 已选择区域
        }

        protected SelectionState _currentState;
        protected Point _startPoint;
        protected Rect _selectionRect;
        protected Point _moveStartPoint;
        protected Rect _originalSelectionRect; // 基准层的选区位置（用于还原）
        protected Rect _currentDragStartRect; // 当前拖动开始时的选区位置
        protected bool _isDragging; // 标记当前是否在拖动

        // 图层缓存
        protected CanvasRenderTarget? _baseContent; // 基准层
        protected CanvasRenderTarget? _selectionContent;

        // 绘制样式
        protected readonly Color _selectionBorderColor = Colors.Black;
        protected readonly float _selectionBorderWidth = 5.0f;

        const float MIN_VISIBLE_RATIO = 0.2f;

        protected readonly CanvasStrokeStyle _selectionStrokeStyle = new() {
            DashStyle = CanvasDashStyle.Dash,
        };
        protected readonly CanvasStrokeStyle _borderStrokeStyle = new() {
            DashStyle = CanvasDashStyle.Dash,
            DashCap = CanvasCapStyle.Round
        };
    }
}
