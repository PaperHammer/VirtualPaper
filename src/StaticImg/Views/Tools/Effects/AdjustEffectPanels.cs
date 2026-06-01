using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Shader;
using VirtualPaper.UIComponent.Input;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class ExposureEffectPanel : EffectPanelBase {
        private readonly ArcSlider _exposure;

        public ExposureEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _exposure = CreateGradientSlider("曝光", -200, 200, 0, root,
                Gradient((0, Colors.Black), (0.5, Colors.Gray), (1, Colors.White)));
            Content = root;
        }

        public override EffectParams Params => new() { Value = (float)_exposure.Value, Dpi = 96f };
    }

    public sealed partial class SaturationEffectPanel : EffectPanelBase {
        private readonly ArcSlider _saturation;

        public SaturationEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _saturation = CreateGradientSlider("饱和度", 0, 200, 100, root,
                Gradient((0, Colors.Gray), (1, Colors.DeepSkyBlue)));
            Content = root;
        }

        public override EffectParams Params => new() { Value = (float)_saturation.Value, Dpi = 96f };
    }

    public sealed partial class HueRotationEffectPanel : EffectPanelBase {
        private readonly ArcSlider _hue;

        public HueRotationEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _hue = CreateGradientSlider("色相", -180, 180, 0, root,
                Gradient((0, Colors.Red), (0.17, Colors.Yellow), (0.33, Colors.Lime), (0.5, Colors.Cyan), (0.67, Colors.Blue), (0.83, Colors.Magenta), (1, Colors.Red)));
            Content = root;
        }

        public override EffectParams Params => new() { Value = (float)_hue.Value, Dpi = 96f };
    }

    public sealed partial class ContrastEffectPanel : EffectPanelBase {
        private readonly ArcSlider _contrast;

        public ContrastEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _contrast = CreateGradientSlider("对比度", -100, 100, 0, root,
                Gradient((0, Colors.Gray), (0.5, Colors.WhiteSmoke), (1, Colors.Black)));
            Content = root;
        }

        public override EffectParams Params => new() { Value = (float)_contrast.Value, Dpi = 96f };
    }

    public sealed partial class TemperatureTintEffectPanel : EffectPanelBase {
        private readonly ArcSlider _temperature;
        private readonly ArcSlider _tint;

        public TemperatureTintEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _temperature = CreateGradientSlider("色温", -100, 100, 0, root,
                Gradient((0, Colors.DodgerBlue), (0.5, Colors.White), (1, Colors.Orange)));
            _tint = CreateGradientSlider("色调", -100, 100, 0, root,
                Gradient((0, Colors.MediumSeaGreen), (0.5, Colors.White), (1, Colors.MediumVioletRed)));
            Content = root;
        }

        public override EffectParams Params => new() {
            Value = (float)_temperature.Value,
            Value2 = (float)_tint.Value,
            Dpi = 96f,
        };
    }

    public sealed partial class HighlightsShadowsEffectPanel : EffectPanelBase {
        private readonly ArcSlider _shadows;
        private readonly ArcSlider _highlights;
        private readonly ArcSlider _clarity;
        private readonly ArcSlider _blur;

        public HighlightsShadowsEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _shadows = CreateGradientSlider("阴影", -100, 100, 0, root,
                Gradient((0, Colors.Black), (1, Colors.White)));
            _highlights = CreateGradientSlider("高光", -100, 100, 0, root,
                Gradient((0, Colors.Black), (1, Colors.White)));
            _clarity = CreateSlider("清晰度", -100, 100, 0, root);
            _blur = CreateSlider("蒙版模糊", 0, 100, 0, root);
            Content = root;
        }

        public override EffectParams Params => new() {
            Value = (float)_shadows.Value,
            Value2 = (float)_highlights.Value,
            Value3 = (float)_clarity.Value,
            Value4 = (float)_blur.Value,
            Dpi = 96f,
        };
    }
}
