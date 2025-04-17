using System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Microsoft.UI.Input;
using VirtualPaper.UIComponent.Services;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItemUtil {
    partial class SelectionTool(LayerBasicData basicData) : Tool {
        public override event EventHandler<CursorChangedEventArgs> SystemCursorChangeRequested;
        public Rect SelectionRect => _selectionRect;

        public void TryCommitSelection() {
            var op = CommitSelection();
            if (op) RenderToTarget();
        }

        public override void OnPointerEntered(CanvasPointerEventArgs e) {
            base.OnPointerEntered(e);
            SystemCursorChangeRequested?.Invoke(this, new(InputSystemCursor.Create(InputSystemCursorShape.Cross)));
        }

        public override void OnPointerPressed(CanvasPointerEventArgs e) {
            if (e.Pointer.Properties.IsRightButtonPressed && _currentState == SelectionState.Selected) {
                RestoreOriginalContent();
                return;
            }
            if (!IsPointerOverTaregt(e) || e.Pointer.Properties.IsMiddleButtonPressed) return;

            RenderTarget = e.RenderData.RenderTarget;
            if (_baseContent == null) SaveBaseContent();

            var position = e.Pointer.Position;

            switch (_currentState) {
                case SelectionState.None:
                    StartNewSelection(position);
                    break;

                case SelectionState.Selected:
                    if (_selectionRect.Contains(position)) {
                        StartDragSelection(position);
                    }
                    else {
                        CommitSelection();
                    }
                    break;
            }

            RenderToTarget();
        }

        public override void OnPointerMoved(CanvasPointerEventArgs e) {
            if (!e.Pointer.Properties.IsLeftButtonPressed) return;

            var currentPos = new Point(
                Math.Min(RenderTarget.SizeInPixels.Width, Math.Max(0, e.Pointer.Position.X)),
                Math.Min(RenderTarget.SizeInPixels.Height, Math.Max(0, e.Pointer.Position.Y)));

            if (_currentState == SelectionState.Selecting) {
                if (_isDragging) {
                    double offsetX = currentPos.X - _moveStartPoint.X;
                    double offsetY = currentPos.Y - _moveStartPoint.Y;

                    Rect newRect = new(
                        _currentDragStartRect.X + offsetX,
                        _currentDragStartRect.Y + offsetY,
                        _currentDragStartRect.Width,
                        _currentDragStartRect.Height);

                    // 允许部分超出边界，但确保至少有一部分可见
                    if (newRect.Right > 0 && newRect.Bottom > 0 &&
                        newRect.Left < RenderTarget.SizeInPixels.Width &&
                        newRect.Top < RenderTarget.SizeInPixels.Height) {
                        UpdateSelectionRect(newRect);
                    }
                }
                else {
                    UpdateSelectionRect(new Rect(
                        Math.Min(_startPoint.X, currentPos.X),
                        Math.Min(_startPoint.Y, currentPos.Y),
                        Math.Abs(currentPos.X - _startPoint.X),
                        Math.Abs(currentPos.Y - _startPoint.Y)));
                }
            }

            RenderToTarget();
        }

        public override void OnPointerReleased(CanvasPointerEventArgs e) {
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
                    _currentState = SelectionState.None;
                    UpdateSelectionRect(Rect.Empty);
                }
            }

            RenderToTarget();
        }

        private void RestoreOriginalContent() {
            if (_selectionContent == null) return;

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
        }

        private void StopSelection() {
            _selectionContent?.Dispose();
            _selectionContent = null;
            _currentState = SelectionState.None;
            UpdateSelectionRect(Rect.Empty);
        }

        private void StartNewSelection(Point position) {
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

        private void SaveBaseContent() {
            _baseContent?.Dispose();
            _baseContent = new CanvasRenderTarget(
                RenderTarget,
                RenderTarget.SizeInPixels.Width,
                RenderTarget.SizeInPixels.Height,
                RenderTarget.Dpi);

            using (var ds = _baseContent.CreateDrawingSession()) {
                ds.Clear(Colors.Transparent);
                ds.DrawImage(RenderTarget);
            }
        }

        private void CaptureSelectionContent() {
            if (RenderTarget == null || _selectionRect.IsEmpty) return;

            // 保存原始位置（在首次捕获时记录）
            if (!_isDragging) {
                _originalSelectionRect = _selectionRect;
            }

            _selectionContent?.Dispose();
            _selectionContent = new CanvasRenderTarget(
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

        private bool CommitSelection() {
            if (_currentState != SelectionState.Selected || _selectionContent == null) return false;

            // 将选区内容合并到基础层
            using (var ds = _baseContent.CreateDrawingSession()) {
                ds.DrawImage(_selectionContent, (float)_selectionRect.X, (float)_selectionRect.Y);
            }
            StopSelection();

            return true;
        }

        private void RenderToTarget() {
            try {
                if (RenderTarget == null) return;

                using (var ds = RenderTarget.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);

                    // 绘制基准内容
                    if (_baseContent != null) {
                        ds.DrawImage(_baseContent);
                    }

                    // 绘制选区内容
                    if (_selectionContent != null && _currentState != SelectionState.None) {
                        ds.DrawImage(_selectionContent, (float)_selectionRect.X, (float)_selectionRect.Y);
                    }

                    // 绘制选择框
                    if (_currentState != SelectionState.None) {
                        using (var borderBrush = new CanvasSolidColorBrush(RenderTarget, _selectionBorderColor)) {
                            var strokeStyle = new CanvasStrokeStyle() { DashStyle = CanvasDashStyle.Dash };

                            // 计算实际可见的矩形部分
                            var visibleRect = new Rect(
                                Math.Max(0, _selectionRect.Left),
                                Math.Max(0, _selectionRect.Top),
                                Math.Min(RenderTarget.SizeInPixels.Width, _selectionRect.Right) - Math.Max(0, _selectionRect.Left),
                                Math.Min(RenderTarget.SizeInPixels.Height, _selectionRect.Bottom) - Math.Max(0, _selectionRect.Top));

                            if (visibleRect.Width > 0 && visibleRect.Height > 0) {
                                ds.DrawRectangle(visibleRect, borderBrush, _selectionBorderWidth, strokeStyle);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
        }

        public void CancelSelection() {
            if (_selectionContent == null) return;

            // 恢复原位置内容
            using (var ds = _baseContent.CreateDrawingSession()) {
                ds.DrawImage(_selectionContent,
                    (float)_originalSelectionRect.X,
                    (float)_originalSelectionRect.Y);
            }

            // 重置状态
            _selectionContent?.Dispose();
            _selectionContent = null;
            _currentState = SelectionState.None;
            UpdateSelectionRect(Rect.Empty);
            _isDragging = false;

            RenderToTarget();
        }

        private static bool IsDeviceLost(Exception ex) {
            return ex.HResult == unchecked((int)0x8899000C);
        }

        private void HandleDeviceLost() {
            _baseContent?.Dispose();
            _baseContent = null;
            _selectionContent?.Dispose();
            _selectionContent = null;
            RenderTarget?.Dispose();
            RenderTarget = null;
        }

        private void UpdateSelectionRect(Rect rect) {
            _selectionRect = rect;
            basicData.SelectionRect = rect;
        }

        private enum SelectionState {
            None,       // 无选择
            Selecting,  // 正在选择或拖动区域
            Selected    // 已选择区域
        }

        private SelectionState _currentState = SelectionState.None;
        private Point _startPoint;
        private Rect _selectionRect;
        private Point _moveStartPoint;
        private Rect _originalSelectionRect; // 基准层的选区位置（用于还原）
        private Rect _currentDragStartRect; // 当前拖动开始时的选区位置
        private bool _isDragging; // 标记当前是否在拖动

        // 图层缓存
        private CanvasRenderTarget _baseContent; // 基准层
        private CanvasRenderTarget _selectionContent;

        // 绘制样式
        private readonly Color _selectionBorderColor = Colors.Black;
        private readonly float _selectionBorderWidth = 3.0f;
    }
}
