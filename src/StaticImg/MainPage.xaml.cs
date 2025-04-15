using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Common.Utils.Bridge;
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
        internal string EntryFilePath { get; }
        internal FileType RtFileType { get; }
        internal CanvasDevice SharedDevice { get; }

        /// <summary>
        /// 静态图像编辑页面
        /// </summary>
        /// <param name="entryFilePath">接收后缀为 FImage or FE_STATIC_IMG_PROJ 的文件路径</param>
        public MainPage(IDraftPanelBridge bridge, string entryFilePath, FileType rtFileType) {
            Instance = this;
            Bridge = bridge;
            EntryFilePath = entryFilePath;
            RtFileType = rtFileType;
            SharedDevice = CanvasDevice.GetSharedDevice();

            _viewModel = new MainPageViewModel();
            this.DataContext = _viewModel;

            this.InitializeComponent();
        }

        public async Task SaveAsync() {
            try {
                await inkCanvas.SaveAsync();
            }
            catch (Exception ex) {
                Bridge.Log(LogType.Error, ex);
                Bridge.GetNotify().ShowExp(ex);
            }
        }

        #region ui events
        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            _viewModel.IsEanble = false;
            Bridge.GetNotify().Loading(false, false);

            await inkCanvas.IsReady.Task;

            Bridge.GetNotify().Loaded();
            _viewModel.IsEanble = true;
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

        private void InkCanvas_Loaded(object sender, RoutedEventArgs e) {
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

            double contentWidth = inkCanvas._viewModel.BasicData.Size.Width;
            double contentHeight = inkCanvas._viewModel.BasicData.Size.Height;

            // 计算缩放因子（取宽度和高度两者较小的比例）
            double zoomFactor = Math.Min(
                (viewportWidth - (inkCanvas.Margin.Left + inkCanvas.Margin.Right)) / contentWidth,
                (viewportHeight - (inkCanvas.Margin.Top + inkCanvas.Margin.Bottom)) / contentHeight);
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
            await inkCanvas.AddLayerAsync();
        }

        private async void CopyLayer_Click(object sender, RoutedEventArgs e) {
            await inkCanvas.CopyLayerAsync(_rightTappedItem.ItemTag);
        }

        private async void RenameLayer_Click(object sender, RoutedEventArgs e) {
            await inkCanvas.RenameAsync(_rightTappedItem.ItemTag);
        }

        private async void DeleteLayer_Click(object sender, RoutedEventArgs e) {
            await inkCanvas.DeleteAsync(_rightTappedItem.ItemTag);
        }
        #endregion

        private void Listview_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e) {
            var container = layersListView.ContainerFromItem((e.OriginalSource as FrameworkElement).DataContext) as ListViewItem;
            _rightTappedItem = container.Content as LayerItem;
        }

        private async void ArcPalette_OnCustomeColorChangedEvent(object sender, ColorChnageEventArgs e) {
            await inkCanvas.UpdateCustomColorsAsync(e);
        }

        private void PaintBrushListView_ItemClick(object sender, ItemClickEventArgs e) {
            inkCanvas._viewModel.BasicData.SelectedBrush = e.ClickedItem as PaintBrushItem;
            paintBrushFlyout?.Hide();
        }

        private void PaintBrushExpander_Loaded(object sender, RoutedEventArgs e) {
            if (paintBrushListView.ItemsSource is IList<object> items && items.Count > 0) {
                paintBrushListView.SelectedItem = items[0];
                inkCanvas._viewModel.BasicData.SelectedBrush = items[0] as PaintBrushItem;
            }
        }

        private void PaintBrushThicknessTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
            string input = sender.Text;

            if (string.IsNullOrWhiteSpace(input)) {
                return;
            }

            if (!int.TryParse(input, out int parsedValue) ||
                parsedValue < 1 || parsedValue > 100) {
                sender.Text = inkCanvas._viewModel.BasicData.BrushThickness.ToString();
                sender.SelectionStart = sender.Text.Length;
                return;
            }

            inkCanvas._viewModel.BasicData.BrushThickness = parsedValue;
        }

        private void PaintBrushThicknessTextBox_LostFocus(object sender, RoutedEventArgs e) {
            if (paintBrushThicknessTextBox.Text.Trim().Length == 0) {
                paintBrushThicknessTextBox.Text = inkCanvas._viewModel.BasicData.BrushThickness.ToString();
            }
        }

        private void PaintBrushOpacityTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
            string input = sender.Text;

            if (string.IsNullOrWhiteSpace(input)) {
                return;
            }

            if (!int.TryParse(input, out int parsedValue) ||
                parsedValue < 1 || parsedValue > 100) {
                sender.Text = inkCanvas._viewModel.BasicData.BrushThickness.ToString();
                sender.SelectionStart = sender.Text.Length;
                return;
            }

            inkCanvas._viewModel.BasicData.BrushOpacity = parsedValue;
        }

        private void PaintBrushOpacityTextBox_LostFocus(object sender, RoutedEventArgs e) {
            if (paintBrushOpacityTextBox.Text.Trim().Length == 0) {
                paintBrushOpacityTextBox.Text = inkCanvas._viewModel.BasicData.BrushOpacity.ToString();
            }
        }

        private void ArcListViewToolItem_Loaded(object sender, RoutedEventArgs e) {
            inkCanvas._viewModel.BasicData.SelectedToolItem = toolItemListView.Items[0] as ToolItem;
        }

        private void EraserSizeTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
            string input = sender.Text;

            if (string.IsNullOrWhiteSpace(input)) {
                return;
            }

            if (!int.TryParse(input, out int parsedValue) ||
                parsedValue < 1 || parsedValue > 100) {
                sender.Text = inkCanvas._viewModel.BasicData.EraserSize.ToString();
                sender.SelectionStart = sender.Text.Length;
                return;
            }

            inkCanvas._viewModel.BasicData.EraserSize = parsedValue;
        }

        private void EraserSizeTextBox_LostFocus(object sender, RoutedEventArgs e) {
            if (eraserSizeTextBox.Text.Trim().Length == 0) {
                eraserSizeTextBox.Text = inkCanvas._viewModel.BasicData.EraserSize.ToString();
            }
        }

        private void EraserOpacityTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
            string input = sender.Text;

            if (string.IsNullOrWhiteSpace(input)) {
                return;
            }

            if (!int.TryParse(input, out int parsedValue) ||
                parsedValue < 1 || parsedValue > 100) {
                sender.Text = inkCanvas._viewModel.BasicData.EraserOpacity.ToString();
                sender.SelectionStart = sender.Text.Length;
                return;
            }

            inkCanvas._viewModel.BasicData.EraserOpacity = parsedValue;
        }

        private void EraserOpacityTextBox_LostFocus(object sender, RoutedEventArgs e) {
            if (eraserOpacityTextBox.Text.Trim().Length == 0) {
                eraserOpacityTextBox.Text = inkCanvas._viewModel.BasicData.EraserOpacity.ToString();
            }
        }

        private LayerItem _rightTappedItem;
        internal readonly MainPageViewModel _viewModel;
    }
}
