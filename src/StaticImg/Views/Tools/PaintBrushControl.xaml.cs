using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Workloads.Creation.StaticImg.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class PaintBrushControl : UserControl {
        public PaintBrushItem SelectedBrush {
            get { return (PaintBrushItem)GetValue(SelectedBrushProperty); }
            set { SetValue(SelectedBrushProperty, value); }
        }
        public static readonly DependencyProperty SelectedBrushProperty =
            DependencyProperty.Register(nameof(SelectedBrush), typeof(PaintBrushItem), typeof(PaintBrushControl), new PropertyMetadata(null));

        public double BrushThickness {
            get { return (double)GetValue(BrushThicknessProperty); }
            set { SetValue(BrushThicknessProperty, value); }
        }
        public static readonly DependencyProperty BrushThicknessProperty =
            DependencyProperty.Register(nameof(BrushThickness), typeof(double), typeof(PaintBrushControl), new PropertyMetadata(0));

        public double BrushOpacity {
            get { return (double)GetValue(BrushOpacityProperty); }
            set { SetValue(BrushOpacityProperty, value); }
        }
        public static readonly DependencyProperty BrushOpacityProperty =
            DependencyProperty.Register(nameof(BrushOpacity), typeof(double), typeof(PaintBrushControl), new PropertyMetadata(0));

        public PaintBrushControl() {
            this.InitializeComponent();
        }

        private void PaintBrushExpander_Loaded(object sender, RoutedEventArgs e) {
            if (paintBrushListView.ItemsSource is IList<object> items && items.Count > 0) {
                paintBrushListView.SelectedItem = items[0];
            }
        }

        private void PaintBrushListView_ItemClick(object sender, ItemClickEventArgs e) {
            paintBrushFlyout?.Hide();
        }

        private void PaintBrushThicknessTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
            string input = sender.Text;

            if (string.IsNullOrWhiteSpace(input)) {
                return;
            }

            if (!int.TryParse(input, out int parsedValue) ||
                parsedValue < 1 || parsedValue > 100) {
                sender.Text = BrushThickness.ToString();
                sender.SelectionStart = sender.Text.Length;
                return;
            }
        }

        private void PaintBrushThicknessTextBox_LostFocus(object sender, RoutedEventArgs e) {
            if (paintBrushThicknessTextBox.Text.Trim().Length == 0) {
                paintBrushThicknessTextBox.Text = BrushThickness.ToString();
            }
        }

        private void PaintBrushOpacityTextBox_Changing(TextBox sender, TextBoxTextChangingEventArgs args) {
            string input = sender.Text;

            if (string.IsNullOrWhiteSpace(input)) {
                return;
            }

            if (!int.TryParse(input, out int parsedValue) ||
                parsedValue < 1 || parsedValue > 100) {
                sender.Text = BrushOpacity.ToString();
                sender.SelectionStart = sender.Text.Length;
                return;
            }
        }

        private void PaintBrushOpacityTextBox_LostFocus(object sender, RoutedEventArgs e) {
            if (paintBrushOpacityTextBox.Text.Trim().Length == 0) {
                paintBrushOpacityTextBox.Text = BrushOpacity.ToString();
            }
        }
    }
}
