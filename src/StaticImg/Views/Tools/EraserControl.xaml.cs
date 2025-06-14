using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class EraserControl : UserControl {
        public double EraserSize {
            get { return (double)GetValue(EraserSizeProperty); }
            set { SetValue(EraserSizeProperty, value); }
        }
        public static readonly DependencyProperty EraserSizeProperty =
            DependencyProperty.Register(nameof(EraserSize), typeof(double), typeof(PaintBrushControl), new PropertyMetadata(0));

        public double EraserOpacity {
            get { return (double)GetValue(EraserOpacityProperty); }
            set { SetValue(EraserOpacityProperty, value); }
        }
        public static readonly DependencyProperty EraserOpacityProperty =
            DependencyProperty.Register(nameof(EraserOpacity), typeof(double), typeof(PaintBrushControl), new PropertyMetadata(0));

        public EraserControl() {
            this.InitializeComponent();
        }

        private void EraserSizeTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
            string input = sender.Text;

            if (string.IsNullOrWhiteSpace(input)) {
                return;
            }

            if (!int.TryParse(input, out int parsedValue) ||
                parsedValue < 1 || parsedValue > 100) {
                sender.Text = EraserSize.ToString();
                sender.SelectionStart = sender.Text.Length;
                return;
            }

            //inkCanvas._viewModel.ConfigData.EraserSize = parsedValue;
        }

        private void EraserSizeTextBox_LostFocus(object sender, RoutedEventArgs e) {
            if (eraserSizeTextBox.Text.Trim().Length == 0) {
                eraserSizeTextBox.Text = EraserSize.ToString();
            }
        }

        private void EraserOpacityTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
            string input = sender.Text;

            if (string.IsNullOrWhiteSpace(input)) {
                return;
            }

            if (!int.TryParse(input, out int parsedValue) ||
                parsedValue < 1 || parsedValue > 100) {
                sender.Text = EraserOpacity.ToString();
                sender.SelectionStart = sender.Text.Length;
                return;
            }

            //inkCanvas._viewModel.ConfigData.EraserOpacity = parsedValue;
        }

        private void EraserOpacityTextBox_LostFocus(object sender, RoutedEventArgs e) {
            if (eraserOpacityTextBox.Text.Trim().Length == 0) {
                eraserOpacityTextBox.Text = EraserOpacity.ToString();
            }
        }
    }
}
