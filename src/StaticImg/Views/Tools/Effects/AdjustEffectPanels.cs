using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Shader.Models;
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
            _hue = CreateGradientSlider("色相", 0, 360, 0, root,
                Gradient(
                    (0,      Colors.Red),
                    (1d/6,  Colors.Yellow),
                    (2d/6,  Colors.Lime),
                    (3d/6,  Colors.Cyan),
                    (4d/6,  Colors.Blue),
                    (5d/6,  Colors.Magenta),
                    (1,      Colors.Red)));
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
                Gradient((0, Colors.MediumVioletRed), (0.5, Colors.White), (1, Colors.MediumSeaGreen)));
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
            var grid = new Grid { ColumnSpacing = 12, RowSpacing = 4 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 第一行：阴影 | 高光
            var c00 = new StackPanel();
            _shadows = CreateGradientSlider("阴影", -100, 100, 0, c00,
                Gradient((0, Colors.Black), (1, Colors.White)));
            Grid.SetRow(c00, 0); Grid.SetColumn(c00, 0);
            grid.Children.Add(c00);

            var c01 = new StackPanel();
            _highlights = CreateGradientSlider("高光", -100, 100, 0, c01,
                Gradient((0, Colors.Black), (1, Colors.White)));
            Grid.SetRow(c01, 0); Grid.SetColumn(c01, 1);
            grid.Children.Add(c01);

            // 第二行：清晰度 | 蒙版模糊
            var c10 = new StackPanel();
            _clarity = CreateSlider("清晰度", -100, 100, 0, c10);
            Grid.SetRow(c10, 1); Grid.SetColumn(c10, 0);
            grid.Children.Add(c10);

            var c11 = new StackPanel();
            _blur = CreateSlider("蒙版模糊", 0, 100, 0, c11);
            Grid.SetRow(c11, 1); Grid.SetColumn(c11, 1);
            grid.Children.Add(c11);

            Content = grid;
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
