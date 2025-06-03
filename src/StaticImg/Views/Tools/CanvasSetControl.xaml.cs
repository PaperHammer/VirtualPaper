using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class CanvasSetControl : UserControl {
        public event EventHandler<RoutedEventArgs> LockAspectRatioChecked;
        public event EventHandler<RoutedEventArgs> LockAspectRatioUnchecked;
        public event EventHandler<RoutedEventArgs> ScaleContentChecked;
        public event EventHandler<RoutedEventArgs> ScaleContentUnchecked;
        public event EventHandler<RoutedEventArgs> CanvasOperationRequested;
        public event EventHandler<ArcSize> OnValueChanged;

        public ArcSize Size {
            get { return (ArcSize)GetValue(SizeProperty); }
            set { SetValue(SizeProperty, value); }
        }
        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register("Size", typeof(ArcSize), typeof(CanvasSetControl), new PropertyMetadata(0));

        //public int PixelWidth {
        //    get { return (int)GetValue(PixelWidthProperty); }
        //    set { SetValue(PixelWidthProperty, value); }
        //}
        //public static readonly DependencyProperty PixelWidthProperty =
        //    DependencyProperty.Register("PixelWidth", typeof(int), typeof(CanvasSetControl), new PropertyMetadata(0));

        //public int PixelHeight {
        //    get { return (int)GetValue(PixelHeigthProperty); }
        //    set { SetValue(PixelHeigthProperty, value); }
        //}
        //public static readonly DependencyProperty PixelHeigthProperty =
        //    DependencyProperty.Register("PixelHeight", typeof(int), typeof(CanvasSetControl), new PropertyMetadata(0));
       
        public CanvasSetControl() {
            this.InitializeComponent();
        }

        private void OnSizeBoxLostFocus(object sender, RoutedEventArgs e) {
            if (_isKeyboardExecuted) {
                _isKeyboardExecuted = false;
                return;
            }
            var box = (TextBox)sender;
            ProcessSizeInput(box);
        }

        private void OnSizeBoxKeyDown(object sender, KeyRoutedEventArgs e) {
            if (e.Key == Windows.System.VirtualKey.Enter) {
                _isKeyboardExecuted = true;
                ProcessSizeInput((TextBox)sender);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape) {
                ResetToOriginalValues();
                e.Handled = true;
            }
        }

        private void ProcessSizeInput(TextBox modifiedBox) {
            bool isWidthModified = modifiedBox == widthTextBox;
            bool op1 = ValidateSizeInput(widthTextBox.Text, out int width);
            bool op2 = ValidateSizeInput(heightTextBox.Text, out int height);
            bool isValid = op1 && op2;

            if (!isValid) {
                ShowSizeIllegalMsg();
                ResetToOriginalValues();
                return;
            }

            if (_isLockAspectRatio) {
                if (isWidthModified) {
                    isValid = ValidateSizeInput(
                        (width / Size.Ratio).ToString("F0"),
                        out height);
                }
                else {
                    isValid = ValidateSizeInput(
                        (height * Size.Ratio).ToString("F0"),
                        out width);
                }

                if (!isValid) {
                    ResetToOriginalValues();
                    ShowSizeIllegalMsg();
                    return;
                }
            }

            var rebuild = _isScaleContent ? RebuildMode.ResizeScale : RebuildMode.ResizeExpand;
            OnValueChanged?.Invoke(this, new ArcSize(width, height, Size.Dpi, rebuild));
            CloseSizeIllegalMsg();
        }

        private static bool ValidateSizeInput(string text, out int res) {
            if (string.IsNullOrEmpty(text)) {
                res = 0;
                return false;
            }

            var op = int.TryParse(text, out res) &&
                res >= 1 &&
                res <= MAX_CANVAS_SIZE_WITH_DPI;

            return op;
        }

        private void ResetToOriginalValues() {
            widthTextBox.Text = Size.Width.ToString("F0");
            heightTextBox.Text = Size.Height.ToString("F0");
        }

        private static void CloseSizeIllegalMsg() {
            MainPage.Instance.Bridge.GetNotify().CloseAndRemoveMsg(nameof(Constants.I18n.StaticImg_CanvasSizeInput_Illegal));
        }

        private static void ShowSizeIllegalMsg() {
            MainPage.Instance.Bridge.GetNotify().ShowMsg(
                true,
                nameof(Constants.I18n.StaticImg_CanvasSizeInput_Illegal),
                InfoBarType.Error,
                MAX_CANVAS_SIZE_WITH_DPI.ToString(),
                nameof(Constants.I18n.StaticImg_CanvasSizeInput_Illegal),
                false);
        }

        private void LockAspectRatio_Checked(object sender, RoutedEventArgs e) {
            _isLockAspectRatio = true;
            LockAspectRatioChecked?.Invoke(this, e);
        }

        private void LockAspectRatio_Unchecked(object sender, RoutedEventArgs e) {
            _isLockAspectRatio = false;
            LockAspectRatioUnchecked?.Invoke(this, e);
        }

        private void SacleContent_Checked(object sender, RoutedEventArgs e) {
            _isScaleContent = true;
            ScaleContentChecked?.Invoke(this, e);
        }

        private void SacleContent_Unchecked(object sender, RoutedEventArgs e) {
            _isScaleContent = false;
            ScaleContentUnchecked?.Invoke(this, e);
        }

        private void CanvasOperationBtn_Click(object sender, RoutedEventArgs e) {
            CanvasOperationRequested?.Invoke(this, e);
        }

        private bool _isKeyboardExecuted;
        private bool _isLockAspectRatio;
        private bool _isScaleContent;

        private static int MAX_CANVAS_EDGE => MainPage.Instance.SharedDevice.MaximumBitmapSizeInPixels;
        private static int MAX_CANVAS_SIZE_WITH_DPI => (int)(1.0F * MAX_CANVAS_EDGE / MainPage.Instance.Bridge.GetHardwareDpi() * 96);

        private readonly string _SIG_CanvasSet_Header = nameof(Constants.I18n.SIG_CanvasSet_Header); // 画布
        private readonly string _SIG_CanvasSet_AdjustSize = nameof(Constants.I18n.SIG_CanvasSet_AdjustSize); // 调整画布大小
        private readonly string _SIG_CanvasSet_PixelWidth = nameof(Constants.I18n.SIG_CanvasSet_PixelWidth); // 宽度(像素)
        private readonly string _SIG_CanvasSet_PixelHeight = nameof(Constants.I18n.SIG_CanvasSet_PixelHeight); // 高度(像素)
        private readonly string _SIG_CanvasSet_LockAspectRatio = nameof(Constants.I18n.SIG_CanvasSet_LockAspectRatio); // 锁定纵横比
        private readonly string _SIG_CanvasSet_SacleContent = nameof(Constants.I18n.SIG_CanvasSet_SacleContent); // 同步缩放画布内容
        private readonly string _SIG_CanvasSet_RotateAndFlip = nameof(Constants.I18n.SIG_CanvasSet_RotateAndFlip); // 旋转和翻转
        private readonly string _SIG_CanvasSet_RotateLeftNinety = nameof(Constants.I18n.SIG_CanvasSet_RotateLeftNinety); // 向左旋转90°
        private readonly string _SIG_CanvasSet_RotateRightNinety = nameof(Constants.I18n.SIG_CanvasSet_RotateRightNinety); // 向右旋转90°
        private readonly string _SIG_CanvasSet_FlipHorizon = nameof(Constants.I18n.SIG_CanvasSet_FlipHorizon); // 水平翻转
        private readonly string _SIG_CanvasSet_FlipVertical = nameof(Constants.I18n.SIG_CanvasSet_FlipVertical); // 垂直翻转
    }
}
