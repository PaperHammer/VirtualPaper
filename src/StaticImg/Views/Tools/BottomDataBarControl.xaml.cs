using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class BottomDataBarControl : UserControl {
        public event EventHandler<RoutedEventArgs> FitViewRequest;
        public event EventHandler<SelectionChangedEventArgs> ZoomComboBoxSelectionChanged;
        public event EventHandler<ComboBoxTextSubmittedEventArgs> ZoomComboBoxTextSubmitted;
        public event EventHandler<RoutedEventArgs> ZoomOutRequest;
        public event EventHandler<RoutedEventArgs> ZoomInRequest;
        public event EventHandler<RangeBaseValueChangedEventArgs> ZoomSliderValueChanged;

        public string PointerPosText {
            get { return (string)GetValue(PointerPosTextProperty); }
            set { SetValue(PointerPosTextProperty, value); }
        }
        public static readonly DependencyProperty PointerPosTextProperty =
            DependencyProperty.Register(nameof(PointerPosText), typeof(string), typeof(BottomDataBarControl), new PropertyMetadata(string.Empty));

        public string SelectionSizeText {
            get { return (string)GetValue(SelectionSizeTextProperty); }
            set { SetValue(SelectionSizeTextProperty, value); }
        }
        public static readonly DependencyProperty SelectionSizeTextProperty =
            DependencyProperty.Register(nameof(SelectionSizeText), typeof(string), typeof(BottomDataBarControl), new PropertyMetadata(string.Empty));

        public string CanvasSizeText {
            get { return (string)GetValue(CanvasSizeTextProperty); }
            set { SetValue(CanvasSizeTextProperty, value); }
        }
        public static readonly DependencyProperty CanvasSizeTextProperty =
            DependencyProperty.Register(nameof(CanvasSizeText), typeof(string), typeof(BottomDataBarControl), new PropertyMetadata(string.Empty));

        public float CanvasZoom {
            get { return (float)GetValue(CanvasZoomProperty); }
            set { SetValue(CanvasZoomProperty, value); }
        }
        public static readonly DependencyProperty CanvasZoomProperty =
            DependencyProperty.Register(nameof(CanvasZoom), typeof(float), typeof(BottomDataBarControl), new PropertyMetadata(0, OnCanvasZoomChanged));

        private static void OnCanvasZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var instance = d as BottomDataBarControl;
            if (instance == null) return;
            double newValue = Consts.DecimalToPercent((float)e.NewValue, 1);
            instance.ZoomComboBox.SelectedItem = $"{newValue}%";
            instance._lastCanvasZoomText = $"{newValue}%";
            instance.zoomSlider.Value = newValue;            
        }

        public BottomDataBarControl() {
            this.InitializeComponent();
        }

        private void FitView_ButtonClick(object sender, RoutedEventArgs e) {
            FitViewRequest?.Invoke(sender, e);
        }

        private void ZoomComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count == 0) return;

            var selectedText = e.AddedItems[0].ToString();
            ZoomComboBoxSelectionChanged?.Invoke(sender, e);
        }

        private void ZoomComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args) {
            if (TryParseZoomInput(args.Text, out float _)) {
                ZoomComboBoxTextSubmitted?.Invoke(sender, args);
            }
            else {
                RestoreZoomDisplay();
            }
            args.Handled = true; // 关键点：避免 ZoomComboBox_SelectionChanged 二次处理导致数据显示异常
        }

        private static bool TryParseZoomInput(string input, out float result) {
            if (double.TryParse(input?.TrimEnd('%'), out double percent) && Consts.IsZoomValid(percent / 100)) {
                result = (float)(percent / 100);
                return true;
            }

            result = default;
            return false;
        }

        private void RestoreZoomDisplay() {
            ZoomComboBox.SelectedItem = _lastCanvasZoomText;
            //ZoomComboBox.SelectedItem = $"{Consts.DecimalToPercent(CanvasZoom),1}%";
        }

        private void ZoomOut_ButtonClick(object sender, RoutedEventArgs e) {
            ZoomOutRequest?.Invoke(sender, e);
        }

        private void ZoomIn_ButtonClick(object sender, RoutedEventArgs e) {
            ZoomInRequest?.Invoke(sender, e);
        }

        private void ZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) {
            if (zoomSlider.FocusState == FocusState.Unfocused) return;
            ZoomSliderValueChanged?.Invoke(sender, e);
        }

        private readonly string[] _comboZoomFactors = ["800%", "700%", "600%", "500%", "400%", "300%", "200%", "100%", "75%", "50%", "25%"];
        private string _lastCanvasZoomText;
    }
}
