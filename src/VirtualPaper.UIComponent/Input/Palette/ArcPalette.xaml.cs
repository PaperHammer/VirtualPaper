using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Models.Mvvm;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Input {
    public sealed partial class ArcPalette : UserControl {
        public ICommand AddToCustomCommand { get; }

        public Brush ForegroundColor {
            get => (Brush)GetValue(ForegroundColorProperty);
            private set => SetValue(ForegroundColorProperty, value);
        }
        public static readonly DependencyProperty ForegroundColorProperty =
            DependencyProperty.Register(
                nameof(ForegroundColor),
                typeof(Brush),
                typeof(ArcPalette),
                new PropertyMetadata(new SolidColorBrush(Colors.Black)));

        public Brush BackgroundColor {
            get => (Brush)GetValue(BackgroundColorProperty);
            private set => SetValue(BackgroundColorProperty, value);
        }
        public static readonly DependencyProperty BackgroundColorProperty =
            DependencyProperty.Register(
                nameof(BackgroundColor),
                typeof(Brush),
                typeof(ArcPalette),
                new PropertyMetadata(new SolidColorBrush(Colors.White)));

        public Brush AColor {
            get => (Brush)GetValue(AColorProperty);
            set => SetValue(AColorProperty, value);
        }
        public static readonly DependencyProperty AColorProperty =
            DependencyProperty.Register(
                nameof(AColor),
                typeof(Brush),
                typeof(ArcPalette),
                new PropertyMetadata(new SolidColorBrush(Colors.Black), OnColorChanged));

        public Brush BColor {
            get => (Brush)GetValue(BColorProperty);
            set => SetValue(BColorProperty, value);
        }
        public static readonly DependencyProperty BColorProperty =
            DependencyProperty.Register(
                nameof(BColor),
                typeof(Brush),
                typeof(ArcPalette),
                new PropertyMetadata(new SolidColorBrush(Colors.White), OnColorChanged));

        public ObservableCollection<SolidColorBrush> CustomColors { get; set; } = [];

        private Selection _curSelection = Selection.A;
        private Selection CurrentSelection {
            get => _curSelection;
            set { _curSelection = value; OnSelectionChanged(); }
        }

        public ArcPalette() {
            this.InitializeComponent();
            AddToCustomCommand = new RelayCommand(OnAddToCustom);

            UpdateColors();
            InitializeCustomColors();
        }

        private void InitializeCustomColors() {
            for (int i = 0; i < 10; i++) {
                CustomColors.Add(new SolidColorBrush(Colors.Transparent));
            }
        }

        private void OnAddToCustom() {
            var color = arcColorPicker.Color;
            if (color == Colors.Transparent) return;

            var brush = new SolidColorBrush(color);
            CustomColors.Remove(brush);
            if (CustomColors.Count > 9) {
                CustomColors.RemoveAt(9);
            }
            CustomColors.Insert(0, brush);
        }

        private void OnSelectionChanged() {
            UpdateColors();
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ArcPalette palette) {
                palette.CurrentSelection = palette.ForegroundColor == palette.AColor ? Selection.A : Selection.B;
            }
        }

        private void UpdateColors() {
            ForegroundColor = CurrentSelection == Selection.A ? AColor : BColor;
            BackgroundColor = CurrentSelection == Selection.A ? BColor : AColor;

            UpdateVisual();
        }

        private void UpdateVisual() {
            aColor.BorderThickness = CurrentSelection == Selection.A ? new Thickness(2) : new Thickness(0);
            bColor.BorderThickness = CurrentSelection == Selection.B ? new Thickness(2) : new Thickness(0);
        }

        private void A_ColorBtn_Click(object sender, RoutedEventArgs e) {
            CurrentSelection = Selection.A;
        }

        private void B_ColorBtn_Click(object sender, RoutedEventArgs e) {
            CurrentSelection = Selection.B;
        }

        private void SelectColorBtn_Click(object sender, RoutedEventArgs e) {
            var targetBrush = (sender as FrameworkElement).Tag as SolidColorBrush;
            if (targetBrush == null || targetBrush.Color == Colors.Transparent) return;

            ForegroundColor = targetBrush; // 避免更新选择项
            _ = CurrentSelection == Selection.A ? AColor = targetBrush : BColor = targetBrush;
        }

        // TODO: 选定 BColor, 当 AColor 被切换为 BColor 时，会发生不和预期的交换选择项。但不影响使用
        private void RightSelectionBtn_Click(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e) {
            var targetBrush = (sender as FrameworkElement).Tag as SolidColorBrush;
            if (targetBrush == null || targetBrush.Color == Colors.Transparent) return;

            Background = targetBrush; // 避免更新选择项
            _ = CurrentSelection == Selection.A ? BColor = targetBrush : AColor = targetBrush;
        }

        private void OpenArcColorPickerBtn_Click(object sender, RoutedEventArgs e) {
            arcColorPicker.Visibility = _arcColorPickerVisible ? Visibility.Collapsed : Visibility.Visible;
            _arcColorPickerVisible = !_arcColorPickerVisible;
        }

        private bool _arcColorPickerVisible;
    }

    enum Selection {
        A,
        B
    }
}
