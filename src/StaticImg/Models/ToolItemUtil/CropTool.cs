using System;
using System.Numerics;
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
    partial class CropTool : Tool, IDisposable {
        public override event EventHandler<CursorChangedEventArgs> SystemCursorChangeRequested;
        public Rect SelectionRect => _selectionRect;

        public CropTool(LayerBasicData basicData) {
            this._ratioController = new AspectRatioController(this);
            this._basicData = basicData;
        }

        public override void OnPointerEntered(CanvasPointerEventArgs e) {
            base.OnPointerEntered(e);
            SaveBaseContent();
            SystemCursorChangeRequested?.Invoke(this, new(InputSystemCursor.Create(InputSystemCursorShape.Cross)));
        }

        public override void OnPointerPressed(CanvasPointerEventArgs e) {
            if (e.Pointer.Properties.IsRightButtonPressed && _currentState == SelectionState.Selected) {
                TryRestoreOriginalContent();
                return;
            }
            if (!IsPointerOverTaregt(e) || e.Pointer.Properties.IsMiddleButtonPressed) return;

            //if (_baseContent == null) SaveBaseContent();
            var position = e.Pointer.Position;
            switch (_currentState) {
                case SelectionState.None:
                    StartNewSelection(position);
                    RenderToTarget();
                    break;

                case SelectionState.Selected:
                    if (_selectionRect.Contains(position)) {
                        StartDragSelection(position);
                        RenderToTarget();
                    }
                    else {
                        CommitSelection();
                    }
                    break;
            }            
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

        public bool TryRestoreOriginalContent() {
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

        public bool CommitSelection() {
            if (_currentState != SelectionState.Selected || _selectionContent == null)
                return false;

            // 创建临时绘图目标
            CanvasRenderTarget newBaseContent = null;
            try {
                newBaseContent = new CanvasRenderTarget(
                    RenderTarget,
                    RenderTarget.SizeInPixels.Width,
                    RenderTarget.SizeInPixels.Height,
                    RenderTarget.Dpi);

                using (var ds = newBaseContent.CreateDrawingSession()) {
                    // 清空整个画布
                    ds.Clear(Colors.Transparent);

                    // 将选区内容绘制到原始位置
                    ds.DrawImage(_selectionContent,
                        new Rect(_selectionRect.X, _selectionRect.Y,
                                _selectionRect.Width, _selectionRect.Height));
                }

                // 安全替换基础内容
                var oldContent = _baseContent;
                _baseContent = newBaseContent;
                oldContent?.Dispose();

                // 重置选区状态
                _currentState = SelectionState.None;
                SafeDispose(ref _selectionContent);
                RenderToTarget();

                return true;
            }
            catch {
                newBaseContent?.Dispose();
                throw;
            }
        }

        private void RenderToTarget() {
            try {
                if (RenderTarget == null) return;

                using (var ds = RenderTarget.CreateDrawingSession()) {
                    // 完全清空画布
                    ds.Clear(Colors.Transparent);

                    // 只绘制基础内容（提交后就是选区内容+透明背景）
                    if (_baseContent != null) {
                        ds.DrawImage(_baseContent);
                    }

                    // 绘制进行中的选区状态
                    if (_currentState != SelectionState.None) {
                        // 绘制选区内容（拖拽预览）
                        if (_selectionContent != null) {
                            ds.DrawImage(_selectionContent, (float)_selectionRect.X, (float)_selectionRect.Y);
                        }

                        // 绘制半透明遮罩（选区外区域）
                        using (var overlayBrush = new CanvasSolidColorBrush(RenderTarget, Color.FromArgb(180, 0, 0, 0))) {
                            var outer = CanvasGeometry.CreateRectangle(ds,
                                new Rect(0, 0, RenderTarget.Size.Width, RenderTarget.Size.Height));
                            var inner = CanvasGeometry.CreateRectangle(ds, _selectionRect);
                            ds.FillGeometry(outer.CombineWith(inner, Matrix3x2.Identity, CanvasGeometryCombine.Exclude), overlayBrush);
                        }

                        // 绘制选择框
                        using (var borderBrush = new CanvasSolidColorBrush(RenderTarget, _selectionBorderColor)) {
                            ds.DrawRectangle(_selectionRect, borderBrush, _selectionBorderWidth,
                                new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash });
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
            _basicData.SelectionRect = rect;
        }

        public void ApplyAspectRatio(double ratio) {
            RenderTarget ??= _basicData.SelectedInkCanvas.Render.RenderTarget;
            if (_baseContent == null) SaveBaseContent();
            TryRestoreOriginalContent();
            _ratioController.ApplyRatio(ratio);
        }

        #region dispose
        private bool _disposed = false;
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
                // 释放所有托管资源
                ReleaseAllResources();
            }

            _disposed = true;
        }

        private void ReleaseAllResources() {
            SafeDispose(ref _baseContent);
            SafeDispose(ref _selectionContent);
            // RenderTarget由外部管理，此处不释放
        }

        private static void SafeDispose(ref CanvasRenderTarget resource) {
            try {
                resource?.Dispose();
                resource = null;
            }
            catch { /* 不抛出异常 */ }
        }
        #endregion

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

        private readonly AspectRatioController _ratioController;
        private readonly LayerBasicData _basicData;

        internal class AspectRatioController {
            private readonly CropTool _parent;
            private double _currentRatio;

            public AspectRatioController(CropTool parent) {
                _parent = parent;
            }

            public void ApplyRatio(double ratio) {
                if (_parent.RenderTarget == null) return;

                _currentRatio = ratio;
                CreateCenteredCrop();
                _parent.RenderToTarget();
            }

            private void CreateCenteredCrop() {
                var size = CalculateInitialSize();
                _parent._selectionRect = new Rect(
                    (_parent.RenderTarget.SizeInPixels.Width - size.Width) / 2,
                    (_parent.RenderTarget.SizeInPixels.Height - size.Height) / 2,
                    size.Width,
                    size.Height);
                _parent.UpdateSelectionRect(_parent._selectionRect);
                _parent._currentState = SelectionState.Selected;
                _parent.CaptureSelectionContent();
            }

            private Size CalculateInitialSize() {
                const double maxScale = 0.8;
                var canvas = _parent.RenderTarget.SizeInPixels;

                // 自由比例模式（使用图片中的默认值）
                if (_currentRatio == 0)
                    return new Size(canvas.Width * maxScale, canvas.Height * maxScale);                    

                // 预设比例模式
                double ratio = _currentRatio;
                double maxW = canvas.Width * maxScale;
                double maxH = canvas.Height * maxScale;

                // 高度优先
                Size optionA = new(maxH * ratio, maxH);
                bool isOptionAValid = optionA.Width <= maxW;

                // 宽度优先
                Size optionB = new(maxW, maxW / ratio);
                bool isOptionBValid = optionB.Height <= maxH;

                return (isOptionAValid, isOptionBValid) switch {
                    (true, true) => ArcSize.Area(optionA) > ArcSize.Area(optionB) ? optionA : optionB, // 两者有效选面积大的
                    (true, false) => optionA, // 只有A有效
                    (false, true) => optionB, // 只有B有效
                    _ => GetFallbackSize(ratio, maxW, maxH) // 双重越界时的降级方案
                };
            }

            private static Size GetFallbackSize(double ratio, double maxW, double maxH) {
                // 比例缩放
                double scale = Math.Min(maxW / ratio, maxH) / maxH;
                return new Size(maxH * ratio * scale, maxH * scale);
            }
        }
    }
}
