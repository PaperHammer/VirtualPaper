using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using NLog.Config;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.TaskManager;
using VirtualPaper.Common.Utils.UnReUtil;
using VirtualPaper.UIComponent.Input;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.ToolItems.Utils;
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
        internal UndoRedo<InkRenderData> UnRe { get; }

        /// <summary>
        /// ��̬ͼ��༭ҳ��
        /// </summary>
        /// <param name="entryFilePath">���պ�׺Ϊ FImage or FE_STATIC_IMG_PROJ ���ļ�·��</param>
        public MainPage(IDraftPanelBridge bridge, string entryFilePath, FileType rtFileType) {
            Instance = this;
            Bridge = bridge;
            EntryFilePath = entryFilePath;
            RtFileType = rtFileType;
            SharedDevice = CanvasDevice.GetSharedDevice();
            UnRe = new UndoRedo<InkRenderData>();
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

            await inkCanvas.IsInited.Task;

            Bridge.GetNotify().Loaded();
            _viewModel.IsEanble = true;
        }

        //private void ZoomOut_ButtonClick(object sender, RoutedEventArgs e) {
        //    _viewModel.CanvasZoom = Math.Max(Consts.MinZoomFactor,
        //        Consts.RoundToNearestFive(_viewModel.CanvasZoom) - Consts.GetSubStepSize(_viewModel.CanvasZoom));

        //    UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
        //    UpdateComboBoxText((float)_viewModel.CanvasZoom);
        //    UpdateSliderValue((float)_viewModel.CanvasZoom);
        //}

        //private void ZoomIn_ButtonClick(object sender, RoutedEventArgs e) {
        //    _viewModel.CanvasZoom = Math.Min(Consts.MaxZoomFactor,
        //        Consts.RoundToNearestFive(_viewModel.CanvasZoom) + Consts.GetAddStepSize(_viewModel.CanvasZoom));

        //    UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
        //    UpdateComboBoxText((float)_viewModel.CanvasZoom);
        //    UpdateSliderValue((float)_viewModel.CanvasZoom);
        //}

        //private void ZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) {
        //    if (zoomSlider.FocusState == FocusState.Unfocused) return;

        //    _viewModel.CanvasZoom = Consts.FPercentToDeciaml((float)e.NewValue);

        //    UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
        //    UpdateComboBoxText((float)_viewModel.CanvasZoom);
        //    UpdateSliderValue((float)_viewModel.CanvasZoom);
        //}

        //private void ZoomComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args) {
        //    if (args.Text is string s && double.TryParse(s.TrimEnd('%'), out var res) && Consts.IsZoomValid(res / 100)) {
        //        _viewModel.CanvasZoom = res / 100;

        //        UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
        //        UpdateComboBoxText((float)_viewModel.CanvasZoom);
        //        UpdateSliderValue((float)_viewModel.CanvasZoom);
        //    }
        //    else {
        //        // ��ԭ
        //        zoomComboBox.Text = $"{Consts.DecimalToFPercent((float)_viewModel.CanvasZoom)}%";
        //    }
        //}

        //private void ZoomComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        //    if (double.TryParse((e.AddedItems[0] as string).TrimEnd('%'), out double val)) {
        //        _viewModel.CanvasZoom = val / 100;

        //        UpdateScrollViewerZoom((float)_viewModel.CanvasZoom);
        //        UpdateComboBoxText((float)_viewModel.CanvasZoom);
        //        UpdateSliderValue((float)_viewModel.CanvasZoom);
        //    }
        //    else {
        //        // ��ԭ
        //        zoomComboBox.Text = $"{Consts.DecimalToFPercent((float)_viewModel.CanvasZoom)}%";
        //    }
        //}

        //private async void InkCanvas_Loaded(object sender, RoutedEventArgs e) {
        //    await inkCanvas._viewModel.BasicDataLoaded.Task; // ȷ�����������ڻ������ݼ�����ɺ�����
        //    //FitView();
        //}

        //private void FitView_ButtonClick(object sender, RoutedEventArgs e) {
        //    //FitView();
        //}

        //private void CanvasContainer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e) {
        //    // ����Ƿ�Ϊ�û������Ĺ���/����
        //    //if (e.IsInertial) {
        //    //    // ʹ��������
        //    //    // �� ScrollViewer ������֧��ֱ�Ӳ����Ŀؼ���ʹ�ü��ʻ�
        //    //    // ���������˶����� ChangeView 

        //    _viewModel.CanvasZoom = e.FinalView.ZoomFactor;
        //    //}

        //    UpdateComboBoxText(e.FinalView.ZoomFactor);
        //    UpdateSliderValue(e.FinalView.ZoomFactor);
        //}

        //private void FitView() {
        //    // ��ȡ��ǰ���ӿڳߴ��LayerCanvas��ʵ�ʳߴ�
        //    double viewportWidth = canvasContainer.ViewportWidth;
        //    double viewportHeight = canvasContainer.ViewportHeight;

        //    double contentWidth = inkCanvas._viewModel.ConfigData.Size.Width;
        //    double contentHeight = inkCanvas._viewModel.ConfigData.Size.Height;

        //    // �����������ӣ�ȡ��Ⱥ͸߶����߽�С�ı�����
        //    double zoomFactor = Math.Min(
        //        (viewportWidth - (inkCanvas.Margin.Left + inkCanvas.Margin.Right)) / contentWidth,
        //        (viewportHeight - (inkCanvas.Margin.Top + inkCanvas.Margin.Bottom)) / contentHeight);
        //    // ȷ����������������Χ��
        //    zoomFactor = Math.Max(Consts.MinZoomFactor, Math.Min(zoomFactor, Consts.MaxZoomFactor));
        //    _viewModel.CanvasZoom = zoomFactor;

        //    UpdateScrollViewerZoom((float)zoomFactor);
        //    UpdateComboBoxText((float)zoomFactor);
        //    UpdateSliderValue((float)zoomFactor);
        //}

        //private void UpdateScrollViewerZoom(float value) {
        //    canvasContainer.ChangeView(null, null, value);
        //}

        //private void UpdateComboBoxText(float value) {
        //    double percent = Consts.DecimalToFPercent(value);
        //    zoomComboBox.Text = $"{percent}%";
        //}

        //private void UpdateSliderValue(float value) {
        //    double percent = Consts.DecimalToFPercent(value);
        //    zoomSlider.Value = percent;
        //}
        #endregion

        #region menu items
        //private async void AddLayer_Click(object sender, RoutedEventArgs e) {
        //    await inkCanvas.AddLayerAsync();
        //}

        //private async void CopyLayer_Click(object sender, RoutedEventArgs e) {
        //    await inkCanvas.CopyLayerAsync(_rightTappedItem.ItemTag);
        //}

        //private async void RenameLayer_Click(object sender, RoutedEventArgs e) {
        //    await inkCanvas.RenameAsync(_rightTappedItem.ItemTag);
        //}

        //private async void DeleteLayer_Click(object sender, RoutedEventArgs e) {
        //    await inkCanvas.DeleteAsync(_rightTappedItem.ItemTag);
        //}
        #endregion

        //private void Listview_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e) {
        //    var container = layersListView.ContainerFromItem((e.OriginalSource as FrameworkElement).DataContext) as ListViewItem;
        //    _rightTappedItem = container.Content as LayerItem;
        //}

        //private async void ArcPalette_OnCustomeColorChangedEvent(object sender, ColorChangeEventArgs e) {
        //    await inkCanvas.UpdateCustomColorsAsync(e);
        //}

        //private void PaintBrushListView_ItemClick(object sender, ItemClickEventArgs e) {
        //    inkCanvas._viewModel.ConfigData.SelectedBrush = e.ClickedItem as PaintBrushItem;
        //    paintBrushFlyout?.Hide();
        //}

        //private void PaintBrushExpander_Loaded(object sender, RoutedEventArgs e) {
        //    if (paintBrushListView.ToolItems is IList<object> items && items.Count > 0) {
        //        paintBrushListView.SelectedTool = items[0];
        //        inkCanvas._viewModel.ConfigData.SelectedBrush = items[0] as PaintBrushItem;
        //    }
        //}

        //private void PaintBrushThicknessTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
        //    string input = sender.Text;

        //    if (string.IsNullOrWhiteSpace(input)) {
        //        return;
        //    }

        //    if (!int.TryParse(input, out int parsedValue) ||
        //        parsedValue < 1 || parsedValue > 100) {
        //        sender.Text = inkCanvas._viewModel.ConfigData.EraserSize.ToString();
        //        sender.SelectionStart = sender.Text.Length;
        //        return;
        //    }

        //    inkCanvas._viewModel.ConfigData.EraserSize = parsedValue;
        //}

        //private void PaintBrushThicknessTextBox_LostFocus(object sender, RoutedEventArgs e) {
        //    if (paintBrushThicknessTextBox.Text.Trim().Length == 0) {
        //        paintBrushThicknessTextBox.Text = inkCanvas._viewModel.ConfigData.EraserSize.ToString();
        //    }
        //}

        //private void PaintBrushOpacityTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
        //    string input = sender.Text;

        //    if (string.IsNullOrWhiteSpace(input)) {
        //        return;
        //    }

        //    if (!int.TryParse(input, out int parsedValue) ||
        //        parsedValue < 1 || parsedValue > 100) {
        //        sender.Text = inkCanvas._viewModel.ConfigData.EraserSize.ToString();
        //        sender.SelectionStart = sender.Text.Length;
        //        return;
        //    }

        //    inkCanvas._viewModel.ConfigData.EraserOpacity = parsedValue;
        //}

        //private void PaintBrushOpacityTextBox_LostFocus(object sender, RoutedEventArgs e) {
        //    if (paintBrushOpacityTextBox.Text.Trim().Length == 0) {
        //        paintBrushOpacityTextBox.Text = inkCanvas._viewModel.ConfigData.EraserOpacity.ToString();
        //    }
        //}

        //private void ArcListViewToolItem_Loaded(object sender, RoutedEventArgs e) {
        //    inkCanvas._viewModel.ConfigData.SelectedTool = toolItemListView.Items[0] as ToolItem;
        //}

        //private void EraserSizeTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
        //    string input = sender.Text;

        //    if (string.IsNullOrWhiteSpace(input)) {
        //        return;
        //    }

        //    if (!int.TryParse(input, out int parsedValue) ||
        //        parsedValue < 1 || parsedValue > 100) {
        //        sender.Text = inkCanvas._viewModel.ConfigData.EraserSize.ToString();
        //        sender.SelectionStart = sender.Text.Length;
        //        return;
        //    }

        //    inkCanvas._viewModel.ConfigData.EraserSize = parsedValue;
        //}

        //private void EraserSizeTextBox_LostFocus(object sender, RoutedEventArgs e) {
        //    if (eraserSizeTextBox.Text.Trim().Length == 0) {
        //        eraserSizeTextBox.Text = inkCanvas._viewModel.ConfigData.EraserSize.ToString();
        //    }
        //}

        //private void EraserOpacityTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
        //    string input = sender.Text;

        //    if (string.IsNullOrWhiteSpace(input)) {
        //        return;
        //    }

        //    if (!int.TryParse(input, out int parsedValue) ||
        //        parsedValue < 1 || parsedValue > 100) {
        //        sender.Text = inkCanvas._viewModel.ConfigData.EraserOpacity.ToString();
        //        sender.SelectionStart = sender.Text.Length;
        //        return;
        //    }

        //    inkCanvas._viewModel.ConfigData.EraserOpacity = parsedValue;
        //}

        //private void EraserOpacityTextBox_LostFocus(object sender, RoutedEventArgs e) {
        //    if (eraserOpacityTextBox.Text.Trim().Length == 0) {
        //        eraserOpacityTextBox.Text = inkCanvas._viewModel.ConfigData.EraserOpacity.ToString();
        //    }
        //}

        //private void CanvasContainer_PointerEntered(object sender, PointerRoutedEventArgs e) {
        //    inkCanvas.OnPointerEntered(e);
        //}

        //private void CanvasContainer_PointerMoved(object sender, PointerRoutedEventArgs e) {
        //    inkCanvas.OnPointerMoved(e);
        //}

        //private void CanvasContainer_PointerPressed(object sender, PointerRoutedEventArgs e) {
        //    inkCanvas.OnPointerPressed(e);
        //}

        //private void CanvasContainer_PointerReleased(object sender, PointerRoutedEventArgs e) {
        //    inkCanvas.OnPointerReleased(e);
        //}

        //private void CanvasContainer_PointerExited(object sender, PointerRoutedEventArgs e) {
        //    inkCanvas.OnPointerExited(e);
        //}

        //private void AspectRatio_ItemClick(object sender, ItemClickEventArgs e) {
        //    inkCanvas._viewModel.ConfigData.SeletcedAspectitem = e.ClickedItem as AspectRatioItem;
        //}

        //private void CropCancelBtn_Click(object sender, RoutedEventArgs e) {
        //    inkCanvas.CancelCrop();
        //}

        //private void CropCommitBtn_Click(object sender, RoutedEventArgs e) {
        //    inkCanvas.CommitCrop();
        //}

        //private void SelectCancelBtn_Click(object sender, RoutedEventArgs e) {
        //    inkCanvas.CancelSelect();
        //}

        //private void SelectCommitBtn_Click(object sender, RoutedEventArgs e) {
        //    inkCanvas.CommitSelect();
        //}

        //private static void CloseSizeIllegalMsg() {
        //    MainPage.Instance.Bridge.GetNotify().CloseAndRemoveMsg(nameof(Constants.I18n.StaticImg_CanvasSizeInput_Illegal));
        //}

        //private static void ShowSizeIllegalMsg() {
        //    MainPage.Instance.Bridge.GetNotify().ShowMsg(
        //        true,
        //        nameof(Constants.I18n.StaticImg_CanvasSizeInput_Illegal),
        //        InfoBarType.Error,
        //        MAX_CANVAS_SIZE_WITH_DPI.ToString(),
        //        nameof(Constants.I18n.StaticImg_CanvasSizeInput_Illegal),
        //        false);
        //}

        //private void LockAspectRatio_Checked(object sender, RoutedEventArgs e) {
        //    _isLockAspectRatio = true;
        //}

        //private void LockAspectRatio_Unchecked(object sender, RoutedEventArgs e) {
        //    _isLockAspectRatio = false;
        //}

        //private void SacleContent_Checked(object sender, RoutedEventArgs e) {
        //    _isScaleContent = true;
        //}

        //private void SacleContent_Unchecked(object sender, RoutedEventArgs e) {
        //    _isScaleContent = false;
        //}

        //private void SizeBoxLostFocus(object sender, RoutedEventArgs e) {
        //    if (_isKeyboardExecuted) {
        //        _isKeyboardExecuted = false;
        //        return;
        //    }
        //    var box = (TextBox)sender;
        //    ProcessSizeInput(box);
        //}

        //private void SizeBoxKeyDown(object sender, KeyRoutedEventArgs e) {
        //    if (e.Key == Windows.System.VirtualKey.Enter) {
        //        _isKeyboardExecuted = true;
        //        ProcessSizeInput((TextBox)sender);
        //        e.Handled = true;
        //    }
        //    else if (e.Key == Windows.System.VirtualKey.Escape) {
        //        ResetToOriginalValues();
        //        e.Handled = true;
        //    }
        //}

        //private void ProcessSizeInput(TextBox modifiedBox) {
        //    bool isWidthModified = modifiedBox == widthTextBox;
        //    bool op1 = ValidateSizeInput(widthTextBox.Text, out int width);
        //    bool op2 = ValidateSizeInput(heightTextBox.Text, out int height);
        //    bool isValid = op1 && op2;

        //    if (!isValid) {
        //        ShowSizeIllegalMsg();
        //        ResetToOriginalValues();
        //        return;
        //    }

        //    if (_isLockAspectRatio) {
        //        if (isWidthModified) {
        //            isValid = ValidateSizeInput(
        //                (width / inkCanvas._viewModel.ConfigData.Size.Ratio).ToString("F0"),
        //                out height);
        //        }
        //        else {
        //            isValid = ValidateSizeInput(
        //                (height * inkCanvas._viewModel.ConfigData.Size.Ratio).ToString("F0"),
        //                out width);
        //        }

        //        if (!isValid) {
        //            ResetToOriginalValues();
        //            ShowSizeIllegalMsg();
        //            return;
        //        }
        //    }

        //    var rebuild = _isScaleContent ? RebuildMode.ResizeScale : RebuildMode.ResizeExpand;
        //    inkCanvas._viewModel.ConfigData.Size = new(width, height, inkCanvas._viewModel.ConfigData.Size.Dpi, rebuild);
        //    CloseSizeIllegalMsg();
        //}

        //private static bool ValidateSizeInput(string text, out int res) {
        //    if (string.IsNullOrEmpty(text)) {
        //        res = 0;
        //        return false;
        //    }

        //    var op = int.TryParse(text, out res) &&
        //        res >= 1 &&
        //        res <= MAX_CANVAS_SIZE_WITH_DPI;

        //    return op;
        //}

        //private void ResetToOriginalValues() {
        //    widthTextBox.Text = inkCanvas._viewModel.ConfigData.Size.Width.ToString("F0");
        //    heightTextBox.Text = inkCanvas._viewModel.ConfigData.Size.Height.ToString("F0");
        //}

        //private void CanvasOperationBtn_Click(object sender, RoutedEventArgs e) {
        //    RebuildMode rm = (CanvasOperation)((Button)sender).Tag switch {
        //        CanvasOperation.RotateLeft => RebuildMode.RotateLeft,
        //        CanvasOperation.RotateRight => RebuildMode.RotateRight,
        //        CanvasOperation.FlipHorizontally => RebuildMode.FlipHorizontal,
        //        CanvasOperation.FlipVertically => RebuildMode.FlipVertical,
        //        _ => RebuildMode.None,
        //    };
        //    inkCanvas._viewModel.ConfigData.Size = new(
        //        inkCanvas._viewModel.ConfigData.Size.Width,
        //        inkCanvas._viewModel.ConfigData.Size.Height,
        //        inkCanvas._viewModel.ConfigData.Size.Dpi,
        //        rm);
        //}

        //private bool _isScaleContent;
        //private bool _isLockAspectRatio;
        //private bool _isKeyboardExecuted;
        //private LayerItem _rightTappedItem;
        internal readonly MainPageViewModel _viewModel;
        //private static int MAX_CANVAS_EDGE => MainPage.Instance.SharedDevice.MaximumBitmapSizeInPixels;
        //private static int MAX_CANVAS_SIZE_WITH_DPI => (int)(1.0F * MAX_CANVAS_EDGE / MainPage.Instance.Bridge.GetHardwareDpi() * 96);
    }
}
