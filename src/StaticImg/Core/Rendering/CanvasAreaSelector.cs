using System;
using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Microsoft.UI.Input;
using VirtualPaper.Common.Utils.UndoRedo;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Events;

namespace Workloads.Creation.StaticImg.Core.Rendering {
    /// <summary>
    /// 2D 画布区域选择器基类
    /// </summary>
    public abstract class CanvasAreaSelector : RenderBase {
        public event EventHandler<Rect>? OnSelectRectChanged;

        protected CanvasRenderTarget? OriginalContentSnapshot => _resources.OriginalContentSnapshot;
        protected CanvasRenderTarget? SourceRenderTarget => _resources.SourceRenderTarget;
        protected CanvasRenderTarget? SelectedRegionSnapshot => _resources.SelectedRegionSnapshot;
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
                UpdateSelectionCursor(InputSystemCursorShape.SizeAll);
            }
            else {
                UpdateSelectionCursor(InputSystemCursorShape.Cross);
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
            _lastCursorShape = null;
            base.HandleExited(e);
            EndSelection();
        }

        private void UpdateSelectionCursor(InputSystemCursorShape shape) {
            if (_lastCursorShape == shape) return;

            _lastCursorShape = shape;
            InputCursor cursor = shape == InputSystemCursorShape.SizeAll
                ? _moveCursor ??= InputSystemCursor.Create(InputSystemCursorShape.SizeAll)
                : _crossCursor ??= InputSystemCursor.Create(InputSystemCursorShape.Cross);
            RequestCursorChange(cursor);
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
                    CaptureSelectedRegionSnapshot();
                }
                else {
                    // 无效选区
                    CaptureSelectedRegionSnapshot();
                    RestoreOriginalContent();
                    UpdateSelectionRect(Rect.Empty);
                }
            }
        }

        public virtual bool RestoreOriginalContent() {
            if (OriginalContentSnapshot == null || SourceRenderTarget == null) return false;

            CanvasRenderTarget originalSnapshot = OriginalContentSnapshot;
            CanvasRenderTarget target = SourceRenderTarget;
            bool isReset = false;
            try {
                // 撤销/重做裁剪会替换并释放各图层的 RenderTarget。
                // 旧目标已脱离图层时，丢弃旧预览即可，不能覆盖撤销后的新内容。
                if (!IsTargetStillAttached(target)) {
                    Reset();
                    isReset = true;
                    HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
                    return true;
                }

                // 恢复原位置内容
                if (SelectedRegionSnapshot != null && !_originalSelectionRect.IsEmpty) {
                    using var ds = originalSnapshot.CreateDrawingSession();
                    ds.Blend = CanvasBlend.Copy; // 覆盖模式
                    ds.DrawImage(
                        SelectedRegionSnapshot,
                        (float)_originalSelectionRect.X,
                        (float)_originalSelectionRect.Y);
                }

                Reset();
                isReset = true;
                RenderToTarget(target);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SEHException) {
                // 校验与创建 DrawingSession 之间仍可能发生资源替换。
                if (!isReset) {
                    Reset();
                    isReset = true;
                }
                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
            }
            finally {
                if (!isReset) Reset();
                // RenderToTarget 完成后，完整画布副本不再参与后续绘制。
                ReleaseOriginalContentSnapshot(originalSnapshot);
            }

            return true;
        }

        protected void Reset() {
            ReleaseSelectedRegionSnapshot();
            _currentState = SelectionState.None;
            _originalSelectionRect = Rect.Empty;
            _isDragging = false;
            UpdateSelectionRect(Rect.Empty);
        }

        private void StartNewSelection(Point position) {
            SaveOriginalContentSnapshot();
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

        protected void SaveOriginalContentSnapshot() {
            if (RenderTarget == null) return;

            CanvasRenderTarget target = RenderTarget;

            var newOriginalSnapshot = new CanvasRenderTarget(
                target,
                target.SizeInPixels.Width,
                target.SizeInPixels.Height,
                target.Dpi,
                target.Format,
                target.AlphaMode);

            try {
                using var ds = newOriginalSnapshot.CreateDrawingSession();
                ds.Clear(Colors.Transparent);
                ds.DrawImage(target);
            }
            catch {
                newOriginalSnapshot.Dispose();
                throw;
            }

            _resources.ReplaceOriginalContentSnapshot(newOriginalSnapshot, target);
        }

        protected virtual void CaptureSelectedRegionSnapshot() {
            if (_selectionRect.IsEmpty) return;

            // 更新选区矩形为整数坐标，避免还原后残留虚影
            double x = Math.Floor(_selectionRect.X);
            double y = Math.Floor(_selectionRect.Y);
            double w = Math.Ceiling(_selectionRect.Width);
            double h = Math.Ceiling(_selectionRect.Height);
            _selectionRect = new Rect(x, y, w, h);

            CanvasRenderTarget selectionTarget = SourceRenderTarget ?? RenderTarget;
            var newSelectedRegionSnapshot = new CanvasRenderTarget(
                selectionTarget,
                (float)_selectionRect.Width,
                (float)_selectionRect.Height,
                selectionTarget.Dpi,
                selectionTarget.Format,
                selectionTarget.AlphaMode);

            _originalSelectionRect = _selectionRect;

            //捕获选区内容
            try {
                using var ds = newSelectedRegionSnapshot.CreateDrawingSession();
                ds.Blend = CanvasBlend.Copy;
                ds.DrawImage(OriginalContentSnapshot, newSelectedRegionSnapshot.Bounds, _selectionRect);
            }
            catch {
                newSelectedRegionSnapshot.Dispose();
                throw;
            }

            _resources.ReplaceSelectedRegionSnapshot(newSelectedRegionSnapshot);

            //剪切原位置
            using (var ds = OriginalContentSnapshot!.CreateDrawingSession()) {
                ds.Blend = CanvasBlend.Copy;
                ds.FillRectangle(_selectionRect, Colors.Transparent);
            }
        }

        public virtual IUndoableCommand? CommitSelection() {
            if (_currentState != SelectionState.Selected || SelectedRegionSnapshot == null) return null;

            var command = BuildUndoCommand();
            if (command != null) {
                CanvasRenderTarget? originalSnapshot = OriginalContentSnapshot;
                CanvasRenderTarget? target = SourceRenderTarget;
                ViewModel.Session.UnReUtil.RecordCommand(command);

                Reset();
                try {
                    RenderToTarget(target);
                }
                finally {
                    ReleaseOriginalContentSnapshot(originalSnapshot);
                }
                base.RequestOnceRender();
            }

            return command;
        }

        protected virtual void RenderToTarget(CanvasRenderTarget? target = null) {
            target ??= SourceRenderTarget ?? RenderTarget;
            if (target == null) return;

            try {
                using (var ds = target.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent); // 覆盖模式，避免重影

                    // 绘制基准内容
                    if (OriginalContentSnapshot != null) {
                        ds.DrawImage(OriginalContentSnapshot);
                    }

                    ds.Blend = CanvasBlend.SourceOver; // 避免透明遮盖
                    // 绘制选区内容（自动裁剪到画布边界）
                    if (SelectedRegionSnapshot != null && _currentState != SelectionState.None) {
                        ds.DrawImage(SelectedRegionSnapshot, (float)_selectionRect.X, (float)_selectionRect.Y);
                    }

                    // 绘制完整的选择框（包括延伸到画布外的部分）
                    if (_currentState != SelectionState.None) {
                        DrawFullSelectionBorder(ds, target);
                    }
                }

                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SEHException) {
                // 撤销/重做可能在绘制间隙替换 RenderTarget。
                Reset();
                ReleaseOriginalContentSnapshot();
                HandleRender(new RenderTargetChangedEventArgs(RenderMode.FullRegion));
            }
            catch (Exception ex) {
                ReportFatalError(ex);
            }
        }

        private void DrawFullSelectionBorder(CanvasDrawingSession ds, CanvasRenderTarget target) {
            using (var borderBrush = new CanvasSolidColorBrush(target, _selectionBorderColor)) {
                // 直接绘制完整的选择框，不进行边界裁剪
                ds.DrawRectangle(_selectionRect, borderBrush, _selectionBorderWidth, _borderStrokeStyle);
                // 可视区域边界指示
                DrawViewportIndicator(ds, target);
            }
        }

        private void DrawViewportIndicator(CanvasDrawingSession ds, CanvasRenderTarget target) {
            using (var viewportBrush = new CanvasSolidColorBrush(target, Color.FromArgb(255, 80, 80, 80))) {
                var viewport = new Rect(
                    0, 0,
                    target.SizeInPixels.Width,
                    target.SizeInPixels.Height);

                ds.DrawRectangle(viewport, viewportBrush, 2f);
            }
        }

        protected new void HandleDeviceLost() {
            base.HandleDeviceLost();
            ReleaseOriginalContentSnapshot();
            ReleaseSelectedRegionSnapshot();
        }

        protected void UpdateSelectionRect(Rect rect) {
            _selectionRect = rect;
            OnSelectRectChanged?.Invoke(this, rect);
        }

        /// <summary>
        /// 判断选区开始时记录的目标是否仍是某个图层当前使用的 RenderTarget。
        /// 资源已被撤销、重做或尺寸重建替换时，不能再访问旧目标。
        /// </summary>
        protected bool IsTargetStillAttached(CanvasRenderTarget target) {
            foreach (var layer in ViewModel.Data.AllLayers) {
                if (ReferenceEquals(layer.RenderData?.RenderTarget, target)) return true;
            }
            return false;
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
            ReleaseOriginalContentSnapshot();
            ReleaseSelectedRegionSnapshot();
            // RenderTarget由外部管理，此处不释放
        }

        protected void ReleaseOriginalContentSnapshot(CanvasRenderTarget? expectedSnapshot = null) {
            _resources.ReleaseOriginalContentSnapshot(expectedSnapshot);
        }

        private void ReleaseSelectedRegionSnapshot() {
            _resources.ReleaseSelectedRegionSnapshot();
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

        private readonly SelectionResourceStore<CanvasRenderTarget> _resources = new();
        private InputCursor? _crossCursor;
        private InputCursor? _moveCursor;
        private InputSystemCursorShape? _lastCursorShape;

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
