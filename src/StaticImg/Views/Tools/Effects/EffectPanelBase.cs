using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Shader;
using VirtualPaper.UIComponent.Input;
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

        protected ArcSlider CreateSlider(string label, float min, float max, float value, StackPanel parent) {
            return CreateSliderCore(label, min, max, value, parent, null);
        }

        protected ArcSlider CreateGradientSlider(string label, float min, float max, float value, StackPanel parent, Brush brush) {
            return CreateSliderCore(label, min, max, value, parent, brush);
        }

        private ArcSlider CreateSliderCore(string label, float min, float max, float value, StackPanel parent, Brush? trackBrush) {
            var container = new StackPanel { Spacing = 2 };

            // 第一行：标签（左）+ NumberBox（右）
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            headerGrid.Children.Add(CreateLabel(label));

            var numberBox = new NumberBox {
                Value = value,
                Minimum = min,
                Maximum = max,
                SmallChange = Math.Max(1, Math.Abs(max - min) / 100),
                LargeChange = Math.Max(1, Math.Abs(max - min) / 10),
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
                ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 60,
                MaxWidth = 72,
                FontSize = 13,
                // 仅显示整数，禁止小数位
                NumberFormatter = new Windows.Globalization.NumberFormatting.DecimalFormatter {
                    FractionDigits = 0,
                    IsGrouped = false,
                },
            };
            Grid.SetColumn(numberBox, 1);
            headerGrid.Children.Add(numberBox);

            // 第二行：Slider（全宽）
            var slider = new ArcSlider {
                Minimum = min,
                Maximum = max,
                Value = value,
                TickFrequency = Math.Max(1, Math.Abs(max - min) / 8),
                TickPlacement = TickPlacement.BottomRight,
                SmallChange = Math.Max(1, Math.Abs(max - min) / 100),
                StepFrequency = Math.Max(1, Math.Abs(max - min) / 100),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -4, 0, 0),
            };

            bool _syncing = false;

            // Slider → NumberBox
            slider.ValueChanged += (_, e) => {
                if (_syncing) return;
                _syncing = true;
                numberBox.Value = Math.Round(e.NewValue);      // 取整同步
                _syncing = false;
                RaiseParamsChanged();
            };

            // NumberBox → Slider
            numberBox.ValueChanged += (_, e) => {
                if (_syncing || double.IsNaN(e.NewValue)) return;
                var rounded = Math.Round(e.NewValue);          // 取整
                _syncing = true;
                numberBox.Value = rounded;                     // 回写保证显示整数
                slider.Value = Math.Clamp(rounded, min, max);
                _syncing = false;
                RaiseParamsChanged();
            };

            if (trackBrush != null) {
                slider.TrackFill = trackBrush;
                slider.TrackFillMode = ArcSliderTrackFillMode.Full;
            }

            container.Children.Add(headerGrid);
            container.Children.Add(slider);
            parent.Children.Add(container);
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
        private readonly ArcSlider _slider;

        public SingleSliderEffectPanel(EffectSliderConfig cfg) {
            var root = new StackPanel { Spacing = 4 };
            _slider = CreateSlider(cfg.Label, cfg.Min, cfg.Max, cfg.Default, root);
            Content = root;
        }

        public override EffectParams Params => new() { Value = (float)_slider.Value, Dpi = 96f };
    }

    public sealed partial class DoubleSliderEffectPanel : EffectPanelBase {
        private readonly ArcSlider _slider1;
        private readonly ArcSlider _slider2;

        public DoubleSliderEffectPanel(EffectSliderConfig cfg) {
            var root = new StackPanel { Spacing = 4 };
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

            var presets = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
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
        public EmptyEffectPanel() { }

        public override EffectParams Params => EffectParams.Default;
    }
}
