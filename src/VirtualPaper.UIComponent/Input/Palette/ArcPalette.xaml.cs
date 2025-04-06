using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils.ArcEventArgs;
using VirtualPaper.UIComponent.Utils.Extensions;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Input {
    public sealed partial class ArcPalette : UserControl {
        public event EventHandler<ColorChnageEventArgs> OnCustomeColorChangedEvent;
        public ICommand AddToCustomCommand { get; }

        public Color ForegroundColor {
            get => (Color)GetValue(ForegroundColorProperty);
            set => SetValue(ForegroundColorProperty, value);
        }
        public static readonly DependencyProperty ForegroundColorProperty =
            DependencyProperty.Register(
                nameof(ForegroundColor),
                typeof(Color),
                typeof(ArcPalette),
                new PropertyMetadata(Colors.Black));

        public Color BackgroundColor {
            get => (Color)GetValue(BackgroundColorProperty);
            set => SetValue(BackgroundColorProperty, value);
        }
        public static readonly DependencyProperty BackgroundColorProperty =
            DependencyProperty.Register(
                nameof(BackgroundColor),
                typeof(Color),
                typeof(ArcPalette),
                new PropertyMetadata(Colors.White));

        internal SolidColorBrush AColor {
            get => (SolidColorBrush)GetValue(AColorProperty);
            private set => SetValue(AColorProperty, value);
        }
        public static readonly DependencyProperty AColorProperty =
            DependencyProperty.Register(
                nameof(AColor),
                typeof(SolidColorBrush),
                typeof(ArcPalette),
                new PropertyMetadata(new SolidColorBrush(Colors.Black), OnColorChanged));

        internal SolidColorBrush BColor {
            get => (SolidColorBrush)GetValue(BColorProperty);
            private set => SetValue(BColorProperty, value);
        }
        public static readonly DependencyProperty BColorProperty =
            DependencyProperty.Register(
                nameof(BColor),
                typeof(SolidColorBrush),
                typeof(ArcPalette),
                new PropertyMetadata(new SolidColorBrush(Colors.White), OnColorChanged));

        public ObservableList<Color> InitCustomColors {
            get { return (ObservableList<Color>)GetValue(InitCustomColorsProperty); }
            set { SetValue(InitCustomColorsProperty, value); }
        }
        public static readonly DependencyProperty InitCustomColorsProperty =
            DependencyProperty.Register(
                nameof(InitCustomColors),
                typeof(ObservableList<Color>),
                typeof(ArcPalette),
                new PropertyMetadata(null, OnInitCustomColorsInited));

        private ObservableList<SolidColorBrush> CustomBrushes { get; set; } = [];

        private Selection _curSelection = Selection.A;
        private Selection CurrentSelection {
            get => _curSelection;
            set { _curSelection = value; OnSelectionChanged(); }
        }

        public ArcPalette() {
            this.InitializeComponent();
            AddToCustomCommand = new RelayCommand(OnAddToCustom);

            InitializeArcColorPicker();
            UpdateColors();

        }

        private void InitializeArcColorPicker() {
            // 确保控件已经加载到视觉树中, 避免卡顿
            arcColorPicker.Visibility = Visibility.Collapsed;
        }

        private void InitCustomColors_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.NewItems != null && e.NewItems.Count > 0)
                UpdateToCustom((Color)e.NewItems[0]);
        }

        private static void OnInitCustomColorsInited(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ArcPalette instance) {
                instance.InitCustomColors.CollectionChanged += instance.InitCustomColors_CollectionChanged;

                var colors = e.NewValue as List<Color>;
                instance.CustomBrushes.Clear();
                foreach (var color in colors) {
                    instance.CustomBrushes.Add(new SolidColorBrush(color));
                }
                while (instance.CustomBrushes.Count < 10) {
                    instance.CustomBrushes.Add(new SolidColorBrush(Colors.Transparent));
                }
            }
        }

        private void OnAddToCustom() {
            var newColor = arcColorPicker.Color;
            if (newColor == Colors.Transparent) return;
           
            var eventArgs = new ColorChnageEventArgs {
                RemoveItem = CustomBrushes.FindBrushByColor(new SolidColorBrush(newColor))?.Color,
                AddItem = newColor
            };
            OnCustomeColorChangedEvent?.Invoke(this, eventArgs);
        }

        private void UpdateToCustom(Color newColor) {
            if (newColor == Colors.Transparent) return;

            var brush = new SolidColorBrush(newColor);
            var existingBrush = CustomBrushes.FindBrushByColor(brush);
            if (existingBrush != null) {
                CustomBrushes.Remove(existingBrush);
            }
            if (CustomBrushes.Count > 9) {
                CustomBrushes.RemoveAt(9);
            }
            CustomBrushes.Insert(0, brush);
        }

        private void OnSelectionChanged() {
            UpdateColors();
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ArcPalette palette) {
                palette.CurrentSelection = palette.ForegroundColor == palette.AColor.Color ? Selection.A : Selection.B;
            }
        }

        private void UpdateColors() {
            ForegroundColor = CurrentSelection == Selection.A ? AColor.Color : BColor.Color;
            BackgroundColor = CurrentSelection == Selection.A ? BColor.Color : AColor.Color;

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

            ForegroundColor = targetBrush.Color; // 避免更新选择项
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
