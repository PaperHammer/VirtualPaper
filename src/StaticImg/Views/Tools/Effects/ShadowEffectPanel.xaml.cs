using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class ShadowEffectPanel : EffectPanelBase {
        private double _defaultBlur, _defaultOffsetX, _defaultOffsetY, _defaultOpacity;

        public ShadowEffectPanel() {
            this.InitializeComponent();
            _defaultBlur = BlurSlider.Value;
            _defaultOffsetX = OffsetXSlider.Value;
            _defaultOffsetY = OffsetYSlider.Value;
            _defaultOpacity = OpacitySlider.Value;
            UpdateBlurText();
            UpdateOffsetXText();
            UpdateOffsetYText();
            UpdateOpacityText();
        }

        public override EffectParams Params => new() {
            Value = (float)BlurSlider.Value,
            Value2 = (float)OffsetXSlider.Value,
            Value3 = (float)OffsetYSlider.Value,
            Value4 = (float)OpacitySlider.Value,
            Dpi = 96f,
        };

        public override void Reset() {
            BlurSlider.Value = _defaultBlur;
            OffsetXSlider.Value = _defaultOffsetX;
            OffsetYSlider.Value = _defaultOffsetY;
            OpacitySlider.Value = _defaultOpacity;
            UpdateBlurText();
            UpdateOffsetXText();
            UpdateOffsetYText();
            UpdateOpacityText();
        }

        private void BlurSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateBlurText();
            RaiseParamsChanged();
        }

        private void OffsetXSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateOffsetXText();
            RaiseParamsChanged();
        }

        private void OffsetYSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateOffsetYText();
            RaiseParamsChanged();
        }

        private void OpacitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateOpacityText();
            RaiseParamsChanged();
        }

        private void UpdateBlurText() => BlurValueText.Text = ((int)BlurSlider.Value).ToString();
        private void UpdateOffsetXText() => OffsetXValueText.Text = ((int)OffsetXSlider.Value).ToString();
        private void UpdateOffsetYText() => OffsetYValueText.Text = ((int)OffsetYSlider.Value).ToString();
        private void UpdateOpacityText() => OpacityValueText.Text = ((int)OpacitySlider.Value).ToString();
    }
}
