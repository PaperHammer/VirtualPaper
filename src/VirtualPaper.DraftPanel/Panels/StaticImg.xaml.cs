using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using VirtualPaper.Common;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.Model.Runtime;
using VirtualPaper.DraftPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Panels {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    internal sealed partial class StaticImg : Page, IRuntime {
        /// <summary>
        /// ��̬ͼ��༭ҳ��
        /// </summary>
        /// <param name="entryFilePath">���պ�׺Ϊ FImage or FE_STATIC_IMG_PROJ ���ļ�·��</param>
        public StaticImg(string entryFilePath, FileType rtFileType) {
            this.InitializeComponent();

            _viewModel = new StaticImgViewModel(entryFilePath, rtFileType);
            this.DataContext = _viewModel;
        }

        public async Task SaveAsync() {
            await _viewModel.SaveAsync();
        }

        #region ui event
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
                // ��ԭ
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
                // ��ԭ
                zoomComboBox.Text = $"{StaticImgConstants.DecimalToPercent((float)_viewModel.CanvasZoom)}%";
            }
        }

        private void LayerManager_Loaded(object sender, RoutedEventArgs e) {
            //await Task.Delay(300);  DataLoaded="LayerManager_DataLoaded"
            FitView();
        }

        private void FitView_ButtonClick(object sender, RoutedEventArgs e) {
            FitView();
        }

        private void CanvasSVer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e) {
            // ����Ƿ�Ϊ�û������Ĺ���/����
            //if (e.IsInertial) {
            //    // ʹ��������
            //    // �� ScrollViewer ������֧��ֱ�Ӳ����Ŀؼ���ʹ�ü��ʻ�
            //    // ���������˶����� ChangeView 

            _viewModel.CanvasZoom = e.FinalView.ZoomFactor;
            //}

            UpdateComboBoxText(e.FinalView.ZoomFactor);
            UpdateSliderValue(e.FinalView.ZoomFactor);
        }

        private void FitView() {
            // ��ȡ��ǰ���ӿڳߴ��LayerCanvas��ʵ�ʳߴ�
            double viewportWidth = canvasSVer.ViewportWidth;
            double viewportHeight = canvasSVer.ViewportHeight;

            double contentWidth = _viewModel.ManagerData.Size.Width;
            double contentHeight = _viewModel.ManagerData.Size.Height;

            // �����������ӣ�ȡ��Ⱥ͸߶����߽�С�ı�����
            double zoomFactor = Math.Min(
                (viewportWidth - (layerManager.Margin.Left + layerManager.Margin.Right)) / contentWidth,
                (viewportHeight - (layerManager.Margin.Top + layerManager.Margin.Bottom)) / contentHeight);
            // ȷ����������������Χ��
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
        }
        #endregion

        internal readonly StaticImgViewModel _viewModel;

        private void AddLayer_Click(object sender, RoutedEventArgs e) {

        }

        private void CopyLayer_Click(object sender, RoutedEventArgs e) {

        }

        private void RenameLayer_Click(object sender, RoutedEventArgs e) {

        }

        private void DeleteLayer_Click(object sender, RoutedEventArgs e) {

        }
    }
}
