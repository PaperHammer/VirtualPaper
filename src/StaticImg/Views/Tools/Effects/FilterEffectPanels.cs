using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Shader;
using VirtualPaper.UIComponent.Input;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class BlurEffectPanel : EffectPanelBase {
        private readonly ArcSlider _blur;

        public BlurEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _blur = CreateSlider("模糊", 0, 100, 0, root);
            Content = root;
        }

        public override EffectParams Params => new() { Value = (float)_blur.Value, Dpi = 96f };
    }

    public sealed partial class DirectionalBlurEffectPanel : EffectPanelBase {
        private readonly ArcSlider _blur;
        private readonly ArcSlider _angle;

        public DirectionalBlurEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _blur = CreateSlider("模糊", 0, 100, 0, root);
            _angle = CreateGradientSlider("角度", 0, 360, 0, root,
                Gradient((0, Colors.Red), (0.25, Colors.Yellow), (0.5, Colors.Cyan), (0.75, Colors.Blue), (1, Colors.Red)));
            Content = root;
        }

        public override EffectParams Params => new() { Value = (float)_blur.Value, Value2 = (float)_angle.Value, Dpi = 96f };
    }

    public sealed partial class SharpenEffectPanel : EffectPanelBase {
        private readonly ArcSlider _amount;

        public SharpenEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _amount = CreateSlider("锐化", 0, 100, 0, root);
            Content = root;
        }

        public override EffectParams Params => new() { Value = (float)_amount.Value, Dpi = 96f };
    }

    public sealed partial class VignetteEffectPanel : EffectPanelBase {
        private readonly ArcSlider _amount;

        public VignetteEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _amount = CreateGradientSlider("暗角", 0, 100, 0, root,
                Gradient((0, Colors.White), (1, Colors.Black)));
            Content = root;
        }

        public override EffectParams Params => new() { Value = (float)_amount.Value, Dpi = 96f };
    }

    public sealed partial class EmbossEffectPanel : EffectPanelBase {
        private readonly ArcSlider _amount;
        private readonly ArcSlider _angle;

        public EmbossEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _amount = CreateSlider("强度", 0, 100, 0, root);
            _angle = CreateGradientSlider("角度", 0, 360, 45, root,
                Gradient((0, Colors.Red), (0.25, Colors.Yellow), (0.5, Colors.Cyan), (0.75, Colors.Blue), (1, Colors.Red)));
            Content = root;
        }

        public override EffectParams Params => new() { Value = (float)_amount.Value, Value2 = (float)_angle.Value, Dpi = 96f };
    }

    public sealed partial class PosterizeEffectPanel : EffectPanelBase {
        private readonly ArcSlider _level;

        public PosterizeEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _level = CreateSlider("色阶", 2, 256, 256, root);
            Content = root;
        }

        public override EffectParams Params => new() {
            Value = (float)_level.Value,
            Value2 = (float)_level.Value,
            Value3 = (float)_level.Value,
            Dpi = 96f,
        };
    }

    public sealed partial class ShadowEffectPanel : EffectPanelBase {
        private readonly ArcSlider _blur;
        private readonly ArcSlider _offsetX;
        private readonly ArcSlider _offsetY;
        private readonly ArcSlider _opacity;

        public ShadowEffectPanel() {
            var root = new StackPanel { Spacing = 8 };
            _blur = CreateSlider("模糊", 0, 100, 0, root);
            _offsetX = CreateSlider("偏移X", -200, 200, 0, root);
            _offsetY = CreateSlider("偏移Y", -200, 200, 0, root);
            _opacity = CreateSlider("透明度", 0, 100, 0, root);
            Content = root;
        }

        public override EffectParams Params => new() {
            Value = (float)_blur.Value,
            Value2 = (float)_offsetX.Value,
            Value3 = (float)_offsetY.Value,
            Value4 = (float)_opacity.Value,
            Dpi = 96f,
        };
    }
}
