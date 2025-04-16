using System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItemUtil {
    partial class SelectionTool(LayerBasicData basicData) : Tool {
        public override void OnPointerEntered(CanvasPointerEventArgs e) { }
        public override void OnPointerExited(CanvasPointerEventArgs e) { }
      
        public override void OnPointerPressed(CanvasPointerEventArgs e) {
            if (e.Pointer.Properties.IsMiddleButtonPressed) return;

            if (e.Pointer.Properties.IsRightButtonPressed && _currentState == SelectionState.Selected) {
                RestoreOriginalContent();
                return;
            }

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

            var currentPos = e.Pointer.Position;

            if (_currentState == SelectionState.Selecting) {
                if (_isDragging) {
                    // 拖动现有选区
                    double offsetX = currentPos.X - _moveStartPoint.X;
                    double offsetY = currentPos.Y - _moveStartPoint.Y;
                    _selectionRect = new Rect(
                        _originalSelectionRect.X + offsetX,
                        _originalSelectionRect.Y + offsetY,
                        _originalSelectionRect.Width,
                        _originalSelectionRect.Height);
                }
                else {
                    // 创建新选区
                    _selectionRect = new Rect(
                        Math.Min(_startPoint.X, currentPos.X),
                        Math.Min(_startPoint.Y, currentPos.Y),
                        Math.Abs(currentPos.X - _startPoint.X),
                        Math.Abs(currentPos.Y - _startPoint.Y));
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
                    _selectionRect = Rect.Empty;
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
            _selectionRect = Rect.Empty;
        }

        private void StartNewSelection(Point position) {
            _currentState = SelectionState.Selecting;
            _startPoint = position;
            _selectionRect = new Rect(position, new Size(0, 0));
            _isDragging = false;
            _originalSelectionRect = Rect.Empty;
        }

        private void StartDragSelection(Point position) {
            _currentState = SelectionState.Selecting;
            _moveStartPoint = position;
            _isDragging = true;
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

        private void CommitSelection() {
            if (_currentState != SelectionState.Selected || _selectionContent == null) return;

            // 将选区内容合并到基础层
            using (var ds = _baseContent.CreateDrawingSession()) {
                ds.DrawImage(_selectionContent, (float)_selectionRect.X, (float)_selectionRect.Y);
            }

            StopSelection();
        }

        private void RenderToTarget() {
            try {
                if (RenderTarget == null) return;

                using (var ds = RenderTarget.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);

                    // 1. 绘制基准内容
                    if (_baseContent != null) {
                        ds.DrawImage(_baseContent);
                    }

                    // 2. 绘制选区内容
                    if (_selectionContent != null && _currentState != SelectionState.None) {
                        ds.DrawImage(_selectionContent, (float)_selectionRect.X, (float)_selectionRect.Y);
                    }

                    // 3. 绘制选择框
                    if (_currentState != SelectionState.None) {
                        using (var borderBrush = new CanvasSolidColorBrush(RenderTarget, _selectionBorderColor)) {
                            var strokeStyle = new CanvasStrokeStyle() {
                                DashStyle = CanvasDashStyle.Dot,
                            };
                            ds.DrawRectangle(_selectionRect, borderBrush, _selectionBorderWidth, strokeStyle);
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
            _selectionRect = Rect.Empty;
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

        private enum SelectionState {
            None,       // 无选择
            Selecting,  // 正在选择或拖动区域
            Selected    // 已选择区域
        }

        private SelectionState _currentState = SelectionState.None;
        private Point _startPoint;
        private Rect _selectionRect;
        private Point _moveStartPoint;
        private Rect _originalSelectionRect;
        private bool _isDragging; // 标记当前是否在拖动

        // 图层缓存
        private CanvasRenderTarget _baseContent; // 基准层
        private CanvasRenderTarget _selectionContent;

        // 绘制样式
        private readonly Color _selectionBorderColor = Colors.Black;
        private readonly float _selectionBorderWidth = 2.5f;
    }
}
