using System.Numerics;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Shader;
using VirtualPaper.UIComponent.Input;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class ThresholdEffectPanel : EffectPanelBase {
        private readonly ArcSlider _threshold;

        public ThresholdEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _threshold = CreateSlider("阈值", 0, 300, 150, root);
            Content = root;
        }

        public override EffectParams Params => new() {
            Value = (float)_threshold.Value / 100f,
            Color1 = new Vector4(1, 1, 1, 1),
            Color2 = new Vector4(0, 0, 0, 1),
            Dpi = 96f,
        };
    }

    public sealed partial class RippleEffectPanel : EffectPanelBase {
        private readonly ArcSlider _frequency;
        private readonly ArcSlider _phase;
        private readonly ArcSlider _amplitude;
        private readonly ArcSlider _spread;

        public RippleEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _frequency = CreateSlider("频率", 0, 200, 140, root);
            _phase = CreateSlider("相位", -200, 200, 0, root);
            _amplitude = CreateSlider("振幅", 0, 100, 60, root);
            _spread = CreateSlider("扩散", 1, 100, 1, root);
            Content = root;
        }

        public override EffectParams Params => new() {
            Value = (float)_frequency.Value,
            Value2 = (float)_phase.Value,
            Value3 = (float)_amplitude.Value,
            Value4 = (float)_spread.Value / 100f,
            Dpi = 96f,
        };
    }

    public sealed partial class DisplacementLiquefactionEffectPanel : EffectPanelBase {
        private readonly ArcSlider _radius;
        private readonly ArcSlider _pressure;
        private readonly ComboBox _mode;

        public DisplacementLiquefactionEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _mode = new ComboBox {
                Header = "模式",
                SelectedIndex = 1,
                ItemsSource = new[] { "重置", "推", "挤压", "扩展", "左旋", "右旋" },
            };
            _mode.SelectionChanged += (_, _) => RaiseParamsChanged();
            root.Children.Add(_mode);
            _radius = CreateSlider("半径", 1, 100, 20, root);
            _pressure = CreateSlider("压力", 1, 100, 50, root);
            Content = root;
        }

        public override EffectParams Params => new() {
            Mode = _mode.SelectedIndex,
            Value = (float)_pressure.Value / 100f,
            Value2 = (float)_radius.Value,
            Amount = 512,
            Dpi = 96f,
        };
    }
}
