using System;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils.ArcEventArgs;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Models.ToolItemUtil;
using Workloads.Creation.StaticImg.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Components {
    public sealed partial class InkCanvas : UserControl {
        public TaskCompletionSource<bool> IsReady => _isReady;

        public InkCanvas() {
            _viewModel = new(MainPage.Instance.EntryFilePath, MainPage.Instance.RtFileType);
            SetupHandlers();

            _compositeTarget = new CanvasRenderTarget(
                MainPage.Instance.SharedDevice,
                _viewModel.BasicData.Size.Width,
                _viewModel.BasicData.Size.Height,
                _viewModel.BasicData.Size.Dpi,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                CanvasAlphaMode.Premultiplied);

            this.InitializeComponent();
        }

        private void SetupHandlers() {
            _viewModel.RequestFullRender += (s, e) => {
                RebuildCompositeTarget();
                IsReady.TrySetResult(true);
            };
            _viewModel.TryCommitSelectionArea += (s, e) => {
                if (_selectedTool is SelectionTool st) {
                    st?.TryCommitSelection();
                }
            };
        }

        private void RebuildCompositeTarget() {
            using (var ds = _compositeTarget.CreateDrawingSession()) {
                ds.Clear(Colors.Transparent);
                // 确保层级的正确性
                for (int i = _viewModel.BasicData.InkDatas.Count - 1; i >= 0; i--) {
                    var layer = _viewModel.BasicData.InkDatas[i];
                    if (!layer.IsEnable || layer.Render == null) continue;
                    ds.DrawImage(layer.Render.RenderTarget);
                }
            }

            inkCanvas.Invalidate();
        }

        internal async Task SaveAsync() {
            await _viewModel.SaveAsync();
        }

        #region init
        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            RenderWorkerground();
        }

        private void RenderWorkerground() {
            int gridSize = 10; // 网格间距
            var color1 = Colors.LightGray; // 浅灰色
            var color2 = Colors.DarkGray;  // 深灰色
            var lightGeometryGroup = new GeometryGroup();
            var darkGeometryGroup = new GeometryGroup();

            for (int x = 0; x < container.Width; x += gridSize) {
                for (int y = 0; y < container.Height; y += gridSize) {
                    bool isLight = ((x / gridSize) + (y / gridSize)) % 2 == 0;
                    // 创建矩形几何图形
                    var rectangleGeometry = new RectangleGeometry() { Rect = new Rect(x, y, gridSize, gridSize) };
                    if (isLight) {
                        lightGeometryGroup.Children.Add(rectangleGeometry);
                    }
                    else {
                        darkGeometryGroup.Children.Add(rectangleGeometry);
                    }
                }
            }

            var lightPath = new Microsoft.UI.Xaml.Shapes.Path {
                Fill = new SolidColorBrush(color1),
                Data = lightGeometryGroup,
            };
            var darkPath = new Microsoft.UI.Xaml.Shapes.Path {
                Fill = new SolidColorBrush(color2),
                Data = darkGeometryGroup,
            };

            workground.Children.Add(lightPath);
            workground.Children.Add(darkPath);
        }
        #endregion

        private async void InkingCanvas_Loaded(object sender, RoutedEventArgs e) {
            await _viewModel.LoadBasicOrInit();
            await _viewModel.LoadRenderDataAsync();
        }

        private void InkCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args) {
            if (_compositeTarget != null) {
                RebuildCompositeTarget();
                args.DrawingSession.DrawImage(_compositeTarget);
            }
        }

        internal new void OnPointerEntered(PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            _originalInputCursor = this.ProtectedCursor ?? InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            this.ProtectedCursor = _viewModel.BasicData.SelectedToolItem?.Cursor;

            HandleToolEvent(tool => tool.OnPointerEntered(new(pointerPoint, _viewModel.BasicData.SelectedInkCanvas.Render)));
        }

        internal new void OnPointerMoved(PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            _viewModel.BasicData.UpdatePointerPos(pointerPoint.Position);
            HandleToolEvent(tool => tool.OnPointerMoved(new(pointerPoint, _viewModel.BasicData.SelectedInkCanvas.Render)));
        }

        internal new void OnPointerPressed(PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            HandleToolEvent(tool => tool.OnPointerPressed(new(pointerPoint, _viewModel.BasicData.SelectedInkCanvas.Render)));
        }

        internal new void OnPointerReleased(PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            HandleToolEvent(tool => tool.OnPointerReleased(new(pointerPoint, _viewModel.BasicData.SelectedInkCanvas.Render)));
        }

        internal new void OnPointerExited(PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(inkCanvas);
            HandleToolEvent(tool => tool.OnPointerExited(new(pointerPoint, _viewModel.BasicData.SelectedInkCanvas.Render)));
            if (_originalInputCursor != null) {
                this.ProtectedCursor = _originalInputCursor;
            }
        }

        private void HandleToolEvent(Action<Tool> action) {
            if (_viewModel.BasicData.SelectedInkCanvas == null) {
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Draft_SI_LayerNotAvailable), InfoBarType.Error, key: nameof(Constants.I18n.Draft_SI_LayerNotAvailable), isAllowDuplication: false);
                return;
            }

            if (!_viewModel.BasicData.SelectedInkCanvas.IsEnable) {
                MainPage.Instance.Bridge.GetNotify().ShowMsg(true, nameof(Constants.I18n.Draft_SI_LayerLocked), InfoBarType.Warning, key: nameof(Constants.I18n.Draft_SI_LayerLocked), isAllowDuplication: false);
                return;
            }

            _selectedTool = _viewModel.GetTool(_viewModel.BasicData.SelectedToolItem.Type);
            if (_selectedTool == null) {
                return;
            }

            action(_selectedTool);
            inkCanvas.Invalidate();
        }

        internal async Task AddLayerAsync() {
            var layer = await _viewModel.BasicData.AddLayerAsync();
            await layer.Render.IsCompleted.Task;
            RebuildCompositeTarget();
        }

        internal async Task CopyLayerAsync(long itemTag) {
            var layer = await _viewModel.BasicData.CopyLayerAsync(itemTag);
            await layer.Render.IsCompleted.Task;
            RebuildCompositeTarget();
        }

        internal async Task RenameAsync(long itemTag) {
            await _viewModel.BasicData.RenameAsync(itemTag);
        }

        internal async Task DeleteAsync(long itemTag) {
            await _viewModel.BasicData.DeleteAsync(itemTag);
        }

        internal async Task UpdateCustomColorsAsync(ColorChnageEventArgs e) {
            await _viewModel.BasicData.UpdateCustomColorsAsync(e);
        }

        internal InkCanvasViewModel _viewModel;
        private InputCursor _originalInputCursor;
        private Tool _selectedTool;
        private readonly CanvasRenderTarget _compositeTarget;
        private readonly TaskCompletionSource<bool> _isReady = new();
    }
}
