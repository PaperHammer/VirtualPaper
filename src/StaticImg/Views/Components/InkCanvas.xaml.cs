using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.UIComponent.Collection;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Input;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.ToolItems;
using Workloads.Creation.StaticImg.Utils;
using Workloads.Creation.StaticImg.ViewModels;
using Workloads.Creation.StaticImg.Views.Tools;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Components {
    public sealed partial class InkCanvas : ArcUserControl {
        public TaskCompletionSource<bool> IsInited => _isInited;

        public InkCanvas() {
            this.InitializeComponent();
            _originalInputCursor = this.ProtectedCursor ?? InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            _tool = new ToolManager();
        }

        protected override void OnPayloadChanged(FrameworkPayload? newPayload, FrameworkPayload? oldPayload) {
            base.OnPayloadChanged(newPayload, oldPayload);

            if (newPayload == null) {
                return;
            }

            ArcPageContext? context = null;
            if (Payload != null) {
                Payload.TryGet(NaviPayloadKey.InkProjectSession, out _session);
                Payload.TryGet(NaviPayloadKey.ArcPageContext, out context);
                Consts.InitData(_session);
            }
            _viewModel = new InkCanvasViewModel(_session, context);
        }

        private void ArcUserControl_Loaded(object sender, RoutedEventArgs e) {
            RegisterTools();
        }

        private void RegisterTools() {
            _tool.RegisterTool(ToolType.PaintBrush, new BrushTool(_viewModel.Data));
            _tool.RegisterTool(ToolType.Fill, new FillTool(_viewModel.Data));
            _tool.RegisterTool(ToolType.Eraser, new EraserTool(_viewModel.Data));
            _tool.RegisterTool(ToolType.Selection, new SelectionTool(_viewModel.Data));
            _tool.RegisterTool(ToolType.Crop, new CropTool(_viewModel.Data));

            foreach (var tool in _tool.GetAllTools()) {
                tool.SystemCursorChangeRequested += (s, e) => {
                    this.ProtectedCursor = e.Cursor ?? _originalInputCursor;
                };

                tool.RenderRequest += (s, e) => {
                    RenderToCompositeTarget(e.Mode, e.Region);
                };

                tool.OnceRenderCompleted += (s, e) => {
                    OnOnceRenderCompleted();
                };
            }
        }

        internal async Task SaveAsync() {
            await _viewModel.SaveAsync();
        }

        #region children event
        private void SetupHandlers() {
            _viewModel.Data.SizeChanged += (s, e) => {
                RebuildComposite();
                RenderToCompositeTarget(RenderMode.FullRegion);
            };
            _viewModel.Data.SeletcedToolChanged += (s, e) => {
                //before
                HandleSelectionToolBefore();
                _selectedTool = _tool.GetTool(_viewModel.Data.SelectedToolItem.Type);
                //after

            };
            _viewModel.Data.SeletcedLayerChanged += (s, e) => {
                HandleLayerChanged();
            };
            _viewModel.Data.SelectedCropAspectClicked += (s, e) => {
                HandleCropAspectClicked(e);
            };
            _viewModel.Data.RenderRequest += (s, e) => {
                RenderToCompositeTarget(e.Mode, e.Region);
            };
            _viewModel.Data.GetFocus += (s, e) => {
                this.Focus(FocusState.Programmatic);
            };
        }

        private void HandleCropAspectClicked(double e) {
            if (_selectedTool is CropTool ct) {
                ct.ApplyAspectRatio(e);
            }
        }

        private void HandleLayerChanged() {
            TryRestore();
        }

        private void HandleSelectionToolBefore() {
            TryRestore();
        }

        private void TryRestore() {
            if (_selectedTool is SelectionTool st) {
                var op = st.RestoreOriginalContent();
                if (op) RenderToCompositeTarget(RenderMode.FullRegion);
            }
            else if (_selectedTool is CropTool ct) {
                var op = ct.RestoreOriginalContent();
                if (op) RenderToCompositeTarget(RenderMode.FullRegion);
            }
        }

        private void RebuildComposite() {
            DebugUtil.Output("RebuildComposite triggered");
            _compositeTarget = new CanvasRenderTarget(
                _session.SharedDevice,
                (float)_viewModel.Data.CanvasSize.Width,
                (float)_viewModel.Data.CanvasSize.Height,
                _viewModel.Data.CanvasSize.Dpi,
                _session.SharedFormat,
                _session.SharedAlphaMode);
        }
        #endregion

        #region redner
        private async void RenderCanvas_Loaded(object sender, RoutedEventArgs e) {
            try {
                if (IsInited.Task.IsCompleted) return;

                await _viewModel.LoadAsync();
                SetupHandlers();
                FitView();
                RebuildComposite();
                RenderToCompositeTarget(RenderMode.FullRegion);
                IsInited.TrySetResult(true);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
            }
        }

        private void RenderCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args) {
            if (_compositeTarget == null) return;

            using (args.DrawingSession) {
                args.DrawingSession.DrawImage(_compositeTarget);
            }
        }

        private void OnOnceRenderCompleted() {
            _viewModel.Data.SelectedLayer.RenderData.HandleOnceRenderCompleted();
        }

        private void RenderToCompositeTarget(RenderMode mode, Rect region = default) {
            DebugUtil.Output("RenderToCompositeTarget triggered");
            if (_compositeTarget == null) return;

            var layers = _viewModel.Data.ActiveLayers;
            using (var ds = _compositeTarget.CreateDrawingSession()) {
                if (mode == RenderMode.FullRegion) {
                    ds.Clear(Colors.Transparent);
                    FullRender(layers, ds);
                }
                else {
                    if (region == Rect.Empty) return;
                    PartialRender(layers, ds, region);
                }
            }

            renderCanvas.Invalidate();
        }

        private void FullRender(IEnumerable<LayerInfo> layers, CanvasDrawingSession ds) {
            using (var batch = ds.CreateSpriteBatch()) {
                foreach (var layer in layers.Reverse()) {
                    if (layer.RenderData?.RenderTarget == null) continue;
                    // SpriteBatch 在绘制大量纹理时效率更高
                    batch.Draw(layer.RenderData.RenderTarget, new System.Numerics.Vector2(0, 0));
                }
            }
        }

        private void PartialRender(IEnumerable<LayerInfo> layers, CanvasDrawingSession ds, Rect region) {
            using (var layerDs = ds.CreateLayer(1.0f, region)) { // 限制绘制区域提升性能
                ds.Blend = CanvasBlend.Copy;
                ds.FillRectangle(region, Colors.Transparent); // 强制抹除旧的合成像素
                ds.Blend = CanvasBlend.SourceOver; // 切回正常模式进行重新合并

                foreach (var layer in layers.Reverse()) {
                    if (layer.RenderData?.RenderTarget == null) continue;

                    // 检查图层内容是否与刷新区域有交集
                    var visibleRect = region.IntersectRect(layer.RenderData.RenderTarget.Bounds);
                    if (!visibleRect.IsEmpty) {
                        // 使用源矩形和目标矩形 1:1 绘制
                        ds.DrawImage(layer.RenderData.RenderTarget, visibleRect, visibleRect);
                    }
                }
            }
        }

        private void InitializeGridPattern(ICanvasResourceCreator rc) {
            _gridBrush?.Dispose();

            using var texture = new CanvasRenderTarget(rc, _gridSize * 2, _gridSize * 2, 96);
            using (var ds = texture.CreateDrawingSession()) {
                ds.Clear(Color.FromArgb(255, 168, 168, 168));
                ds.FillRectangle(_gridSize, 0, _gridSize, _gridSize, Color.FromArgb(255, 150, 150, 150));
                ds.FillRectangle(0, _gridSize, _gridSize, _gridSize, Color.FromArgb(255, 150, 150, 150));
            }

            _gridBrush = new CanvasImageBrush(rc, texture) {
                ExtendX = CanvasEdgeBehavior.Wrap,
                ExtendY = CanvasEdgeBehavior.Wrap
            };
        }

        private void BackgroundGrid_RegionsInvalidated(CanvasVirtualControl sender, CanvasRegionsInvalidatedEventArgs args) {
            if (_gridBrush?.Device != sender.Device) InitializeGridPattern(sender);

            foreach (var region in args.InvalidatedRegions) {
                using var ds = sender.CreateDrawingSession(region);
                ds.FillRectangle(region, _gridBrush);
            }
        }
        #endregion

        #region Scroll
        private void Scroll_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e) {
            _viewModel.Data.CanvasZoom = e.FinalView.ZoomFactor;
        }

        private void FitView() {
            // 获取可用显示区域
            double availableWidth = Scroll.ViewportWidth;
            double availableHeight = Scroll.ViewportHeight;

            // 考虑边距
            double effectiveWidth = availableWidth - (Container.Margin.Left + Container.Margin.Right);
            double effectiveHeight = availableHeight - (Container.Margin.Top + Container.Margin.Bottom);

            // 计算缩放比例
            double widthRatio = effectiveWidth / _viewModel.Data.CanvasSize.Width;
            double heightRatio = effectiveHeight / _viewModel.Data.CanvasSize.Height;

            // 选择较小的比例以确保完全显示
            double zoomFactor = Math.Min(widthRatio, heightRatio);

            // 应用缩放限制
            zoomFactor = Math.Max(Consts.MinZoomFactor, Math.Min(zoomFactor, Consts.MaxZoomFactor));

            // 应用缩放
            PerformZoom((float)zoomFactor, null);
        }

        /// <summary>
        /// 通用缩放方法
        /// </summary>
        /// <param name="targetZoom">目标缩放比例</param>
        /// <param name="centerPoint">缩放中心点（相对于 ScrollViewer 视口）。如果为 null，则以当前视口中心为基准。</param>
        /// <param name="disableAnimation">是否禁用动画（Slider拖动建议禁用，按钮点击建议启用）</param>
        private void PerformZoom(float targetZoom, Point? centerPoint = null, bool disableAnimation = false) {
            // 获取当前状态
            float currentZoom = Scroll.ZoomFactor;

            // 限制缩放范围
            targetZoom = Math.Clamp(targetZoom, (float)Consts.MinZoomFactor, (float)Consts.MaxZoomFactor);

            // 如果变化极小，直接忽略（避免浮点数抖动）
            if (Math.Abs(targetZoom - currentZoom) < 0.001f) return;

            // 确定缩放参考中心点 (Viewport 坐标系)
            double viewportX, viewportY;

            if (centerPoint.HasValue) {
                // 指定点（如：鼠标位置）
                viewportX = centerPoint.Value.X;
                viewportY = centerPoint.Value.Y;
            }
            else {
                // 未指定点 -> 使用视口几何中心
                viewportX = Scroll.ViewportWidth / 2.0;
                viewportY = Scroll.ViewportHeight / 2.0;
            }

            // 计算保持中心点不动的 offset
            // (当前Offset + 视口中心) / 当前缩放 = 内容绝对坐标
            // 内容绝对坐标 * 新缩放 - 视口中心 = 新Offset

            double contentX = (Scroll.HorizontalOffset + viewportX) / currentZoom;
            double contentY = (Scroll.VerticalOffset + viewportY) / currentZoom;

            double newHorizontalOffset = (contentX * targetZoom) - viewportX;
            double newVerticalOffset = (contentY * targetZoom) - viewportY;

            Scroll.ChangeView(newHorizontalOffset, newVerticalOffset, targetZoom, disableAnimation);

            if (_viewModel?.Data != null) {
                _viewModel.Data.CanvasZoom = targetZoom;
            }
        }

        /// <summary>
        /// 通用滚动方法
        /// </summary>
        /// <param name="deltaX">水平滚动量</param>
        /// <param name="deltaY">垂直滚动量</param>
        private void PerformScroll(double deltaX, double deltaY) {
            double newHorizontalOffset = Scroll.HorizontalOffset + deltaX;
            double newVerticalOffset = Scroll.VerticalOffset + deltaY;

            Scroll.ChangeView(newHorizontalOffset, newVerticalOffset, null, false);
        }

        private void BottomDataBarControl_ZoomComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems[0] is string textValue) {
                var val = double.Parse(textValue.TrimEnd('%')) / 100;
                PerformZoom((float)val);
            }
        }

        private void BottomDataBarControl_ZoomComboBoxTextSubmitted(object sender, ComboBoxTextSubmittedEventArgs e) {
            var val = double.Parse(e.Text.TrimEnd('%')) / 100;
            PerformZoom((float)val);
        }

        private void BottomDataBarControl_ZoomInRequest(object sender, RoutedEventArgs e) {
            var newZoomFactor = Math.Max(Consts.MinZoomFactor,
                Consts.RoundToNearestFive(_viewModel.Data.CanvasZoom) + Consts.GetSubStepSize(_viewModel.Data.CanvasZoom));
            PerformZoom((float)newZoomFactor);
        }

        private void BottomDataBarControl_ZoomOutRequest(object sender, RoutedEventArgs e) {
            var newZoomFactor = Math.Max(Consts.MinZoomFactor,
                Consts.RoundToNearestFive(_viewModel.Data.CanvasZoom) - Consts.GetSubStepSize(_viewModel.Data.CanvasZoom));
            PerformZoom((float)newZoomFactor);
        }

        private void BottomDataBarControl_ZoomSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e) {
            var newZoomFactor = Consts.PercentToDeciaml((float)e.NewValue);
            PerformZoom((float)newZoomFactor);
        }
        #endregion

        #region CanvasSet
        private void CanvasSet_OnValueChanged(object sender, ArcSize e) {
            _viewModel.Data.CanvasSize = new(e.Width, e.Height, e.Dpi, e.Rebuild);
        }

        private void CanvasOperationBtn_Click(object sender, RoutedEventArgs e) {
            RebuildMode rm = (CanvasOperation)((Button)sender).Tag switch {
                CanvasOperation.RotateLeft => RebuildMode.RotateLeft,
                CanvasOperation.RotateRight => RebuildMode.RotateRight,
                CanvasOperation.FlipHorizontally => RebuildMode.FlipHorizontal,
                CanvasOperation.FlipVertically => RebuildMode.FlipVertical,
                _ => RebuildMode.None,
            };
            _viewModel.Data.CanvasSize = new(
                _viewModel.Data.CanvasSize.Width,
                _viewModel.Data.CanvasSize.Height,
                _viewModel.Data.CanvasSize.Dpi,
                rm);
        }
        #endregion

        #region Selection
        private void Selection_SelectCancel(object sender, RoutedEventArgs e) {
            SelectionRequested(SeletionRequest.Cancel);
        }

        private void Selection_SelectCommit(object sender, RoutedEventArgs e) {
            SelectionRequested(SeletionRequest.Commit);
        }

        private void SelectionRequested(SeletionRequest sr) {
            if (_selectedTool is not SelectionTool st) return;

            bool op;
            switch (sr) {
                case SeletionRequest.Commit:
                    op = st.CommitSelection();
                    if (op) RenderToCompositeTarget(RenderMode.FullRegion);
                    break;
                case SeletionRequest.Cancel:
                    op = st.RestoreOriginalContent();
                    if (op) RenderToCompositeTarget(RenderMode.FullRegion);
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region Crop
        private void Crop_CropCancelRequest(object sender, RoutedEventArgs e) {
            CropRequested(CropRequest.Cancel);
        }

        private void Crop_CropCommitRequest(object sender, RoutedEventArgs e) {
            CropRequested(CropRequest.Commit);
        }

        private void CropRequested(CropRequest cr) {
            if (_selectedTool is not CropTool ct) return;

            bool op;
            switch (cr) {
                case CropRequest.Commit:
                    op = ct.CommitSelection();
                    if (op) RenderToCompositeTarget(RenderMode.FullRegion);
                    break;
                case CropRequest.Cancel:
                    op = ct.RestoreOriginalContent();
                    if (op) RenderToCompositeTarget(RenderMode.FullRegion);
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region Layer Mangaer
        private void LayerManage_AddLayerRequest(object sender, Guid id) {
            _viewModel.Data.AddLayer(layerId: id);
            RenderToCompositeTarget(RenderMode.FullRegion);
        }

        private void LayerManage_CopyLayerRequest(object sender, Guid id) {
            _viewModel.Data.CopyLayer(id);
            RenderToCompositeTarget(RenderMode.FullRegion);
        }

        private async void LayerManage_RenameLayerRequest(object sender, Guid id) {
            await _viewModel.Data.SetLayerNameAsync(id);
        }

        private void LayerManage_DeleteLayerRequest(object sender, Guid id) {
            _viewModel.Data.DeleteLayer(id);
            RenderToCompositeTarget(RenderMode.FullRegion);
        }

        private void LayerManage_MoveLayerRequest(object sender, ItemMoveEventArgs e) {
            _viewModel.Data.MoveLayer(e.Item as LayerInfo, e.OldIndex, e.NewIndex);
            RenderToCompositeTarget(RenderMode.FullRegion);
        }
        #endregion

        #region ColorPalette
        private async void ColorPalette_CustomeColorChanged(object sender, ColorChangeEventArgs e) {
            await _viewModel.Data.UpdateCustomColorsAsync(e);
        }
        #endregion

        #region BottomBar
        private void BottomDataBarControl_FitViewRequest(object sender, RoutedEventArgs e) {
            FitView();
        }
        #endregion

        #region ui events
        // 由子控件冒泡事件传递
        private void Scroll_PointerMoved(object sender, PointerRoutedEventArgs e) {
            OnPointerMoved(e, PointerPosition.InsideContainer);
        }

        private void Scroll_PointerPressed(object sender, PointerRoutedEventArgs e) {
            OnPointerPressed(e, PointerPosition.InsideContainer);
        }

        private void Scroll_PointerReleased(object sender, PointerRoutedEventArgs e) {
            OnPointerReleased(e, PointerPosition.InsideContainer);
        }

        private void Scroll_PointerExited(object sender, PointerRoutedEventArgs e) {
            OnPointerExited(e, PointerPosition.OutsideContainer);
        }

        private void Container_PointerEntered(object sender, PointerRoutedEventArgs e) {
            OnPointerEntered(e, PointerPosition.InsideCanvas);
        }

        private void Container_PointerMoved(object sender, PointerRoutedEventArgs e) {
            OnPointerMoved(e, PointerPosition.InsideCanvas);
            e.Handled = true;
        }

        private void Container_PointerPressed(object sender, PointerRoutedEventArgs e) {
            OnPointerPressed(e, PointerPosition.InsideCanvas);
            e.Handled = true;
        }

        private void Container_PointerReleased(object sender, PointerRoutedEventArgs e) {
            OnPointerReleased(e, PointerPosition.InsideCanvas);
            e.Handled = true;
        }

        private void Container_PointerExited(object sender, PointerRoutedEventArgs e) {
            OnPointerExited(e, PointerPosition.InsideContainer);
        }

        internal void OnPointerEntered(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            _currentPointerId = e.Pointer.PointerId;
            var pointerPoint = e.GetCurrentPoint(renderCanvas);
            HandleToolEvent(tool => tool.HandleEntered(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.Data.SelectedLayer.RenderData.RenderTarget, pointerPos)));
        }

        internal void OnPointerMoved(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            var pointerPoint = e.GetCurrentPoint(renderCanvas);
            _viewModel.Data.UpdatePointerPos(pointerPoint.Position);
            HandleToolEvent(tool => tool.HandleMoved(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.Data.SelectedLayer.RenderData.RenderTarget, pointerPos)));
        }

        internal void OnPointerPressed(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            var pointerPoint = e.GetCurrentPoint(renderCanvas);
            HandleToolEvent(tool => tool.HandlePressed(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.Data.SelectedLayer.RenderData.RenderTarget, pointerPos)));
        }

        internal void OnPointerReleased(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            var pointerPoint = e.GetCurrentPoint(renderCanvas);
            HandleToolEvent(tool => tool.HandleReleased(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.Data.SelectedLayer.RenderData.RenderTarget, pointerPos)));
        }

        internal void OnPointerExited(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            _currentPointerId = null;
            var pointerPoint = e.GetCurrentPoint(renderCanvas);
            HandleToolEvent(tool => tool.HandleExited(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.Data.SelectedLayer.RenderData.RenderTarget, pointerPos)));
        }

        private void HandleToolEvent(Action<RenderBase> action) {
            if (_viewModel.Data.SelectedToolItem == null ||
                _viewModel.Data.SelectedLayer == null ||
                _viewModel.Data.SelectedLayer.RenderData == null ||
                _viewModel.Data.SelectedLayer.RenderData.RenderTarget == null) {
                GlobalMessageUtil.ShowError(
                    ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)),
                    message: nameof(Constants.I18n.Draft_SI_LayerNotAvailable),
                    key: nameof(Constants.I18n.Draft_SI_LayerNotAvailable),
                    isNeedLocalizer: true);
                return;
            }

            if (!_viewModel.Data.SelectedLayer.IsVisible) {
                GlobalMessageUtil.ShowWarning(
                    ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)),
                    message: nameof(Constants.I18n.Draft_SI_LayerLocked),
                    key: nameof(Constants.I18n.Draft_SI_LayerLocked),
                    isNeedLocalizer: true);
                return;
            }

            _selectedTool = _tool.GetTool(_viewModel.Data.SelectedToolItem.Type);
            if (_selectedTool == null) {
                // 还原光标
                this.ProtectedCursor = _originalInputCursor;
                return;
            }

            action(_selectedTool);
        }

        private void Container_PointerWheelChanged(object sender, PointerRoutedEventArgs e) {
            var modifiers = e.KeyModifiers;
            var properties = e.GetCurrentPoint(Scroll).Properties;
            double delta = properties.MouseWheelDelta;

            if (delta == 0) return;

            if (modifiers == Windows.System.VirtualKeyModifiers.Control) {
                float currentZoom = Scroll.ZoomFactor;
                float zoomMultiplier = (delta > 0) ? 1.1f : 0.9f;
                float targetZoom = currentZoom * zoomMultiplier;

                var mousePos = e.GetCurrentPoint(Scroll).Position; 
                PerformZoom(targetZoom, mousePos);

                e.Handled = true;
                return;
            }

            if (modifiers == Windows.System.VirtualKeyModifiers.Shift) {
                PerformScroll(-delta, 0);

                e.Handled = true;
                return;
            }
        }
        #endregion

        private RenderBase? _selectedTool;
        private readonly ToolManager _tool;
        private InkCanvasViewModel _viewModel = null!;
        private readonly InputCursor _originalInputCursor;
        private CanvasRenderTarget? _compositeTarget;
        private readonly TaskCompletionSource<bool> _isInited = new();
        private CanvasImageBrush? _gridBrush;
        private const int _gridSize = 20;
        private InkProjectSession _session = null!;
        private uint? _currentPointerId;
    }
}
