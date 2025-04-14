using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.UIComponent.Input;
using VirtualPaper.UIComponent.Utils.ArcEventArgs;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.ViewModels;
using Workloads.Creation.StaticImg.Views.Components;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, IRuntime {
        internal static MainPage Instance { get; private set; }
        internal IDraftPanelBridge Bridge { get; }

        /// <summary>
        /// 静态图像编辑页面
        /// </summary>
        /// <param name="entryFilePath">接收后缀为 FImage or FE_STATIC_IMG_PROJ 的文件路径</param>
        public MainPage(IDraftPanelBridge bridge, string entryFilePath, FileType rtFileType) {
            Instance = this;

            this.InitializeComponent();

            Bridge = bridge;
            _viewModel = new MainPageViewModel(entryFilePath, rtFileType);
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
            _viewModel.CanvasZoom = Math.Max(Consts.MinZoomFactor,
                Consts.RoundToNearestFive(_viewModel.CanvasZoom) - Consts.GetSubStepSize(_viewModel.CanvasZoom));

            UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
            UpdateComboBoxText((float)_viewModel.CanvasZoom);
            UpdateSliderValue((float)_viewModel.CanvasZoom);
        }

        private void ZoomIn_ButtonClick(object sender, RoutedEventArgs e) {
            _viewModel.CanvasZoom = Math.Min(Consts.MaxZoomFactor,
                Consts.RoundToNearestFive(_viewModel.CanvasZoom) + Consts.GetAddStepSize(_viewModel.CanvasZoom));

            UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
            UpdateComboBoxText((float)_viewModel.CanvasZoom);
            UpdateSliderValue((float)_viewModel.CanvasZoom);
        }

        private void ZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) {
            if (zoomSlider.FocusState == FocusState.Unfocused) return;

            _viewModel.CanvasZoom = Consts.PercentToDeciaml((float)e.NewValue);

            UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
            UpdateComboBoxText((float)_viewModel.CanvasZoom);
            UpdateSliderValue((float)_viewModel.CanvasZoom);
        }

        private void ZoomComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args) {
            if (args.Text is string s && double.TryParse(s.TrimEnd('%'), out var res) && Consts.IsZoomValid(res / 100)) {
                _viewModel.CanvasZoom = res / 100;

                UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
                UpdateComboBoxText((float)_viewModel.CanvasZoom);
                UpdateSliderValue((float)_viewModel.CanvasZoom);
            }
            else {
                // 还原
                zoomComboBox.Text = $"{Consts.DecimalToPercent((float)_viewModel.CanvasZoom)}%";
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
                zoomComboBox.Text = $"{Consts.DecimalToPercent((float)_viewModel.CanvasZoom)}%";
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
            zoomFactor = Math.Max(Consts.MinZoomFactor, Math.Min(zoomFactor, Consts.MaxZoomFactor));
            _viewModel.CanvasZoom = zoomFactor;

            UpdateScrollViewerZoom((float)zoomFactor);
            UpdateComboBoxText((float)zoomFactor);
            UpdateSliderValue((float)zoomFactor);
        }

        private void UpdateScrollViewerZoom(float value) {
            canvasSVer.ChangeView(null, null, value);
        }

        private void UpdateComboBoxText(float value) {
            double percent = Consts.DecimalToPercent(value);
            zoomComboBox.Text = $"{percent}%";
        }

        private void UpdateSliderValue(float value) {
            double percent = Consts.DecimalToPercent(value);
            zoomSlider.Value = percent;
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

        private async void ArcPalette_OnCustomeColorChangedEvent(object sender, ColorChnageEventArgs e) {
            await _viewModel.UpdateCustomColorsAsync(e);
        }

        private void PaintBrushListView_ItemClick(object sender, ItemClickEventArgs e) {
            _viewModel.SelectedBrush = e.ClickedItem as PaintBrushItem;
            PaintBrushFlyout?.Hide();
        }

        private void PaintBrushExpander_Loaded(object sender, RoutedEventArgs e) {
            if (PaintBrushListView.ItemsSource is IList<object> items && items.Count > 0) {
                PaintBrushListView.SelectedItem = items[0];
                _viewModel.SelectedBrush = items[0] as PaintBrushItem;
            }
        }

        private void ThicknessTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
            string input = sender.Text;

            if (string.IsNullOrWhiteSpace(input)) {
                return;
            }

            if (!int.TryParse(input, out int parsedValue) ||
                parsedValue < 1 || parsedValue > 100) {
                sender.Text = _viewModel.BrushThickness.ToString();
                sender.SelectionStart = sender.Text.Length;
                return;
            }

            _viewModel.BrushThickness = parsedValue;
        }

        private void ThicknessTextBox_LostFocus(object sender, RoutedEventArgs e) {
            if (thicknessTextBox.Text.Trim().Length == 0) {
                thicknessTextBox.Text = _viewModel.BrushThickness.ToString();
            }
        }

        private void OpacityTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
            string input = sender.Text;

            if (string.IsNullOrWhiteSpace(input)) {
                return;
            }

            if (!int.TryParse(input, out int parsedValue) ||
                parsedValue < 1 || parsedValue > 100) {
                sender.Text = _viewModel.BrushThickness.ToString();
                sender.SelectionStart = sender.Text.Length;
                return;
            }

            _viewModel.BrushOpacity = parsedValue;
        }

        private void OpacityTextBox_LostFocus(object sender, RoutedEventArgs e) {
            if (opacityTextBox.Text.Trim().Length == 0) {
                opacityTextBox.Text = _viewModel.BrushOpacity.ToString();
            }
        }

        private void ArcListViewToolItem_Loaded(object sender, RoutedEventArgs e) {
            _viewModel.SelectedToolItem = toolItemListView.Items[0] as ToolItem;
        }

        internal readonly MainPageViewModel _viewModel;
        private LayerItem _rightTappedItem;
    }
}
