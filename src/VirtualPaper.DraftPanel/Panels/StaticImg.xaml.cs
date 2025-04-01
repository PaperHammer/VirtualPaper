using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using VirtualPaper.Common;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.Model.Runtime;
using VirtualPaper.DraftPanel.UIComponents;
using VirtualPaper.DraftPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Panels {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    internal sealed partial class StaticImg : Page, IRuntime {
        /// <summary>
        /// 静态图像编辑页面
        /// </summary>
        /// <param name="entryFilePath">接收后缀为 FImage or FE_STATIC_IMG_PROJ 的文件路径</param>
        public StaticImg(string entryFilePath, FileType rtFileType) {
            this.InitializeComponent();

            _viewModel = new StaticImgViewModel(entryFilePath, rtFileType);
            this.DataContext = _viewModel;
        }

        public async Task SaveAsync() {
            await _viewModel.SaveAsync();
        }

        #region ui events
        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            await _viewModel.LoadAsync();
        }

        private void ZoomOut_ButtonClick(object sender, RoutedEventArgs e) {
            _viewModel.CanvasZoom = Math.Max(StaticImgConstants.MinZoomFactor,
                StaticImgConstants.RoundToNearestFive(_viewModel.CanvasZoom) - StaticImgConstants.GetSubStepSize(_viewModel.CanvasZoom));

            UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
            UpdateComboBoxText((float)_viewModel.CanvasZoom);
            UpdateSliderValue((float)_viewModel.CanvasZoom);
        }

        private void ZoomIn_ButtonClick(object sender, RoutedEventArgs e) {
            _viewModel.CanvasZoom = Math.Min(StaticImgConstants.MaxZoomFactor,
                StaticImgConstants.RoundToNearestFive(_viewModel.CanvasZoom) + StaticImgConstants.GetAddStepSize(_viewModel.CanvasZoom));

            UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
            UpdateComboBoxText((float)_viewModel.CanvasZoom);
            UpdateSliderValue((float)_viewModel.CanvasZoom);
        }

        private void ZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) {
            if (zoomSlider.FocusState == FocusState.Unfocused) return;

            _viewModel.CanvasZoom = StaticImgConstants.PercentToDeciaml((float)e.NewValue);

            UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
            UpdateComboBoxText((float)_viewModel.CanvasZoom);
            UpdateSliderValue((float)_viewModel.CanvasZoom);
        }

        private void ZoomComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args) {
            if (args.Text is string s && double.TryParse(s.TrimEnd('%'), out var res) && StaticImgConstants.IsZoomValid(res / 100)) {
                _viewModel.CanvasZoom = res / 100;

                UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
                UpdateComboBoxText((float)_viewModel.CanvasZoom);
                UpdateSliderValue((float)_viewModel.CanvasZoom);
            }
            else {
                // 还原
                zoomComboBox.Text = $"{StaticImgConstants.DecimalToPercent((float)_viewModel.CanvasZoom)}%";
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
                zoomComboBox.Text = $"{StaticImgConstants.DecimalToPercent((float)_viewModel.CanvasZoom)}%";
            }
        }

        private void LayerManager_Loaded(object sender, RoutedEventArgs e) {
            FitView();
        }

        private void FitView_ButtonClick(object sender, RoutedEventArgs e) {
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

        private void FitView() {
            // 获取当前的视口尺寸和LayerCanvas的实际尺寸
            double viewportWidth = canvasSVer.ViewportWidth;
            double viewportHeight = canvasSVer.ViewportHeight;

            double contentWidth = _viewModel.ManagerData.Size.Width;
            double contentHeight = _viewModel.ManagerData.Size.Height;

            // 计算缩放因子（取宽度和高度两者较小的比例）
            double zoomFactor = Math.Min(
                (viewportWidth - (layerManager.Margin.Left + layerManager.Margin.Right)) / contentWidth,
                (viewportHeight - (layerManager.Margin.Top + layerManager.Margin.Bottom)) / contentHeight);
            // 确保缩放因子在允许范围内
            zoomFactor = Math.Max(StaticImgConstants.MinZoomFactor, Math.Min(zoomFactor, StaticImgConstants.MaxZoomFactor));
            _viewModel.CanvasZoom = zoomFactor;

            UpdateScrollViewerZoom((float)zoomFactor);
            UpdateComboBoxText((float)zoomFactor);
            UpdateSliderValue((float)zoomFactor);
        }

        private void UpdateScrollViewerZoom(float value) {
            canvasSVer.ChangeView(null, null, value);
        }

        private void UpdateComboBoxText(float value) {
            double percent = StaticImgConstants.DecimalToPercent(value);
            zoomComboBox.Text = $"{percent}%";
        }

        private void UpdateSliderValue(float value) {
            double percent = StaticImgConstants.DecimalToPercent(value);
            zoomSlider.Value = percent;
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count == 0) {
                (sender as ListView).SelectedItem = _viewModel.ManagerData.SelectedLayerData;
            }
            else {
                _viewModel.ManagerData.SelectedLayerData = (sender as ListView).SelectedItem as CanvasLayerData;
            }
        }
        #endregion

        #region menu items
        private async void AddLayer_Click(object sender, RoutedEventArgs e) {
            await _viewModel.AddLayerAsync();
        }

        private async void CopyLayer_Click(object sender, RoutedEventArgs e) {
            await _viewModel.CopyLayerAsync(_rightTappedItem.ItemTag);
        }

        private async void RenameLayer_Click(object sender, RoutedEventArgs e) {
            await _viewModel.RenameAsync(_rightTappedItem.ItemTag);
        }

        private async void DeleteLayer_Click(object sender, RoutedEventArgs e) {
            await _viewModel.DeleteAsync(_rightTappedItem.ItemTag);
        }
        #endregion

        private void Listview_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e) {
            var container = layersListView.ContainerFromItem((e.OriginalSource as FrameworkElement).DataContext) as ListViewItem;
            _rightTappedItem = container.Content as LayerItem;
        }

        private void ArcColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args) {
            _viewModel.ManagerData.SelectedColor = args.NewColor;
        }

        internal readonly StaticImgViewModel _viewModel;
        private LayerItem _rightTappedItem;
    }
}
