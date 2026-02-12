using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Logging;
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

            //this.Bindings.Update();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
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
                HandleSelectionTool_Before();
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

        private void HandleSelectionTool_Before() {
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

        internal void RenderToCompositeTarget(RenderMode mode, Rect region = default) {
            if (_compositeTarget == null) return;

            var layers = _viewModel.Data.ActiveLayers;
            using (var ds = _compositeTarget.CreateDrawingSession()) {
                ds.Blend = CanvasBlend.Copy;
                if (mode == RenderMode.FullRegion) {
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
            // 逆序遍历
            foreach (var layer in layers.Reverse()) {
                if (layer.RenderData == null) continue;
                ds.DrawImage(layer.RenderData.RenderTarget);
            }
        }

        private void PartialRender(IEnumerable<LayerInfo> layers, CanvasDrawingSession ds, Rect region) {
            // 禁用抗锯齿（开启抗锯齿的局部刷新会导致刷新区域边界出现细线）
            // 抗锯齿算法将由各工具自己实现      
            ds.Antialiasing = CanvasAntialiasing.Aliased;

            foreach (var layer in layers.Reverse()) {
                if (layer.RenderData == null) continue;

                var visibleRect = region.IntersectRect(layer.RenderData.RenderTarget.Bounds);
                if (!visibleRect.IsEmpty) {
                    ds.DrawImage(layer.RenderData.RenderTarget, visibleRect, visibleRect);
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
            // 检查是否为用户触发的滚动/缩放
            //if (e.IsInertial) {
            //    // 使用鼠标滚轮
            //    // 在 ScrollViewer 和其他支持直接操作的控件上使用键笔划
            //    // 调用启用了动画的 ChangeView 

            _viewModel.Data.CanvasZoom = e.FinalView.ZoomFactor;
            //}
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
            UpdateScrollViewerZoom((float)zoomFactor);
        }

        private void UpdateScrollViewerZoom(double value) {
            _viewModel.Data.CanvasZoom = (float)value;
            Scroll.ChangeView(null, null, _viewModel.Data.CanvasZoom);
        }

        private void BottomDataBarControl_ZoomComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems[0] is string textValue) {
                var val = double.Parse(textValue.TrimEnd('%')) / 100;
                UpdateScrollViewerZoom((float)val);
            }
        }

        private void BottomDataBarControl_ZoomComboBoxTextSubmitted(object sender, ComboBoxTextSubmittedEventArgs e) {
            var val = double.Parse(e.Text.TrimEnd('%')) / 100;
            UpdateScrollViewerZoom((float)val);
        }

        private void BottomDataBarControl_ZoomInRequest(object sender, RoutedEventArgs e) {
            var newZoomFactor = Math.Max(Consts.MinZoomFactor,
                Consts.RoundToNearestFive(_viewModel.Data.CanvasZoom) + Consts.GetSubStepSize(_viewModel.Data.CanvasZoom));
            UpdateScrollViewerZoom(newZoomFactor);
        }

        private void BottomDataBarControl_ZoomOutRequest(object sender, RoutedEventArgs e) {
            var newZoomFactor = Math.Max(Consts.MinZoomFactor,
                Consts.RoundToNearestFive(_viewModel.Data.CanvasZoom) - Consts.GetSubStepSize(_viewModel.Data.CanvasZoom));
            UpdateScrollViewerZoom(newZoomFactor);
        }

        private void BottomDataBarControl_ZoomSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e) {
            Debug.WriteLine("-" + e.NewValue);
            var newZoomFactor = Consts.PercentToDeciaml((float)e.NewValue);
            Debug.WriteLine("--" + newZoomFactor);
            UpdateScrollViewerZoom(newZoomFactor);
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
        private void LayerManage_AddLayerRequest(object sender, EventArgs e) {
            _viewModel.Data.AddLayer();
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
        //private float _opacity = 1f;
    }
}
