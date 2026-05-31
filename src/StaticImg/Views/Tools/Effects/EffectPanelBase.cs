using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Shader;
using Workloads.Creation.StaticImg.Utils;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    /// <summary>效果参数面板基类</summary>
    public abstract class EffectPanelBase : UserControl {
        public event EventHandler<EffectParams>? ParamsChanged;

        public abstract EffectParams Params { get; }

        protected void RaiseParamsChanged() => ParamsChanged?.Invoke(this, Params);

        protected static TextBlock CreateLabel(string text) => new() {
            Text = text,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };

        protected static TextBlock CreateValueText(double value) => new() {
            Text = value.ToString("0.#"),
            FontSize = 14,
            MinWidth = 66,
            TextAlignment = TextAlignment.Center,
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        protected Slider CreateSlider(string label, float min, float max, float value, StackPanel parent) {
            return CreateSliderCore(label, min, max, value, parent, null);
        }

        protected Slider CreateGradientSlider(string label, float min, float max, float value, StackPanel parent, Brush brush) {
            return CreateSliderCore(label, min, max, value, parent, brush);
        }

        private Slider CreateSliderCore(string label, float min, float max, float value, StackPanel parent, Brush? trackBrush) {
            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var valueText = CreateValueText(value);
            Grid.SetColumn(valueText, 1);
            titleGrid.Children.Add(CreateLabel(label));
            titleGrid.Children.Add(valueText);

            var slider = new Slider {
                Minimum = min,
                Maximum = max,
                Value = value,
                TickFrequency = Math.Max(1, Math.Abs(max - min) / 8),
                TickPlacement = TickPlacement.BottomRight,
                SmallChange = Math.Max(1, Math.Abs(max - min) / 100),
                StepFrequency = Math.Max(1, Math.Abs(max - min) / 100),
                VerticalAlignment = VerticalAlignment.Center,
            };
            slider.ValueChanged += (_, _) => {
                valueText.Text = slider.Value.ToString("0.#");
                RaiseParamsChanged();
            };

            if (trackBrush != null) {
                slider.Background = trackBrush;
                slider.Foreground = trackBrush;
            }

            parent.Children.Add(titleGrid);
            parent.Children.Add(slider);
            return slider;
        }

        protected static LinearGradientBrush Gradient(params (double offset, Windows.UI.Color color)[] stops) {
            var brush = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 0) };
            foreach (var (offset, color) in stops)
                brush.GradientStops.Add(new GradientStop { Offset = offset, Color = color });
            return brush;
        }

        protected static Grid CreateGridBox(double width, double height, int divisions = 4) {
            var grid = new Grid { Width = width, Height = height };
            var stroke = new SolidColorBrush(Colors.Gray) { Opacity = 0.55 };
            for (int i = 0; i <= divisions; i++) {
                double x = width * i / divisions;
                double y = height * i / divisions;
                grid.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = height, Stroke = stroke, StrokeThickness = 1 });
                grid.Children.Add(new Line { X1 = 0, Y1 = y, X2 = width, Y2 = y, Stroke = stroke, StrokeThickness = 1 });
            }
            grid.Children.Add(new Polyline {
                Points = { new Windows.Foundation.Point(0, height), new Windows.Foundation.Point(width * 0.35, height * 0.75), new Windows.Foundation.Point(width * 0.7, height * 0.35), new Windows.Foundation.Point(width, 0) },
                Stroke = new SolidColorBrush(Colors.WhiteSmoke),
                StrokeThickness = 1.2,
            });
            return grid;
        }
    }

    public sealed partial class SingleSliderEffectPanel : EffectPanelBase {
        private readonly Slider _slider;

        public SingleSliderEffectPanel(EffectSliderConfig cfg) {
            var root = new StackPanel { Spacing = 8 };
            _slider = CreateSlider(cfg.Label, cfg.Min, cfg.Max, cfg.Default, root);
            Content = root;
        }

        public override EffectParams Params => new() { Value = (float)_slider.Value, Dpi = 96f };
    }

    public sealed partial class DoubleSliderEffectPanel : EffectPanelBase {
        private readonly Slider _slider1;
        private readonly Slider _slider2;

        public DoubleSliderEffectPanel(EffectSliderConfig cfg) {
            var root = new StackPanel { Spacing = 8 };
            _slider1 = CreateSlider(cfg.Label, cfg.Min, cfg.Max, cfg.Default, root);
            _slider2 = CreateSlider(cfg.Label2, cfg.Min2, cfg.Max2, cfg.Default2, root);
            Content = root;
        }

        public override EffectParams Params => new() {
            Value = (float)_slider1.Value,
            Value2 = (float)_slider2.Value,
            Dpi = 96f,
        };
    }

    public sealed partial class BrightnessEffectPanel : EffectPanelBase {
        private const double CurveWidth = 260;
        private const double CurveHeight = 170;
        private const double ThumbSize = 22;

        private readonly Canvas _curveCanvas;
        private readonly Border _mainPoint;
        private double _brightness;
        private bool _isDragging;

        public BrightnessEffectPanel() {
            var root = new Grid { Width = 420, Height = 220 };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var presets = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            foreach (var v in new[] { 150, 125, 100, 75, 50 }) {
                var btn = new Button { Content = v.ToString(), Height = 36, HorizontalAlignment = HorizontalAlignment.Stretch };
                btn.Click += (_, _) => SetBrightness(v - 100);
                presets.Children.Add(btn);
            }
            root.Children.Add(presets);

            _curveCanvas = new Canvas {
                Width = CurveWidth,
                Height = CurveHeight,
                Margin = new Thickness(18, 0, 0, 0),
                Background = new SolidColorBrush(Colors.Transparent),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(_curveCanvas, 1);
            BuildBrightnessGrid();

            _mainPoint = CreateCurvePoint();
            _mainPoint.PointerPressed += CurvePoint_PointerPressed;
            _mainPoint.PointerMoved += CurvePoint_PointerMoved;
            _mainPoint.PointerReleased += CurvePoint_PointerReleased;
            _mainPoint.PointerExited += CurvePoint_PointerReleased;
            _curveCanvas.Children.Add(_mainPoint);

            root.Children.Add(_curveCanvas);
            Content = root;
            SetBrightness(0, false);
        }

        public override EffectParams Params => new() {
            Value = (float)_brightness,
            Dpi = 96f,
        };

        private void BuildBrightnessGrid() {
            var stroke = new SolidColorBrush(Colors.Gray) { Opacity = 0.55 };
            for (int i = 0; i <= 4; i++) {
                double x = CurveWidth * i / 4;
                double y = CurveHeight * i / 4;
                _curveCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = CurveHeight, Stroke = stroke, StrokeThickness = 1 });
                _curveCanvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = CurveWidth, Y2 = y, Stroke = stroke, StrokeThickness = 1 });
            }
            _curveCanvas.Children.Add(new Polyline {
                Points = { new Windows.Foundation.Point(0, CurveHeight), new Windows.Foundation.Point(CurveWidth * 0.35, CurveHeight * 0.7), new Windows.Foundation.Point(CurveWidth * 0.7, CurveHeight * 0.35), new Windows.Foundation.Point(CurveWidth, 0) },
                Stroke = new SolidColorBrush(Colors.WhiteSmoke),
                StrokeThickness = 1.2,
            });
        }

        private static Border CreateCurvePoint() => new() {
            Width = ThumbSize,
            Height = ThumbSize,
            CornerRadius = new CornerRadius(ThumbSize / 2),
            Background = new SolidColorBrush(Colors.WhiteSmoke),
            BorderBrush = new SolidColorBrush(Colors.DimGray),
            BorderThickness = new Thickness(4),
        };

        private void CurvePoint_PointerPressed(object sender, PointerRoutedEventArgs e) {
            _isDragging = true;
            _mainPoint.CapturePointer(e.Pointer);
            UpdateFromPoint(e.GetCurrentPoint(_curveCanvas).Position);
        }

        private void CurvePoint_PointerMoved(object sender, PointerRoutedEventArgs e) {
            if (!_isDragging) return;
            UpdateFromPoint(e.GetCurrentPoint(_curveCanvas).Position);
        }

        private void CurvePoint_PointerReleased(object sender, PointerRoutedEventArgs e) {
            _isDragging = false;
            _mainPoint.ReleasePointerCapture(e.Pointer);
        }

        private void UpdateFromPoint(Windows.Foundation.Point p) {
            double y = Math.Clamp(p.Y, 0, CurveHeight);
            double value = (0.5 - y / CurveHeight) * 200; // -100~100, center=0
            SetBrightness(value);
        }

        private void SetBrightness(double value, bool notify = true) {
            _brightness = Math.Clamp(value, -100, 100);
            double y = (0.5 - _brightness / 200) * CurveHeight;
            Canvas.SetLeft(_mainPoint, CurveWidth * 0.5 - ThumbSize / 2);
            Canvas.SetTop(_mainPoint, y - ThumbSize / 2);
            if (notify) RaiseParamsChanged();
        }
    }

    public sealed partial class EmptyEffectPanel : EffectPanelBase {
        public EmptyEffectPanel() {
            Content = new TextBlock {
                Text = "无需参数",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
        }

        public override EffectParams Params => EffectParams.Default;
    }
}
