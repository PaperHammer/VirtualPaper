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
                if (op) RenderToCompositeTarget();
            }
            else if (_selectedTool is CropTool ct) {
                var op = ct.RestoreOriginalContent();
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
        }
        #endregion

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
                using (args.DrawingSession) {
                    args.DrawingSession.DrawImage(_compositeTarget);
                }
            }
        }

        internal void RenderToCompositeTarget() {
            using (var ds = _compositeTarget.CreateDrawingSession()) {
                ds.Clear(Colors.Transparent);
                // 逆序遍历，确保层级正确性
                for (int i = _viewModel.ConfigData.InkDatas.Count - 1; i >= 0; i--) {
                    var layer = _viewModel.ConfigData.InkDatas[i];
                    if (!layer.IsEnable || layer.RenderData == null) continue;
                    ds.DrawImage(layer.RenderData.RenderTarget);
                }
            }

            inkCanvas.Invalidate();
        }
        #endregion

        #region Scroll 
        private void Scroll_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e) {
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
            double availableWidth = Scroll.ViewportWidth;
            double availableHeight = Scroll.ViewportHeight;

            // 考虑边距
            double effectiveWidth = availableWidth - (Container.Margin.Left + Container.Margin.Right);
            double effectiveHeight = availableHeight - (Container.Margin.Top + Container.Margin.Bottom);

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
            Scroll.ChangeView(null, null, _viewModel.ConfigData.CanvasZoom);
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
                    op = st.RestoreOriginalContent();
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
                    op = ct.RestoreOriginalContent();
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
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            HandleToolEvent(tool => tool.OnPointerEntered(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.ConfigData.SelectedInkCanvas.RenderData, pointerPos)));
        }

        internal void OnPointerMoved(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            _viewModel.ConfigData.UpdatePointerPos(pointerPoint.Position);
            HandleToolEvent(tool => tool.OnPointerMoved(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.ConfigData.SelectedInkCanvas.RenderData, pointerPos)));
        }

        internal void OnPointerPressed(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            HandleToolEvent(tool => tool.OnPointerPressed(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.ConfigData.SelectedInkCanvas.RenderData, pointerPos)));
        }

        internal void OnPointerReleased(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            HandleToolEvent(tool => tool.OnPointerReleased(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.ConfigData.SelectedInkCanvas.RenderData, pointerPos)));
        }

        internal void OnPointerExited(PointerRoutedEventArgs e, PointerPosition pointerPos) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            HandleToolEvent(tool => tool.OnPointerExited(
                new CanvasPointerEventArgs(pointerPoint, _viewModel.ConfigData.SelectedInkCanvas.RenderData, pointerPos)));
        }

        private void HandleToolEvent(Action<Tool> action) {
            if (_viewModel.ConfigData.SelectedToolItem == null || 
                _viewModel.ConfigData.SelectedInkCanvas == null || 
                _viewModel.ConfigData.SelectedInkCanvas.RenderData == null ||
                _viewModel.ConfigData.SelectedInkCanvas.RenderData.RenderTarget == null) {
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
        private DateTime _lastRenderTime = DateTime.MinValue;        
    }
}
