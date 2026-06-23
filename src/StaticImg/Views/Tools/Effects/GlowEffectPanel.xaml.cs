using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class GlowEffectPanel : EffectPanelBase {
        private double _defaultBlur, _defaultOpacity;

        public GlowEffectPanel() {
            this.InitializeComponent();
            _defaultBlur = BlurSlider.Value;
            _defaultOpacity = OpacitySlider.Value;
            UpdateBlurText();
            UpdateOpacityText();
        }

        public override EffectParams Params {
            get {
                if (BlurSlider == null || OpacitySlider == null) return EffectParams.Default;
                return new EffectParams {
                    Value = (float)BlurSlider.Value,
                    Value2 = 0,  // OffsetX = 0 (centered)
                    Value3 = 0,  // OffsetY = 0 (centered)
                    Value4 = (float)OpacitySlider.Value,
                    Dpi = 96f,
                };
            }
        }

        public override void Reset() {
            if (BlurSlider == null || OpacitySlider == null) return;
            BlurSlider.Value = _defaultBlur;
            OpacitySlider.Value = _defaultOpacity;
            UpdateBlurText();
            UpdateOpacityText();
        }

        private void Slider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateBlurText();
            UpdateOpacityText();
            RaiseParamsChanged();
        }

        private void UpdateBlurText() {
            if (BlurSlider == null) return;
            BlurValueText.Text = ((int)BlurSlider.Value).ToString();
        }

        private void UpdateOpacityText() {
            if (OpacitySlider == null) return;
            OpacityValueText.Text = ((int)OpacitySlider.Value).ToString();
        }
    }
}
