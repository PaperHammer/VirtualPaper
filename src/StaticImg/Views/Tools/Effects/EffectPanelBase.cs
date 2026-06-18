using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using VirtualPaper.Shader.Models;
using VirtualPaper.UIComponent.Input;
using Workloads.Creation.StaticImg.Utils;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    /// <summary>效果参数面板基类</summary>
    public abstract class EffectPanelBase : UserControl {
        public event EventHandler<EffectParams>? ParamsChanged;

        public abstract EffectParams Params { get; }

        /// <summary>
        /// true 表示此效果无需参数、点击即生效（如灰度、反相）
        /// ShowEffectPanel 检测到此标志后会立即调用一次 UpdateParams
        /// </summary>
        public virtual bool IsOneShot => false;

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
        private readonly BrightnessCurvePanel _curve;
        // 预分配数组，避免拖动时频繁 GC
        private readonly float[] _rTable = new float[256];
        private readonly float[] _gTable = new float[256];
        private readonly float[] _bTable = new float[256];

        public BrightnessEffectPanel() {
            var root = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Stretch, };

            _curve = new BrightnessCurvePanel {
                Width = 340,
                Height = 200,
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _curve.CurveChanged += (_, lut) => {
                for (int i = 0; i < 256; i++) {
                    _rTable[i] = (float)lut[i];
                    _gTable[i] = (float)lut[i];
                    _bTable[i] = (float)lut[i];
                }
            };

            root.Children.Add(_curve);

            Content = root;

            // 初始化默认恒等曲线
            ResetToIdentity();
        }

        public override EffectParams Params => new() {
            RedTable = _rTable,
            GreenTable = _gTable,
            BlueTable = _bTable,
            AlphaTable = null, // Alpha 不受亮度曲线影响
            Dpi = 96f
        };

        private void ResetToIdentity() {
            for (int i = 0; i < 256; i++) {
                float v = i / 255f;
                _rTable[i] = v;
                _gTable[i] = v;
                _bTable[i] = v;
            }
        }
    }

    public sealed partial class EmptyEffectPanel : EffectPanelBase {
        public EmptyEffectPanel() {
            Content = new TextBlock {
                Text = "预览中",
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
        }

        public override bool IsOneShot => true;
        public override EffectParams Params => EffectParams.Default;
    }
}
