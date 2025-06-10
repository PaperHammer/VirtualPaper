using System;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Input;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.EventArg;
using Workloads.Creation.StaticImg.Models.ToolItems;
using Workloads.Creation.StaticImg.Utils;
using Workloads.Creation.StaticImg.ViewModels;
using Workloads.Creation.StaticImg.Views.Tools;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Components {
    public sealed partial class InkCanvas : UserControl {
        public TaskCompletionSource<bool> IsInited => _isInited;

        public InkCanvas() {
            this.InitializeComponent();

            _originalInputCursor = this.ProtectedCursor ?? InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            _tool = new();
            _viewModel = new(MainPage.Instance.EntryFilePath, MainPage.Instance.RtFileType);
            RegisterTools();
        }

        #region children event
        private void SetupHandlers() {
            _viewModel.ConfigData.SizeChanged += (s, e) => {
                RebuildComposite();
                RenderToCompositeTarget();
            };
            //_viewModel.Ready += (s, e) => {
            //    //RenderToCompositeTarget();
            //    //RenderToCompositeTarget();
            //    IsInited.TrySetResult(true);
            //};
            _viewModel.ConfigData.SeletcedToolChanged += (s, e) => {
                //before
                HandleSelectionTool_Before();
                _selectedTool = _tool.GetTool(_viewModel.ConfigData.SelectedToolItem.Type);
                //after             
            };
            _viewModel.ConfigData.SeletcedLayerChanged += (s, e) => {
                HandleLayerChanged();
            };
            _viewModel.ConfigData.SelectedCropAspectClicked += (s, e) => {
                HandleCropAspectClicked(e);
            };
        }

        private void HandleCropAspectClicked(double e) {
            if (_selectedTool is CropTool ct) {
                ct.ApplyAspectRatio(e);
                RenderToCompositeTarget();
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
                var op = st.TryRestoreOriginalContent();
                if (op) RenderToCompositeTarget();
            }
            else if (_selectedTool is CropTool ct) {
                var op = ct.TryRestoreOriginalContent();
                if (op) RenderToCompositeTarget();
            }
        }

        private void RebuildComposite() {
            _compositeTarget = new CanvasRenderTarget(
                MainPage.Instance.SharedDevice,
                (float)_viewModel.ConfigData.Size.Width,
                (float)_viewModel.ConfigData.Size.Height,
                _viewModel.ConfigData.Size.Dpi,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                CanvasAlphaMode.Premultiplied);
            //_renderState = new RenderState(_compositeTarget);
            //_renderState.RenderComposite += (s, e) => {
            //    RenderToCompositeTarget();
            //};
        }
        #endregion

        //internal void RenderToCompositeTarget() {
        //    // 使用双缓冲避免闪烁
        //    if (_renderState.NeedsFullRedraw || _renderState.DirtyRects.Count == 0) {
        //        using (var ds = _compositeTarget.CreateDrawingSession()) {
        //            ds.Clear(Colors.Transparent);
        //            for (int i = _viewModel.ConfigData.InkDatas.Count - 1; i >= 0; i--) {
        //                var layer = _viewModel.ConfigData.InkDatas[i];
        //                if (!layer.IsEnable || layer.RenderData == null) continue;
        //                ds.DrawImage(layer.RenderData.RenderTarget);
        //            }
        //        }
        //        _renderState.NeedsFullRedraw = false;
        //    }
        //    else {
        //        // 增量重绘
        //        using (var ds = _compositeTarget.CreateDrawingSession()) {
        //            // 先合并所有脏矩形
        //            var mergedRect = _renderState.MergeDirtyRects();
        //            ds.FillRectangle(mergedRect, Colors.Transparent);

        //            // 分层绘制
        //            for (int i = _viewModel.ConfigData.InkDatas.Count - 1; i >= 0; i--) {
        //                var layer = _viewModel.ConfigData.InkDatas[i];
        //                if (!layer.IsEnable || layer.RenderData == null) continue;

        //                foreach (var dirtyRect in _renderState.DirtyRects) {
        //                    var sourceRect = new Rect(
        //                        dirtyRect.X - layer.RenderData.RenderTarget.Bounds.X,
        //                        dirtyRect.Y - layer.RenderData.RenderTarget.Bounds.Y,
        //                        dirtyRect.Width,
        //                        dirtyRect.Height);

        //                    ds.DrawImage(
        //                        layer.RenderData.RenderTarget,
        //                        dirtyRect,
        //                        sourceRect);
        //                }
        //            }
        //        }
        //        _renderState.DirtyRects.Clear();
        //    }

        //    inkCanvas.Invalidate();
        //}

        internal async Task SaveAsync() {
            await _viewModel.SaveAsync();
        }

        private void RegisterTools() {
            _tool.RegisterTool(ToolType.PaintBrush, new PaintBrushTool(_viewModel.ConfigData));
            _tool.RegisterTool(ToolType.Fill, new FillTool(_viewModel.ConfigData));
            _tool.RegisterTool(ToolType.Eraser, new EraserTool(_viewModel.ConfigData));
            _tool.RegisterTool(ToolType.Selection, new SelectionTool(_viewModel.ConfigData));
            _tool.RegisterTool(ToolType.Crop, new CropTool(_viewModel.ConfigData));

            foreach (var tool in _tool.GetAllTools()) {
                tool.SystemCursorChangeRequested += (s, e) => {
                    this.ProtectedCursor = e.Cursor ?? _originalInputCursor;
                };

                tool.RenderRequest += (s, e) => {
                    //if (e.Mode == RenderMode.PartialRegion && e.RenderData.DirtyRegion != Rect.Empty) {
                    //    RenderPartial(e.RenderData.DirtyRegion);
                    //    return;
                    //}
                    RenderToCompositeTarget();
                };
            }
        }

        #region inkcanvas and redner
        private async void InkingCanvas_Loaded(object sender, RoutedEventArgs e) {
            try {
                await _viewModel.LoadBasicOrInit();
                FitView();
                await _viewModel.LoadRenderDataAsync();
                await _viewModel.RenderDataLoaded.Task;
                RebuildComposite();
                RenderToCompositeTarget();
                SetupHandlers();
                IsInited.TrySetResult(true);
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.Log(LogType.Error, ex);
                MainPage.Instance.Bridge.GetNotify().ShowExp(ex);
            }
        }

        private void InkCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args) {
            if (_compositeTarget != null) {
                args.DrawingSession.DrawImage(_compositeTarget);
            }
        }

        //private void InkCanvas_RegionsInvalidated(CanvasVirtualControl sender, CanvasRegionsInvalidatedEventArgs args) {
        //    foreach (var region in args.InvalidatedRegions) {
        //        using (var ds = sender.CreateDrawingSession(region)) {
        //            //switch (_renderMode) {
        //            //    case RenderMode.FullRegion:
        //            //        DrawFullRegion(ds, region);
        //            //        break;
        //            //    case RenderMode.PartialRegion:
        //            //        DrawPartialRegion(ds, region);
        //            //        break;
        //            //    default:
        //            //        break;
        //            //}
        //            ds.DrawImage(_compositeTarget);
        //        }
        //    }
        //}

        //private void DrawFullRegion(CanvasDrawingSession ds, Rect region) {
        //    //for (int i = _viewModel.ConfigData.InkDatas.Count - 1; i >= 0; i--) {
        //    //    var layer = _viewModel.ConfigData.InkDatas[i];
        //    //    if (!layer.IsEnable || layer.RenderData == null) continue;

        //    //    if (Consts.TryGetIntersect(region, layer.RenderData.Bound, out var intersectRegion)) {
        //    //        ds.DrawImage(layer.RenderData.RenderTarget, intersectRegion);
        //    //    }
        //    //}
        //    ds.DrawImage(_compositeTarget);
        //}

        //private void DrawPartialRegion(CanvasDrawingSession ds, Rect region) {
        //    //for (int i = _viewModel.ConfigData.InkDatas.Count - 1; i >= 0; i--) {
        //    //    var layer = _viewModel.ConfigData.InkDatas[i];
        //    //    if (!layer.IsEnable || layer.RenderData == null) continue;

        //    //    if (Consts.TryGetIntersect(region, layer.RenderData.DirtyRegion, out var intersectRegion)) {
        //    //        ds.DrawImage(layer.RenderData.RenderTarget, intersectRegion);
        //    //    }
        //    //}
        //    ds.DrawImage(_compositeTarget);
        //}

        //private void RenderToCompositeTarget() {
        //    //_renderMode = RenderMode.FullRegion;
        //    RenderToCompositeTarget();
        //    //inkCanvas.Invalidate();
        //}

        //private void RenderPartial(Rect region) {
        //    _renderMode = RenderMode.PartialRegion;
        //    RenderToCompositeTarget();
        //    inkCanvas.Invalidate(region);
        //}

        internal void RenderToCompositeTarget() {
            using (var ds = _compositeTarget.CreateDrawingSession()) {
                ds.Clear(Colors.Transparent);
                // 确保层级的正确性
                for (int i = _viewModel.ConfigData.InkDatas.Count - 1; i >= 0; i--) {
                    var layer = _viewModel.ConfigData.InkDatas[i];
                    if (!layer.IsEnable || layer.RenderData == null) continue;
                    ds.DrawImage(layer.RenderData.RenderTarget);
                }
            }

            inkCanvas.Invalidate();
        }
        #endregion

        #region scroll 
        private void CanvasContainer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e) {
            // 检查是否为用户触发的滚动/缩放
            //if (e.IsInertial) {
            //    // 使用鼠标滚轮
            //    // 在 ScrollViewer 和其他支持直接操作的控件上使用键笔划
            //    // 调用启用了动画的 ChangeView 

            _viewModel.ConfigData.CanvasZoom = e.FinalView.ZoomFactor;
            //}
        }

        private void FitView() {
            // 获取可用显示区域
            double availableWidth = canvasContainer.ViewportWidth;
            double availableHeight = canvasContainer.ViewportHeight;

            // 考虑边距
            double effectiveWidth = availableWidth - (container.Margin.Left + container.Margin.Right);
            double effectiveHeight = availableHeight - (container.Margin.Top + container.Margin.Bottom);

            // 计算缩放比例
            double widthRatio = effectiveWidth / _viewModel.ConfigData.Size.Width;
            double heightRatio = effectiveHeight / _viewModel.ConfigData.Size.Height;

            // 选择较小的比例以确保完全显示
            double zoomFactor = Math.Min(widthRatio, heightRatio);

            // 应用缩放限制
            zoomFactor = Math.Max(Consts.MinZoomFactor, Math.Min(zoomFactor, Consts.MaxZoomFactor));

            // 应用缩放
            UpdateScrollViewerZoom((float)zoomFactor);
        }

        private void UpdateScrollViewerZoom(double value) {
            _viewModel.ConfigData.CanvasZoom = (float)value;
            canvasContainer.ChangeView(null, null, _viewModel.ConfigData.CanvasZoom);
        }

        private void CanvasContainer_PointerEntered(object sender, PointerRoutedEventArgs e) {
            OnPointerEntered(e);
        }

        private void CanvasContainer_PointerMoved(object sender, PointerRoutedEventArgs e) {
            OnPointerMoved(e);
        }

        private void CanvasContainer_PointerPressed(object sender, PointerRoutedEventArgs e) {
            OnPointerPressed(e);
        }

        private void CanvasContainer_PointerReleased(object sender, PointerRoutedEventArgs e) {
            OnPointerReleased(e);
        }

        private void CanvasContainer_PointerExited(object sender, PointerRoutedEventArgs e) {
            OnPointerExited(e);
        }

        private void BottomDataBarControl_ZoomComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var val = double.Parse((e.AddedItems[0] as string).TrimEnd('%')) / 100;
            UpdateScrollViewerZoom((float)val);
        }

        private void BottomDataBarControl_ZoomComboBoxTextSubmitted(object sender, ComboBoxTextSubmittedEventArgs e) {
            var val = double.Parse(e.Text.TrimEnd('%')) / 100;
            UpdateScrollViewerZoom((float)val);
        }

        private void BottomDataBarControl_ZoomInRequest(object sender, RoutedEventArgs e) {
            var newZoomFactor = Math.Max(Consts.MinZoomFactor,
                Consts.RoundToNearestFive(_viewModel.ConfigData.CanvasZoom) + Consts.GetSubStepSize(_viewModel.ConfigData.CanvasZoom));
            UpdateScrollViewerZoom(newZoomFactor);
        }

        private void BottomDataBarControl_ZoomOutRequest(object sender, RoutedEventArgs e) {
            var newZoomFactor = Math.Max(Consts.MinZoomFactor,
                Consts.RoundToNearestFive(_viewModel.ConfigData.CanvasZoom) - Consts.GetSubStepSize(_viewModel.ConfigData.CanvasZoom));
            UpdateScrollViewerZoom(newZoomFactor);
        }

        private void BottomDataBarControl_ZoomSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e) {
            var newZoomFactor = Consts.PercentToDeciaml((float)e.NewValue);
            UpdateScrollViewerZoom(newZoomFactor);
        }
        #endregion

        #region CanvasSet
        private void CanvasSet_OnValueChanged(object sender, ArcSize e) {
            _viewModel.ConfigData.Size = new(e.Width, e.Height, e.Dpi, e.Rebuild);
        }

        private void CanvasOperationBtn_Click(object sender, RoutedEventArgs e) {
            RebuildMode rm = (CanvasOperation)((Button)sender).Tag switch {
                CanvasOperation.RotateLeft => RebuildMode.RotateLeft,
                CanvasOperation.RotateRight => RebuildMode.RotateRight,
                CanvasOperation.FlipHorizontally => RebuildMode.FlipHorizontal,
                CanvasOperation.FlipVertically => RebuildMode.FlipVertical,
                _ => RebuildMode.None,
            };
            _viewModel.ConfigData.Size = new(
                _viewModel.ConfigData.Size.Width,
                _viewModel.ConfigData.Size.Height,
                _viewModel.ConfigData.Size.Dpi,
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
                    if (op) RenderToCompositeTarget();
                    break;
                case SeletionRequest.Cancel:
                    op = st.TryRestoreOriginalContent();
                    if (op) RenderToCompositeTarget();
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
                    if (op) RenderToCompositeTarget();
                    break;
                case CropRequest.Cancel:
                    op = ct.TryRestoreOriginalContent();
                    if (op) RenderToCompositeTarget();
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region Layer Mangaer
        private async void LayerManage_AddLayerRequest(object sender, EventArgs e) {
            var layer = await _viewModel.ConfigData.AddLayerAsync();
            await layer.RenderData.IsCompleted.Task;
            RenderToCompositeTarget();
        }

        private async void LayerManage_CopyLayerRequest(object sender, long e) {
            var layer = await _viewModel.ConfigData.CopyLayerAsync(e);
            await layer.RenderData.IsCompleted.Task;
            RenderToCompositeTarget();
        }

        private async void LayerManage_RenameLayerRequest(object sender, long e) {
            await _viewModel.ConfigData.RenameAsync(e);
        }

        private async void LayerManage_DeleteLayerRequest(object sender, long e) {
            await _viewModel.ConfigData.DeleteAsync(e);
            RenderToCompositeTarget();
        }

        private async void LayerManage_MoveLayerRequest(object sender, EventArgs e) {
            await _viewModel.ConfigData.SaveBasicAsync();
            RenderToCompositeTarget();
        }
        #endregion

        #region ColorPalette
        private async void ColorPalette_CustomeColorChanged(object sender, ColorChangeEventArgs e) {
            await _viewModel.ConfigData.UpdateCustomColorsAsync(e);
        }
        #endregion

        #region BottomBar
        private void BottomDataBarControl_FitViewRequest(object sender, RoutedEventArgs e) {
            FitView();
        }
        #endregion

        #region ui events
        internal new void OnPointerEntered(PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            HandleToolEvent(tool => tool.OnPointerEntered(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.ConfigData.SelectedInkCanvas.RenderData)));
        }

        internal new void OnPointerMoved(PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            _viewModel.ConfigData.UpdatePointerPos(pointerPoint.Position);
            HandleToolEvent(tool => tool.OnPointerMoved(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.ConfigData.SelectedInkCanvas.RenderData)));
        }

        internal new void OnPointerPressed(PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            HandleToolEvent(tool => tool.OnPointerPressed(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.ConfigData.SelectedInkCanvas.RenderData)));
        }

        internal new void OnPointerReleased(PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            HandleToolEvent(tool => tool.OnPointerReleased(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.ConfigData.SelectedInkCanvas.RenderData)));
        }

        internal new void OnPointerExited(PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            HandleToolEvent(tool => tool.OnPointerExited(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.ConfigData.SelectedInkCanvas.RenderData)));
        }

        private void HandleToolEvent(Action<Tool> action) {
            if (_viewModel.ConfigData.SelectedInkCanvas == null || _viewModel.ConfigData.SelectedToolItem == null) {
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Draft_SI_LayerNotAvailable), InfoBarType.Error, key: nameof(Constants.I18n.Draft_SI_LayerNotAvailable), isAllowDuplication: false);
                return;
            }

            if (!_viewModel.ConfigData.SelectedInkCanvas.IsEnable) {
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Draft_SI_LayerLocked), InfoBarType.Warning, key: nameof(Constants.I18n.Draft_SI_LayerLocked), isAllowDuplication: false);
                return;
            }

            _selectedTool = _tool.GetTool(_viewModel.ConfigData.SelectedToolItem.Type);
            if (_selectedTool == null) {
                // 还原光标
                this.ProtectedCursor = _originalInputCursor;
                return;
            }

            action(_selectedTool);
        }
        #endregion

        private Tool? _selectedTool;
        private readonly ToolManager _tool;
        private readonly InkCanvasViewModel _viewModel;
        private readonly InputCursor _originalInputCursor;
        private CanvasRenderTarget _compositeTarget;
        private readonly TaskCompletionSource<bool> _isInited = new();
        //private RenderMode _renderMode;
    }
}
