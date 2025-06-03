using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils.Extensions;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Input {
    public sealed partial class ArcPalette : UserControl {
        public event EventHandler<ColorChangeEventArgs> OnCustomeColorChangedEvent;
        public ICommand AddToCustomCommand { get; }

        public Color ForegroundColor {
            get => (Color)GetValue(ForegroundColorProperty);
            set { if (ForegroundColor == value) return; SetValue(ForegroundColorProperty, value); }
        }
        public static readonly DependencyProperty ForegroundColorProperty =
            DependencyProperty.Register(
                nameof(ForegroundColor),
                typeof(Color),
                typeof(ArcPalette),
                new PropertyMetadata(Colors.Transparent, OnOuterColorChanged));

        public Color BackgroundColor {
            get => (Color)GetValue(BackgroundColorProperty);
            set { if (BackgroundColor == value) return; SetValue(BackgroundColorProperty, value); }
        }
        public static readonly DependencyProperty BackgroundColorProperty =
            DependencyProperty.Register(
                nameof(BackgroundColor),
                typeof(Color),
                typeof(ArcPalette),
                new PropertyMetadata(Colors.Transparent, OnOuterColorChanged));

        /// <summary>
        /// 主颜色
        /// </summary>
        internal SolidColorBrush AColor {
            get => (SolidColorBrush)GetValue(AColorProperty);
            private set { if (AColor == value) return; SetValue(AColorProperty, value); }
        }
        internal static readonly DependencyProperty AColorProperty =
            DependencyProperty.Register(
                nameof(AColor),
                typeof(SolidColorBrush),
                typeof(ArcPalette),
                new PropertyMetadata(new SolidColorBrush(Colors.Black)));

        /// <summary>
        /// 副颜色
        /// </summary>
        internal SolidColorBrush BColor {
            get => (SolidColorBrush)GetValue(BColorProperty);
            private set { if (BColor == value) return; SetValue(BColorProperty, value); }
        }
        internal static readonly DependencyProperty BColorProperty =
            DependencyProperty.Register(
                nameof(BColor),
                typeof(SolidColorBrush),
                typeof(ArcPalette),
                new PropertyMetadata(new SolidColorBrush(Colors.White)));

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
            set { if (_curSelection == value) return; _curSelection = value; OnSelectionChanged(); }
        }

        public ArcPalette() {
            this.InitializeComponent();
            AddToCustomCommand = new RelayCommand(OnAddToCustom);

            //InitializeArcColorPicker();
            UpdateOuterColors();
        }

        //private void InitializeArcColorPicker() {
        //    // 确保控件已经加载到视觉树中, 避免卡顿
        //    //arcColorPicker.Visibility = Visibility.Collapsed;
        //}

        private static void OnOuterColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ArcPalette instance) {
                Debug.WriteLine($"OuterColorChanged: {instance.ForegroundColor} {instance.BackgroundColor}");
                instance.UpdateVisual();
            }
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

            var eventArgs = new ColorChangeEventArgs {
                OldItem = CustomBrushes.FindBrushByColor(new SolidColorBrush(newColor))?.Color,
                NewItem = newColor
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
            UpdateOuterColors();
        }

        //private static void OnInnerColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        //    if (d is ArcPalette palette) {
        //        palette.CurrentSelection = palette.ForegroundColor == palette.AColor.Color ? Selection.A : Selection.B;
        //    }
        //}

        private void UpdateOuterColors() {
            ForegroundColor = CurrentSelection == Selection.A ? AColor.Color : BColor.Color;
            BackgroundColor = CurrentSelection == Selection.A ? BColor.Color : AColor.Color;
        }

        internal void UpdateInnerColors() {
            AColor = CurrentSelection == Selection.A ? new SolidColorBrush(ForegroundColor) : new SolidColorBrush(BackgroundColor);
            BColor = CurrentSelection == Selection.A ? new SolidColorBrush(BackgroundColor) : new SolidColorBrush(ForegroundColor);
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

        private void RightSelectionBtn_Click(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e) {
            var targetBrush = (sender as FrameworkElement).Tag as SolidColorBrush;
            if (targetBrush == null || targetBrush.Color == Colors.Transparent) return;

            BackgroundColor = targetBrush.Color; // 避免更新选择项
            _ = CurrentSelection == Selection.A ? BColor = targetBrush : AColor = targetBrush;
        }

        private void OpenArcColorPickerBtn_Click(object sender, RoutedEventArgs e) {
            arcColorPicker.Visibility = _arcColorPickerVisible ? Visibility.Collapsed : Visibility.Visible;
            _arcColorPickerVisible = !_arcColorPickerVisible;
        }

        private bool _arcColorPickerVisible = true;
    }

    public class ColorChangeEventArgs : EventArgs {
        public Color? OldItem { get; set; }
        public Color? NewItem { get; set; }
    }

    enum Selection {
        A,
        B
    }
}
