using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Shader;
using VirtualPaper.Shader.Models;
using VirtualPaper.UIComponent.Collection;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Input;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Debugging;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.Extensions;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.ToolItems;
using Workloads.Creation.StaticImg.Utils;
using Workloads.Creation.StaticImg.ViewModels;
using Workloads.Creation.StaticImg.Views.Tools;
using Workloads.Creation.StaticImg.Views.Tools.Effects;
using Workloads.Utils.DraftUtils.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Components {
    public sealed partial class InkCanvas : ArcUserControl {
        private static readonly TimeSpan EffectPreviewThrottleInterval = TimeSpan.FromMilliseconds(100);
        public TaskCompletionSource<bool> IsInited => _isInited;

        public InkCanvas() {
            this.InitializeComponent();
            _originalInputCursor = this.ProtectedCursor ?? InputSystemCursor.Create(InputSystemCursorShape.Arrow);

            _effectPreviewTimer = DispatcherQueue.CreateTimer();
            _effectPreviewTimer.Interval = EffectPreviewThrottleInterval;
            _effectPreviewTimer.IsRepeating = true;
            _effectPreviewTimer.Tick += EffectPreviewTimer_Tick;
            Unloaded += InkCanvas_Unloaded;
        }

        private void InkCanvas_Unloaded(object sender, RoutedEventArgs e) {
            CancelPendingEffectPreview();
            _tool?.CancelPendingOperations();
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
                Consts.InitData();
            }
            _viewModel = new InkCanvasViewModel(_session, context!);
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
            _tool.RegisterTool(ToolType.CanvasEffect, new EffectTool());

            foreach (var tool in _tool.GetAllTools()) {
                tool.SystemCursorChangeRequested += (s, e) => {
                    this.ProtectedCursor = e.Cursor ?? _originalInputCursor;
                };
                tool.RenderRequest += (s, e) => {
                    UpdateStrokeCacheDebugPanel(e.StrokeCacheDebugInfo);
                    RenderToCompositeTarget(e.Mode, e.Region);
                };
                tool.OnceRenderCompleted += (s, e) => {
                    OnOnceRenderCompleted();
                };
                tool.FatalErrorOccurred += (s, e) => {
                    OnFatalErrorOccurred(s, e);
                };
            }

            CanvasEffect.EffectPreviewRequested += OnEffectPreviewRequested;
        }

        private async void OnFatalErrorOccurred(object? s, Exception e) {
            ArcLog.GetLogger<InkCanvas>().Fatal(e.Message);
            GlobalMessageUtil.ShowError(message: e.Message);
            await _viewModel.SaveAsync(true);
        }

        internal async Task<bool> SaveAsync() {
            return await _viewModel.SaveAsync();
        }

        internal async Task UpdateRecentUsedAsync(string filePath) {
            await _viewModel.UpdateRecentUsedAsync(filePath);
        }

        internal async Task<string?> ExportAsync(ExportDataStaticImg data, CancellationToken token = default) {
            var size = _viewModel.Data.CanvasSize.ToSize();
            return await _compositeTarget.ExportAsync(size, data, token);
        }

        #region children event
        private void SetupHandlers() {
            _viewModel.Data.SizeChanged += (s, e) => {
                RenderToCompositeTarget(RenderMode.FullRegion);
            };
            _viewModel.Data.SeletcedToolChanged += (s, e) => {
                //before
                TryRestore();
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

        private void TryRestore() {
            if (_selectedTool is SelectionTool st) {
                var op = st.RestoreOriginalContent();
                if (op) RenderToCompositeTarget(RenderMode.FullRegion);
            }
            else if (_selectedTool is CropTool ct) {
                var op = ct.RestoreOriginalContent();
                if (op) RenderToCompositeTarget(RenderMode.FullRegion);
            }
            else if (_selectedTool is EffectTool et) {
                CancelPendingEffectPreview();
                et.Cancel();
                CanvasEffect.Restore();
                UnsubscribeCurrentEffectPanel();
                effectPanelHost.Visibility = Visibility.Collapsed;
            }
        }

        private void RebuildCompositeIfNeeded() {
            float requiredWidth = (float)_viewModel.Data.CanvasSize.Width;
            float requiredHeight = (float)_viewModel.Data.CanvasSize.Height;

            if (_compositeTarget == null ||
                _compositeTarget.Size.Width < requiredWidth ||
                _compositeTarget.Size.Height < requiredHeight) {
                DebugUtil.Output("RebuildComposite triggered: Expanding target");

                // 每次多分配 20% 的空间，避免频繁重建
                float maximumEdge = Consts.GetMaximumCanvasEdge(_viewModel.Data.CanvasSize.Dpi);
                float newWidth = Math.Min(maximumEdge, Math.Max(requiredWidth * 1.2f, requiredWidth));
                float newHeight = Math.Min(maximumEdge, Math.Max(requiredHeight * 1.2f, requiredHeight));

                _compositeTarget?.Dispose();
                _compositeTarget = new CanvasRenderTarget(
                    InkProjectSession.SharedDevice,
                    newWidth,
                    newHeight,
                    _viewModel.Data.CanvasSize.Dpi,
                    _session.SharedFormat,
                    _session.SharedAlphaMode);
                _tool.RefreshToolRenderData(_viewModel.Data.CanvasSize);
            }
        }

        #endregion

        #region redner
        private async void RenderCanvas_Loaded(object sender, RoutedEventArgs e) {
            try {
                if (IsInited.Task.IsCompleted) return;
                await _viewModel.LoadAsync();
                FitView();
                RenderToCompositeTarget(RenderMode.FullRegion);
                SetupHandlers();
                IsInited.TrySetResult(true);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }

        private void RenderCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args) {
            if (_compositeTarget == null) return;

            var destRect = new Rect(0, 0, _viewModel.Data.CanvasSize.Width, _viewModel.Data.CanvasSize.Height);
            // 只从 _compositeTarget 的左上角，截取逻辑大小的画面画到屏幕上
            Rect sourceRect = destRect;
            using (args.DrawingSession) {
                args.DrawingSession.DrawImage(_compositeTarget, destRect, sourceRect);
                DrawStrokeCacheDebugOverlay(args.DrawingSession);
            }
        }

        private void DrawStrokeCacheDebugOverlay(CanvasDrawingSession ds) {
            if (!StaticImgDebugSwitches.ShowStrokeCacheOverlay ||
                _strokeCacheDebugInfo is not { } debugInfo) return;

            if (debugInfo.CacheBounds != Rect.Empty)
                ds.DrawRectangle(debugInfo.CacheBounds, Color.FromArgb(255, 0, 200, 255), 2f);

            if (debugInfo.UpdatedCacheBounds != Rect.Empty)
                ds.DrawRectangle(debugInfo.UpdatedCacheBounds, Color.FromArgb(255, 255, 165, 0), 4f);

            ds.DrawRectangle(debugInfo.DirtyBounds, Color.FromArgb(255, 255, 0, 255), 3f);
            ds.DrawRectangle(debugInfo.ActiveStrokeBounds, Color.FromArgb(255, 80, 255, 80), 3f);
        }

        private void UpdateStrokeCacheDebugPanel(StrokeCacheDebugInfo? debugInfo) {
            _strokeCacheDebugInfo = debugInfo;
            if (!StaticImgDebugSwitches.ShowStrokeCacheOverlay || debugInfo == null) {
                strokeCacheDebugPanel.Visibility = Visibility.Collapsed;
                return;
            }

            strokeCacheDebugPanel.Visibility = Visibility.Visible;
            strokeCacheDebugSummary.Text =
                $"Cache {(debugInfo.CacheBounds == Rect.Empty ? "none" : "allocated")}  |  " +
                $"Updated {(debugInfo.UpdatedCacheBounds == Rect.Empty ? "no" : "yes")}  |  " +
                $"Points {debugInfo.ActivePointCount}";
            strokeCacheDebugDetails.Text =
                $"Dirty ({debugInfo.DirtyBounds.X:F0},{debugInfo.DirtyBounds.Y:F0}) " +
                $"{debugInfo.DirtyBounds.Width:F0}×{debugInfo.DirtyBounds.Height:F0}  |  " +
                $"Active {debugInfo.ActiveStrokeBounds.Width:F0}×{debugInfo.ActiveStrokeBounds.Height:F0}  |  " +
                $"Cache {debugInfo.CacheBounds.Width:F0}×{debugInfo.CacheBounds.Height:F0}  |  " +
                "Block 32  |  Overlap 2";
        }

        private void OnOnceRenderCompleted() {
            _viewModel.Data.SelectedLayer.RenderData.HandleOnceRenderCompleted();
        }

        private void RenderToCompositeTarget(RenderMode mode, Rect region = default) {
            RebuildCompositeIfNeeded();
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

        /// <summary>
        /// 通用缩放方法
        /// </summary>
        /// <param name="targetZoom">目标缩放比例</param>
        /// <param name="contentAnchor">内容坐标系上的锚点（鼠标在 container 上的位置）。如果为 null，则以当前视口中心为基准。</param>
        /// <param name="viewportAnchor">锚点在 ScrollViewer 视口中的位置；与 contentAnchor 成对传入。</param>
        /// <param name="disableAnimation">是否禁用动画（Slider拖动建议禁用，按钮点击建议启用）</param>
        private void PerformZoom(
            float targetZoom,
            Point? contentAnchor = null,
            Point? viewportAnchor = null,
            bool disableAnimation = false) {
            float currentZoom = scroll.ZoomFactor;
            targetZoom = Math.Clamp(targetZoom, (float)Consts.MinZoomFactor, (float)Consts.MaxZoomFactor);

            if (Math.Abs(targetZoom - currentZoom) < 1e-5) return;

            Point effectiveViewportAnchor;
            Point effectiveContentAnchor;
            if (contentAnchor.HasValue && viewportAnchor.HasValue) {
                effectiveContentAnchor = contentAnchor.Value;
                effectiveViewportAnchor = viewportAnchor.Value;
            }
            else {
                effectiveViewportAnchor = new Point(
                    scroll.ViewportWidth / 2.0,
                    scroll.ViewportHeight / 2.0);
                effectiveContentAnchor = new Point(
                    (scroll.HorizontalOffset + effectiveViewportAnchor.X) / currentZoom - container.Margin.Left,
                    (scroll.VerticalOffset + effectiveViewportAnchor.Y) / currentZoom - container.Margin.Top);
            }

            Point newOffset = CalculateZoomOffset(
                targetZoom,
                effectiveContentAnchor,
                effectiveViewportAnchor,
                container.Margin.Left,
                container.Margin.Top);
            scroll.ChangeView(newOffset.X, newOffset.Y, targetZoom, disableAnimation);
        }

        /// <summary>
        /// 计算保持指定内容点停留在同一视口位置所需的新滚动偏移。
        /// 屏幕显示位置满足：视口位置 = (画布位置 + 内容外边距) * 缩放比例 - 滚动偏移。
        /// 已知画布锚点、目标缩放比例和希望保持不变的视口锚点后，变换公式得到：
        /// 滚动偏移 = (画布位置 + 内容外边距) * 目标缩放比例 - 视口位置。
        /// 因此缩放后将 ScrollViewer 调整到该偏移，可以让鼠标指向的画布内容仍停留在鼠标下方。
        /// </summary>
        internal static Point CalculateZoomOffset(
            float targetZoom,
            Point contentAnchor,
            Point viewportAnchor,
            double contentMarginLeft,
            double contentMarginTop) => new(
                (contentAnchor.X + contentMarginLeft) * targetZoom - viewportAnchor.X,
                (contentAnchor.Y + contentMarginTop) * targetZoom - viewportAnchor.Y);

        private static float GetWheelZoomTarget(float currentZoom, double wheelDelta) {
            int detentCount = Math.Max(1, (int)Math.Round(Math.Abs(wheelDelta) / 120d));
            double targetZoom = currentZoom;
            for (int i = 0; i < detentCount; i++) {
                targetZoom += wheelDelta > 0
                    ? Consts.GetAddStepSize(targetZoom)
                    : -Consts.GetSubStepSize(targetZoom);
                targetZoom = Math.Clamp(targetZoom, Consts.MinZoomFactor, Consts.MaxZoomFactor);
            }
            return (float)targetZoom;
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
        private async void CanvasSet_OnValueCommited(object sender, ArcSize e) {
            bool isApplied = await _viewModel.Data.ApplyResizeOrScaleAsync(e);
            if (!isApplied) {
                CanvasSet.RestoreCurrentSize();
            }
        }

        private async void CanvasOperationBtn_Click(object sender, CanvasOperation e) {
            await _viewModel.Data.ApplyRotateOrFlipAsync(e);
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

        #region CanvasEffect
        private EffectPanelBase? _currentEffectPanel;

        private void CanvasEffect_Cancel(object sender, RoutedEventArgs e) {
            if (_selectedTool is not EffectTool et) return;

            CancelPendingEffectPreview();
            et.Cancel();
            UnsubscribeCurrentEffectPanel();
            effectPanelHost.Visibility = Visibility.Collapsed;
            CanvasEffect.ClickedEffectId = null;
        }

        private void CanvasEffect_Commit(object sender, RoutedEventArgs e) {
            if (_selectedTool is not EffectTool et) return;

            et.Commit(TakePendingEffectParams());
            UnsubscribeCurrentEffectPanel();
            effectPanelHost.Visibility = Visibility.Collapsed;
            CanvasEffect.ClickedEffectId = null;
        }

        private void UnsubscribeCurrentEffectPanel() {
            if (_currentEffectPanel != null) {
                _currentEffectPanel.ParamsChanged -= OnEffectPanelParamsChanged;
                _currentEffectPanel = null;
            }
        }

        private void OnEffectPreviewRequested(object? sender, string effectId) {
            if (_selectedTool is not EffectTool et) return;

            CancelPendingEffectPreview();
            et.Cancel(); // 先取消之前的预览，避免重复叠加效果

            var shaderType = EffectMap.ToShaderType(effectId);
            if (shaderType == ShaderType.None) {
                effectPanelHost.Visibility = Visibility.Collapsed;
                CanvasEffect.ClickedEffectId = null;
                return;
            }

            // 先显示面板获取初始参数
            var panel = GetEffectPanel(shaderType);
            var initialParams = panel?.Params ?? new EffectParams { Value = 0f, Value2 = 255f, Dpi = 96f };

            et.StartPreview(shaderType, initialParams);
            ShowEffectPanel(shaderType);
        }

        private void ShowEffectPanel(ShaderType shaderType) {
            UnsubscribeCurrentEffectPanel();

            var panel = GetEffectPanel(shaderType);
            if (panel == null) return;

            panel.Reset();

            _currentEffectPanel = panel;
            panel.ParamsChanged += OnEffectPanelParamsChanged;

            // 一次性效果（无参数）：立即应用，面板仍显示预览提示
            if (panel.IsOneShot && _selectedTool is EffectTool etOneShot)
                etOneShot.UpdateParams(panel.Params);

            effectPanelHost.Visibility = Visibility.Visible;
        }

        private void OnEffectPanelParamsChanged(object? sender, EffectParams p) {
            if (_selectedTool is not EffectTool { IsPreviewing: true }) return;

            _pendingEffectParams = p;
            if (_effectPreviewTimer.IsRunning) return;

            ApplyPendingEffectPreview();
            _effectPreviewTimer.Start();
        }

        private void EffectPreviewTimer_Tick(DispatcherQueueTimer sender, object args) {
            if (_pendingEffectParams.HasValue)
                ApplyPendingEffectPreview();
            else
                sender.Stop();
        }

        private EffectParams? TakePendingEffectParams() {
            _effectPreviewTimer.Stop();
            EffectParams? pendingParams = _pendingEffectParams;
            _pendingEffectParams = null;
            return pendingParams;
        }

        private void ApplyPendingEffectPreview() {
            EffectParams? pendingParams = _pendingEffectParams;
            _pendingEffectParams = null;
            if (pendingParams.HasValue && _selectedTool is EffectTool { IsPreviewing: true } et)
                et.UpdateParams(pendingParams.Value);
        }

        private void CancelPendingEffectPreview() {
            _effectPreviewTimer.Stop();
            _pendingEffectParams = null;
        }

        private EffectPanelBase? GetEffectPanel(ShaderType shaderType) => shaderType switch {
            ShaderType.Exposure => Exposure,
            ShaderType.Brightness => Brightness,
            ShaderType.Saturation => Saturation,
            ShaderType.HueRotation => HueRotation,
            ShaderType.Contrast => Contrast,
            ShaderType.TemperatureAndTint => TemperatureTint,
            ShaderType.HighlightsAndShadows => HighlightsShadows,
            ShaderType.Grayscale => Empty,
            ShaderType.Invert => Empty,
            ShaderType.Sepia => Empty,
            ShaderType.GaussianBlur => Blur,
            ShaderType.DirectionalBlur => DirectionalBlur,
            ShaderType.Sharpen => Sharpen,
            ShaderType.Vignette => Vignette,
            ShaderType.Emboss => Emboss,
            ShaderType.Posterize => Posterize,
            ShaderType.Shadow => Shadow,
            ShaderType.ThresholdEffect => Threshold,
            ShaderType.RippleEffect => Ripple,
            ShaderType.DisplacementLiquefactionEffect => DisplacementLiquefaction,
            ShaderType.Straighten => Straighten,
            ShaderType.Colouring => Colouring,
            ShaderType.EdgeDetection => EdgeDetection,
            ShaderType.Morphology => Morphology,
            ShaderType.LuminanceToAlpha => LuminanceToAlpha,
            ShaderType.GammaTransfer => GammaTransfer,
            ShaderType.HSB => HSB,
            ShaderType.Fog => Fog,
            ShaderType.Glass => Glass,
            ShaderType.ChromaKey => ChromaKey,
            ShaderType.Noise => Noise,
            ShaderType.Bloom => Bloom,
            ShaderType.Glow => Glow,
            ShaderType.BlendMultiply => Blend,
            ShaderType.BlendScreen => Blend,
            ShaderType.BlendOverlay => Blend,
            ShaderType.BlendSoftLight => Blend,
            _ => null,
        };

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
                    message: nameof(Constants.I18n.Draft_SI_LayerNotAvailable),
                    key: nameof(Constants.I18n.Draft_SI_LayerNotAvailable),
                    isNeedLocalizer: true);
                return;
            }

            if (!_viewModel.Data.SelectedLayer.IsVisible) {
                GlobalMessageUtil.ShowWarning(
                    message: nameof(Constants.I18n.Draft_SI_LayerLocked),
                    key: nameof(Constants.I18n.Draft_SI_LayerLocked),
                    isNeedLocalizer: true);
                return;
            }

            RenderBase? nextTool = _tool.GetTool(_viewModel.Data.SelectedToolItem.Type);
            _selectedTool = nextTool;
            if (nextTool == null) {
                // 还原光标
                this.ProtectedCursor = _originalInputCursor;
                return;
            }

            if (_selectedTool is not CanvasPathDrawer)
                UpdateStrokeCacheDebugPanel(null);

            action(nextTool);
        }

        private void Container_PointerWheelChanged(object sender, PointerRoutedEventArgs e) {
            var modifiers = e.KeyModifiers;
            PointerPoint contentPointer = e.GetCurrentPoint(container);
            double delta = contentPointer.Properties.MouseWheelDelta;

            if (delta == 0) return;

            if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control)) {
                Point viewportAnchor = e.GetCurrentPoint(scroll).Position;
                float targetZoom = GetWheelZoomTarget(scroll.ZoomFactor, delta);
                PerformZoom(
                    targetZoom,
                    contentPointer.Position,
                    viewportAnchor,
                    disableAnimation: true);
                e.Handled = true;
                return;
            }

            if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift)) {
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
        private readonly DispatcherQueueTimer _effectPreviewTimer;
        private EffectParams? _pendingEffectParams;
        private StrokeCacheDebugInfo? _strokeCacheDebugInfo;
    }
}
