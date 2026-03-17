using System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Microsoft.UI.Input;
using VirtualPaper.Common.Utils.UndoRedo;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Events;

namespace Workloads.Creation.StaticImg.Core.Rendering {
    /// <summary>
    /// 2D 画布区域选择器基类
    /// </summary>
    public abstract class CanvasAreaSelector : RenderBase {
        public event EventHandler<Rect>? OnSelectRectChanged;

        protected CanvasRenderTarget? BaseContent { get; private set; }
        protected CanvasRenderTarget? SelectionContent { get; private set; }
        public Rect SelectionRect => _selectionRect;
        public SelectionState CurrentState => _currentState;

        protected abstract IUndoableCommand? BuildUndoCommand();

        public override void HandlePressed(CanvasPointerEventArgs e) {
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

        public override void HandleMoved(CanvasPointerEventArgs e) {
            var position = e.Pointer.Position;
            if (_isDragging || (e.PointerPos == PointerPosition.InsideCanvas && _selectionRect.Contains(position))) {
                RequestCursorChange(InputSystemCursor.Create(InputSystemCursorShape.SizeAll));
            }
            else {
                RequestCursorChange(InputSystemCursor.Create(InputSystemCursorShape.Cross));
            }

            if (!e.Pointer.Properties.IsLeftButtonPressed || _currentState != SelectionState.Selecting) return;

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

        public override void HandleReleased(CanvasPointerEventArgs e) {
            EndSelection();
        }

        public override void HandleExited(CanvasPointerEventArgs e) {
            if (e.PointerPos == PointerPosition.InsideCanvas ||
                e.PointerPos == PointerPosition.InsideContainer) return;
            base.HandleExited(e);
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

        public virtual bool RestoreOriginalContent() {
            if (SelectionRect.IsEmpty || SelectionContent == null) return false;

            // 恢复原位置内容
            using (var ds = BaseContent!.CreateDrawingSession()) {
                ds.Blend = CanvasBlend.Copy; // 覆盖模式
                ds.DrawImage(SelectionContent,
                    (float)_originalSelectionRect.X,
                    (float)_originalSelectionRect.Y);
            }
            Reset();
            RenderToTarget();
            //BaseContent?.Dispose();
            //BaseContent = null;

            return true;
        }

        protected void Reset() {
            SelectionContent?.Dispose();
            SelectionContent = null;
            _currentState = SelectionState.None;
            _originalSelectionRect = Rect.Empty;
            _isDragging = false;
            UpdateSelectionRect(Rect.Empty);
        }

        private void StartNewSelection(Point position) {
            SaveBaseContent();
            _startPoint = position;
            UpdateSelectionRect(new Rect(position, new Size(0, 0)));
            _originalSelectionRect = Rect.Empty;
            _currentState = SelectionState.Selecting;
            _isDragging = false;
        }

        private void StartDragSelection(Point position) {
            _currentDragStartRect = _selectionRect; // 记录当前拖动开始时的位置
            _moveStartPoint = position;
            _currentState = SelectionState.Selecting;
            _isDragging = true;
        }

        protected void SaveBaseContent() {
            if (RenderTarget == null) return;

            //BaseContent?.Dispose();
            BaseContent = new CanvasRenderTarget(
                RenderTarget,
                RenderTarget.SizeInPixels.Width,
                RenderTarget.SizeInPixels.Height,
                RenderTarget.Dpi,
                RenderTarget.Format,
                RenderTarget.AlphaMode);

            using (var ds = BaseContent.CreateDrawingSession()) {
                ds.Clear(Colors.Transparent);
                ds.DrawImage(RenderTarget);
            }
        }

        protected virtual void CaptureSelectionContent() {
            if (_selectionRect.IsEmpty) return;

            // 更新选区矩形为整数坐标，避免还原后残留虚影
            double x = Math.Floor(_selectionRect.X);
            double y = Math.Floor(_selectionRect.Y);
            double w = Math.Ceiling(_selectionRect.Width);
            double h = Math.Ceiling(_selectionRect.Height);
            _selectionRect = new Rect(x, y, w, h);

            //SelectionContent?.Dispose();
            SelectionContent ??= new CanvasRenderTarget(
                RenderTarget,
                (float)_selectionRect.Width,
                (float)_selectionRect.Height,
                RenderTarget.Dpi,
                RenderTarget.Format,
                RenderTarget.AlphaMode);

            _originalSelectionRect = _selectionRect;

            //捕获选区内容
            using (var ds = SelectionContent.CreateDrawingSession()) {
                ds.Blend = CanvasBlend.Copy;
                ds.DrawImage(BaseContent, SelectionContent.Bounds, _selectionRect);
            }

            //剪切原位置
            using (var ds = BaseContent!.CreateDrawingSession()) {
                ds.Blend = CanvasBlend.Copy;
                ds.FillRectangle(_selectionRect, Colors.Transparent);
            }
        }

        public virtual IUndoableCommand? CommitSelection() {
            if (_currentState != SelectionState.Selected || SelectionContent == null) return null;

            var command = BuildUndoCommand();
            if (command != null) {
                ViewModel.Session.UnReUtil.RecordCommand(command);

                Reset();
                RenderToTarget();
                //BaseContent?.Dispose();
                //BaseContent = null;
                base.RequestOnceRender();
            }

            return command;
        }

        protected virtual void RenderToTarget() {
            if (RenderTarget == null) return;

            try {
                using (var ds = RenderTarget.CreateDrawingSession()) {
                    ds.Blend = CanvasBlend.Copy; // 覆盖模式

                    // 绘制基准内容
                    if (BaseContent != null) {
                        ds.DrawImage(BaseContent);
                    }

                    // 绘制选区内容（自动裁剪到画布边界）
                    if (SelectionContent != null && _currentState != SelectionState.None) {
                        ds.DrawImage(SelectionContent, (float)_selectionRect.X, (float)_selectionRect.Y);
                    }

                    // 绘制完整的选择框（包括延伸到画布外的部分）
                    if (_currentState != SelectionState.None) {
                        DrawFullSelectionBorder(ds);
                    }
                }

                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
            catch (ObjectDisposedException) {
                // 处于多线程资源释放的间隙，直接忽略，防止崩溃
                Reset();
            }
            catch (Exception ex) {
                ReportFatalError(ex);
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

        protected new void HandleDeviceLost() {
            base.HandleDeviceLost();
            BaseContent?.Dispose();
            BaseContent = null;
            SelectionContent?.Dispose();
            SelectionContent = null;
        }

        protected void UpdateSelectionRect(Rect rect) {
            _selectionRect = rect;
            OnSelectRectChanged?.Invoke(this, rect);
        }

        #region dispose
        private bool _disposed = false;
        public override void Dispose() {
            Dispose(true);
            base.Dispose();
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
            SafeDispose(BaseContent);
            SafeDispose(SelectionContent);
            // RenderTarget由外部管理，此处不释放
        }

        protected static void SafeDispose<T>(T? resource) where T : IDisposable {
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
