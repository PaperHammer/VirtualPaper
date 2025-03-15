using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.Model.Runtime;
using VirtualPaper.DraftPanel.Utils;
using VirtualPaper.DraftPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Panels {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StaticImg : Page, IRuntime {
        public StaticImg(string folderPath) {
            this.InitializeComponent();

            _viewModel = new(folderPath);
            //_viewModel.OnCanvasZoomChanged += ViewModel_OnCanvasZoomChanged;
        }

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e) {
            isDrawable = _viewModel._vpCanvas.SelectedLayer.IsEnable &&
                _viewModel._vpCanvas.SelectedLayer.IsVisible;
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e) {
            if (!isDrawable) return;

            // 开始新的线条
            var pointerPoint = e.GetCurrentPoint(drawingCanvas);
            _currentLine = new Polyline() {
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _currentLine.Points.Add(pointerPoint.Position);
            drawingCanvas.Children.Add(_currentLine);

            _viewModel._vpCanvas.SelectedLayer.Draws.Add(new Draw() { Path = [new(pointerPoint.Position)], StrokeColor = TypeConvertUtil.Color2Hex(Colors.Black), StrokeThickness = 2 });
            _currentLineIndex = _viewModel._vpCanvas.SelectedLayer.Draws.Count - 1; // 记录当前线条在Draws列表中的索引

            isDrawing = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e) {
            var pointerPoint = e.GetCurrentPoint(drawingCanvas);
            _viewModel.PointerPos = pointerPoint.Position;

            if (!isDrawing || !isDrawable) return;

            // 继续当前线条
            _currentLine.Points.Add(pointerPoint.Position);
            _viewModel._vpCanvas.SelectedLayer.Draws[_currentLineIndex].Path.Add(new(pointerPoint.Position));
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e) {
            EndDrawing();
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e) {
            isDrawable = false;
            _viewModel.PointerPos = null;
            EndDrawing();
        }

        private void EndDrawing() {
            if (isDrawing) {
                // 结束当前线条
                isDrawing = false;
                _currentLine = null; // 清除当前线条引用
            }
        }

        public async Task LoadAsync() {
            await _viewModel.LoadAsync();
            RenderCanvasFromData();
        }

        public async Task SaveAsync() {
            await _viewModel.SaveAsync();
        }

        private void RenderCanvasFromData() {
            for (int layerIndex = 0; layerIndex < _viewModel._vpCanvas.LayerList.Count; layerIndex++) {
                var layer = _viewModel._vpCanvas.LayerList[layerIndex];
                // 确保该图层存在
                drawingCanvas.AddLayer(layerIndex);

                // 获取对应的Grid
                var layerGrid = drawingCanvas._layers[layerIndex];
                layerGrid.Background = TypeConvertUtil.Hex2Brush(layer.Background);
                layerGrid.Width = _viewModel.CanvasSize.Width;
                layerGrid.Height = _viewModel.CanvasSize.Height;

                // 根据当前图层index添加图像和绘制线条
                // 处理图像元素, 并按照ZIndex排序
                var imgs = layer.Images
                    .Where(img => img.LayerIndex == layerIndex)
                    .OrderBy(img => img.ZIndex)
                    .ToList();
                foreach (var img in imgs) {
                    // 添加图像到当前图层Grid中
                    var imageControl = new Image {
                        Source = CanvasUtil.ByteArrayToImageSource(img.ImageData),
                        Width = img.Width,
                        Height = img.Height,
                    };
                    Canvas.SetLeft(imageControl, img.Position.X);
                    Canvas.SetTop(imageControl, img.Position.Y);
                    Canvas.SetZIndex(imageControl, img.ZIndex);
                    layerGrid.Children.Add(imageControl);
                }

                // 处理绘图元素, 并按照ZIndex排序
                var drawings = layer.Draws
                    .Where(draw => draw.LayerIndex == layerIndex)
                    .OrderBy(draw => draw.ZIndex)
                    .ToList();
                foreach (var draw in drawings) {
                    // 添加绘制线条到当前图层Grid中
                    Path path = new() {
                        Stroke = TypeConvertUtil.Hex2Brush(draw.StrokeColor),
                        StrokeThickness = draw.StrokeThickness,
                        Data = CanvasUtil.CreatePathGeometry(draw.Path) // 创建路径几何图形
                    };
                    Canvas.SetLeft(path, 0); // 设置相对于Grid的位置
                    Canvas.SetTop(path, 0);
                    Canvas.SetZIndex(path, draw.ZIndex); // 设置ZIndex
                    layerGrid.Children.Add(path);
                }
            }
        }

        private void ZoomOut_ButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            var a = StaticImgMetadata.RoundToNearestFive(_viewModel.CanvasZoom);
            var b = StaticImgMetadata.GetSubStepSize(_viewModel.CanvasZoom);
            _viewModel.CanvasZoom = Math.Max(StaticImgMetadata.MinZoomFactor,
                StaticImgMetadata.RoundToNearestFive(_viewModel.CanvasZoom) - StaticImgMetadata.GetSubStepSize(_viewModel.CanvasZoom));

            UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
            UpdateComboBoxText((float)_viewModel.CanvasZoom);
            UpdateSliderValue((float)_viewModel.CanvasZoom);
        }

        private void ZoomIn_ButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            var a = StaticImgMetadata.RoundToNearestFive(_viewModel.CanvasZoom);
            var b = StaticImgMetadata.GetSubStepSize(_viewModel.CanvasZoom);
            _viewModel.CanvasZoom = Math.Min(StaticImgMetadata.MaxZoomFactor, 
                StaticImgMetadata.RoundToNearestFive(_viewModel.CanvasZoom) + StaticImgMetadata.GetAddStepSize(_viewModel.CanvasZoom));

            UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
            UpdateComboBoxText((float)_viewModel.CanvasZoom);
            UpdateSliderValue((float)_viewModel.CanvasZoom);
        }

        private void ZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) {
            if (zoomSlider.FocusState == Microsoft.UI.Xaml.FocusState.Unfocused) return;

            _viewModel.CanvasZoom = StaticImgMetadata.PercentToDeciaml((float)e.NewValue);

            UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
            UpdateComboBoxText((float)_viewModel.CanvasZoom);
            UpdateSliderValue((float)_viewModel.CanvasZoom);
        }

        private void ZoomComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args) {
            if (args.Text is string s && double.TryParse(s.TrimEnd('%'), out var res) && StaticImgMetadata.IsZoomValid(res / 100)) {
                _viewModel.CanvasZoom = res / 100;

                UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
                UpdateComboBoxText((float)_viewModel.CanvasZoom);
                UpdateSliderValue((float)_viewModel.CanvasZoom);
            }
            else {
                // 还原
                zoomComboBox.Text = $"{StaticImgMetadata.DecimalToPercent((float)_viewModel.CanvasZoom)}%";
            }
        }

        private void ZoomComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (double.TryParse((e.AddedItems[0] as string).TrimEnd('%'), out double val)) {
                _viewModel.CanvasZoom = val / 100;

                UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
                UpdateComboBoxText((float)_viewModel.CanvasZoom);
                UpdateSliderValue((float)_viewModel.CanvasZoom);
            }
            else {
                // 还原
                zoomComboBox.Text = $"{StaticImgMetadata.DecimalToPercent((float)_viewModel.CanvasZoom)}%";
            }
        }

        private void DrawingCanvas_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            FitView();
        }

        private void FitView_ButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            FitView();
        }

        private void CanvasSVer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e) {
            // 检查是否为用户触发的滚动/缩放
            //if (e.IsInertial) {
            //    // 使用鼠标滚轮
            //    // 在 ScrollViewer 和其他支持直接操作的控件上使用键笔划
            //    // 调用启用了动画的 ChangeView 

            _viewModel.CanvasZoom = e.FinalView.ZoomFactor;
            //}

            UpdateComboBoxText(e.FinalView.ZoomFactor);
            UpdateSliderValue(e.FinalView.ZoomFactor);
        }

        //private void ViewModel_OnCanvasZoomChanged(object sender, double newZoomFactor) {
        //    // 应用新的缩放因子
        //    double percent = StaticImgMetadata.DecimalToPercent((float)newZoomFactor);
        //    zoomSlider.Value = percent;
        //    zoomComboBox.Text = $"{percent}%";
        //}

        private void FitView() {
            // 获取当前的视口尺寸和LayerCanvas的实际尺寸
            double viewportWidth = canvasSVer.ViewportWidth;
            double viewportHeight = canvasSVer.ViewportHeight;

            double contentWidth = _viewModel.CanvasSize.Width;
            double contentHeight = _viewModel.CanvasSize.Height;

            // 计算缩放因子（取宽度和高度两者较小的比例）
            double zoomFactor = Math.Round(Math.Min(
                (viewportWidth - (drawingCanvas.Margin.Left + drawingCanvas.Margin.Right)) / contentWidth,
                (viewportHeight - (drawingCanvas.Margin.Top + drawingCanvas.Margin.Bottom)) / contentHeight), 1);
            // 确保缩放因子在允许范围内
            zoomFactor = Math.Max(StaticImgMetadata.MinZoomFactor, Math.Min(zoomFactor, StaticImgMetadata.MaxZoomFactor));
            _viewModel.CanvasZoom = zoomFactor;

            UpdateScrollViewerZoom((float)zoomFactor);
            UpdateComboBoxText((float)zoomFactor);
            UpdateSliderValue((float)zoomFactor);
        }

        private void UpdateScrollViewerZoom(float value) {
            canvasSVer.ChangeView(null, null, value);
        }

        private void UpdateComboBoxText(float value) {
            double percent = StaticImgMetadata.DecimalToPercent(value);
            zoomComboBox.Text = $"{percent}%";
        }

        private void UpdateSliderValue(float value) {
            double percent = StaticImgMetadata.DecimalToPercent(value);
            zoomSlider.Value = percent;
        }

        internal readonly StaticImgViewModel _viewModel;
        private bool isDrawing = false, isDrawable = false;
        private Polyline _currentLine;
        private int _currentLineIndex = 0;
    }
}
