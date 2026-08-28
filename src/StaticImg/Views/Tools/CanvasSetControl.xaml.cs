using System;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class CanvasSetControl : ArcUserControl {
        public event EventHandler<RoutedEventArgs>? LockAspectRatioChecked;
        public event EventHandler<RoutedEventArgs>? LockAspectRatioUnchecked;
        public event EventHandler<RoutedEventArgs>? ScaleContentChecked;
        public event EventHandler<RoutedEventArgs>? ScaleContentUnchecked;
        public event EventHandler<CanvasOperation>? CanvasOperationRequested;
        public event EventHandler<ArcSize>? OnValueCommited;

        public ArcSize Size {
            get { return (ArcSize)GetValue(SizeProperty); }
            set { SetValue(SizeProperty, value); }
        }
        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register(nameof(Size), typeof(ArcSize), typeof(CanvasSetControl), new PropertyMetadata(default));

        public ICommand? CanvasOperationCommand { get; private set; }

        public CanvasSetControl() {
            this.InitializeComponent();

            InitCommand();
        }

        private void InitCommand() {
            CanvasOperationCommand = new RelayCommand<CanvasOperation>((operation) => {
                CanvasOperationRequested?.Invoke(this, operation);
            });
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
            int maximumEdge = Consts.GetMaximumCanvasEdge(Size.Dpi);
            bool op1 = ValidateSizeInput(widthTextBox.Text, maximumEdge, out int width);
            bool op2 = ValidateSizeInput(heightTextBox.Text, maximumEdge, out int height);
            bool isValid = op1 && op2;

            if (!isValid) {
                ResetToOriginalValues();
                ShowSizeIllegalMsg(maximumEdge);
                return;
            }

            if (_isLockAspectRatio) {
                if (isWidthModified) {
                    isValid = ValidateSizeInput(
                        (width / Size.Ratio).ToString("F0"),
                        maximumEdge,
                        out height);
                }
                else {
                    isValid = ValidateSizeInput(
                        (height * Size.Ratio).ToString("F0"),
                        maximumEdge,
                        out width);
                }

                if (!isValid) {
                    ResetToOriginalValues();
                    ShowSizeIllegalMsg(maximumEdge);
                    return;
                }

                widthTextBox.Text = width.ToString();
                heightTextBox.Text = height.ToString();
            }

            var rebuild = _isScaleContent ? RebuildMode.ResizeScale : RebuildMode.ResizeExpand;
            OnValueCommited?.Invoke(this, new ArcSize(width, height, Size.Dpi, rebuild));
            CloseSizeIllegalMsg();
        }

        private static bool ValidateSizeInput(string text, int maximumEdge, out int res) {
            if (string.IsNullOrEmpty(text)) {
                res = 0;
                return false;
            }

            var op = int.TryParse(text, out res) &&
                res >= 1 &&
                res <= maximumEdge;

            return op;
        }

        private void ResetToOriginalValues() {
            widthTextBox.Text = Size.Width.ToString("F0");
            heightTextBox.Text = Size.Height.ToString("F0");
        }

        /// <summary>
        /// Restores both input boxes from the last canvas size accepted by the model.
        /// Used when an asynchronous resize fails after the values have been submitted.
        /// </summary>
        public void RestoreCurrentSize() => ResetToOriginalValues();

        private static void CloseSizeIllegalMsg() {
            GlobalMessageUtil.CloseAndRemoveMsg(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), nameof(Constants.I18n.StaticImg_CanvasSizeInput_Illegal));
        }

        private void ShowSizeIllegalMsg(int maximumEdge) {
            string messageTemplate = LanguageUtil.GetI18n(
                nameof(Constants.I18n.StaticImg_CanvasSizeInput_Illegal));
            string message = string.Format(
                messageTemplate,
                maximumEdge,
                Size.Width.ToString("F0"),
                Size.Height.ToString("F0"));

            // Replace an existing warning so its restored dimensions cannot become stale.
            CloseSizeIllegalMsg();
            GlobalMessageUtil.ShowError(
                message: message,
                key: nameof(Constants.I18n.StaticImg_CanvasSizeInput_Illegal),
                isNeedLocalizer: false);
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

        private bool _isKeyboardExecuted;
        private bool _isLockAspectRatio;
        private bool _isScaleContent;
    }
}
