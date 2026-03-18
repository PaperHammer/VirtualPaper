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
            _tool = new ToolManager(_viewModel);
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
                tool.FatalErrorOccurred += (s, e) => {
                    OnFatalErrorOccurred(s, e);
                };
            }
        }

        private async void OnFatalErrorOccurred(object s, Exception e) {
            ArcLog.GetLogger<InkCanvas>().Fatal(e.Message);
            GlobalMessageUtil.ShowError(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), message: e.Message);
            await _viewModel.SaveAsync(true);
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
                ct.RestoreOriginalContent();
                ct.ApplyAspectRatio(e);
            }
        }

        private void HandleLayerChanged() {
            _tool.RefreshToolRenderData(_viewModel.Data.CanvasSize);
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
            _tool.RefreshToolRenderData(_viewModel.Data.CanvasSize);
        }
        #endregion

        #region redner
        private async void RenderCanvas_Loaded(object sender, RoutedEventArgs e) {
            try {
                if (IsInited.Task.IsCompleted) return;
                await _viewModel.LoadAsync();                
                //_tool.RefreshToolRenderData();
                FitView();
                RebuildComposite();
                RenderToCompositeTarget(RenderMode.FullRegion);
                SetupHandlers();
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

            lock (_compositeTarget) {
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
        }

        private void FullRender(IEnumerable<LayerInfo> layers, CanvasDrawingSession ds) {
            using (var batch = ds.CreateSpriteBatch()) {
                foreach (var layer in layers.Reverse()) {
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

        #region scroll
        private void Scroll_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e) {
            _viewModel.Data.CanvasZoom = e.FinalView.ZoomFactor;
        }

        private void FitView() {
            // 获取可用显示区域
            double availableWidth = scroll.ViewportWidth;
            double availableHeight = scroll.ViewportHeight;

            // 考虑边距
            double effectiveWidth = availableWidth - (container.Margin.Left + container.Margin.Right);
            double effectiveHeight = availableHeight - (container.Margin.Top + container.Margin.Bottom);

            // 计算缩放比例
            double widthRatio = effectiveWidth / _viewModel.Data.CanvasSize.Width;
            double heightRatio = effectiveHeight / _viewModel.Data.CanvasSize.Height;

            // 选择较小的比例以确保完全显示
            double zoomFactor = Math.Min(widthRatio, heightRatio);

            // 应用缩放限制
            zoomFactor = Math.Max(Consts.MinZoomFactor, Math.Min(zoomFactor, Consts.MaxZoomFactor));

            // 应用缩放
            PerformZoom((float)zoomFactor);
        }

        // todo：按照目标点位缩放；空白区域响应水平滚动
        /// <summary>
        /// 通用缩放方法
        /// </summary>
        /// <param name="targetZoom">目标缩放比例</param>
        /// <param name="contentAnchor">内容坐标系上的锚点（鼠标在 container 上的位置）。如果为 null，则以当前视口中心为基准。</param>
        /// <param name="disableAnimation">是否禁用动画（Slider拖动建议禁用，按钮点击建议启用）</param>
        private void PerformZoom(float targetZoom, Point? contentAnchor = null, bool disableAnimation = false) {
            float currentZoom = scroll.ZoomFactor;
            targetZoom = Math.Clamp(targetZoom, (float)Consts.MinZoomFactor, (float)Consts.MaxZoomFactor);

            if (Math.Abs(targetZoom - currentZoom) < 1e-5) return;

            if (contentAnchor.HasValue) {
                // 先计算原来的锚点在视口中的坐标
                double oldAnchorViewportX = contentAnchor.Value.X * currentZoom - scroll.HorizontalOffset;
                double oldAnchorViewportY = contentAnchor.Value.Y * currentZoom - scroll.VerticalOffset;

                // 再在新缩放下，保证锚点还在同一个视口坐标
                double newOffsetX = contentAnchor.Value.X * targetZoom - oldAnchorViewportX;
                double newOffsetY = contentAnchor.Value.Y * targetZoom - oldAnchorViewportY;

                scroll.ChangeView(newOffsetX, newOffsetY, targetZoom, disableAnimation);
            }
            else {
                double viewportCenterX = scroll.ViewportWidth / 2.0;
                double viewportCenterY = scroll.ViewportHeight / 2.0;
                var centerPointInScroll = new Point(viewportCenterX, viewportCenterY);

                var transform = scroll.TransformToVisual(container);
                Point centerPointInCanvas = transform.TransformPoint(centerPointInScroll);

                double oldAnchorViewportX = centerPointInCanvas.X * currentZoom - scroll.HorizontalOffset;
                double oldAnchorViewportY = centerPointInCanvas.Y * currentZoom - scroll.VerticalOffset;

                double newOffsetX = centerPointInCanvas.X * targetZoom - oldAnchorViewportX;
                double newOffsetY = centerPointInCanvas.Y * targetZoom - oldAnchorViewportY;

                scroll.ChangeView(newOffsetX, newOffsetY, targetZoom, disableAnimation);
            }
        }

        /// <summary>
        /// 通用滚动方法
        /// </summary>
        /// <param name="deltaX">水平滚动量</param>
        /// <param name="deltaY">垂直滚动量</param>
        private void PerformScroll(double deltaX, double deltaY) {
            double newHorizontalOffset = scroll.HorizontalOffset + deltaX;
            double newVerticalOffset = scroll.VerticalOffset + deltaY;

            scroll.ChangeView(newHorizontalOffset, newVerticalOffset, null, false);
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

        private void CanvasOperationBtn_Click(object sender, CanvasOperation e) {
            RebuildMode rm = e switch {
                CanvasOperation.RotateLeft => RebuildMode.RotateLeft,
                CanvasOperation.RotateRight => RebuildMode.RotateRight,
                CanvasOperation.FlipHorizontally => RebuildMode.FlipHorizontal,
                CanvasOperation.FlipVertically => RebuildMode.FlipVertical,
                _ => RebuildMode.None,
            };
            _viewModel.Data.CanvasSize = new ArcSize(
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

            switch (sr) {
                case SeletionRequest.Commit:
                    st.CommitSelection();
                    break;
                case SeletionRequest.Cancel:
                    st.RestoreOriginalContent();
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

            switch (cr) {
                case CropRequest.Commit:
                    ct.CommitSelection();
                    break;
                case CropRequest.Cancel:
                    ct.RestoreOriginalContent();
                    _viewModel.Data.SeletcedAspectItem = null;
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
            e.Handled = true;
        }

        // move 会先被外层的 scroll 捕获并触发
        private void Container_PointerMoved(object sender, PointerRoutedEventArgs e) {
            OnPointerMoved(e, PointerPosition.InsideCanvas);
            e.Handled = true;
        }

        private void Container_PointerPressed(object sender, PointerRoutedEventArgs e) {
            this.Focus(FocusState.Programmatic); // 确保点击画布时获得焦点，避免任然被其他控件占用
            OnPointerPressed(e, PointerPosition.InsideCanvas);
            e.Handled = true;
        }

        private void Container_PointerReleased(object sender, PointerRoutedEventArgs e) {
            OnPointerReleased(e, PointerPosition.InsideCanvas);
            e.Handled = true;
        }

        private void Container_PointerExited(object sender, PointerRoutedEventArgs e) {
            OnPointerExited(e, PointerPosition.InsideContainer);
            e.Handled = true;
        }

        internal void OnPointerEntered(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            var pointerPoint = e.GetCurrentPoint(renderCanvas);
            HandleToolEvent(tool => tool.HandleEntered(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.Data.SelectedLayer.RenderData.RenderTarget, pointerPos, _viewModel.Data.SelectedLayer.Tag)));
        }

        internal void OnPointerMoved(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            var pointerPoint = e.GetCurrentPoint(renderCanvas);
            _viewModel.Data.UpdatePointerPos(pointerPoint.Position);
            HandleToolEvent(tool => tool.HandleMoved(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.Data.SelectedLayer.RenderData.RenderTarget, pointerPos, _viewModel.Data.SelectedLayer.Tag)));
        }

        internal void OnPointerPressed(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            var pointerPoint = e.GetCurrentPoint(renderCanvas);
            HandleToolEvent(tool => tool.HandlePressed(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.Data.SelectedLayer.RenderData.RenderTarget, pointerPos, _viewModel.Data.SelectedLayer.Tag)));
        }

        internal void OnPointerReleased(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            var pointerPoint = e.GetCurrentPoint(renderCanvas);
            HandleToolEvent(tool => tool.HandleReleased(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.Data.SelectedLayer.RenderData.RenderTarget, pointerPos, _viewModel.Data.SelectedLayer.Tag)));
        }

        internal void OnPointerExited(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            var pointerPoint = e.GetCurrentPoint(renderCanvas);
            HandleToolEvent(tool => tool.HandleExited(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.Data.SelectedLayer.RenderData.RenderTarget, pointerPos, _viewModel.Data.SelectedLayer.Tag)));
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
            var pointerPoint = e.GetCurrentPoint(container);
            var properties = pointerPoint.Properties;
            double delta = properties.MouseWheelDelta;

            if (delta == 0) return;

            if (modifiers == Windows.System.VirtualKeyModifiers.Shift) {
                e.Handled = true;
                PerformScroll(-delta, 0);
                return;
            }
        }

        #endregion

        private RenderBase? _selectedTool;
        private ToolManager _tool = null!;
        private InkCanvasViewModel _viewModel = null!;
        private readonly InputCursor _originalInputCursor;
        private CanvasRenderTarget? _compositeTarget;
        private readonly TaskCompletionSource<bool> _isInited = new();
        private CanvasImageBrush? _gridBrush;
        private const int _gridSize = 20;
        private InkProjectSession _session = null!;
    }
}
