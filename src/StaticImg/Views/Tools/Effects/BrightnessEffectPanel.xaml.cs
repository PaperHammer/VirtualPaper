using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class BrightnessEffectPanel : EffectPanelBase {
        private double _defaultBlack, _defaultWhite;

        public BrightnessEffectPanel() {
            this.InitializeComponent();
            _defaultBlack = BlackSlider.Value;
            _defaultWhite = WhiteSlider.Value;
            UpdateBlackText();
            UpdateWhiteText();
        }

        public override EffectParams Params => new() {
            Value = (float)BlackSlider.Value,
            Value2 = (float)WhiteSlider.Value,
            Dpi = 96f,
        };

        public override void Reset() {
            BlackSlider.Value = _defaultBlack;
            WhiteSlider.Value = _defaultWhite;
            UpdateBlackText();
            UpdateWhiteText();
        }

        private void BlackSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateBlackText();
            RaiseParamsChanged();
        }

        private void WhiteSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateWhiteText();
            RaiseParamsChanged();
        }

        private void UpdateBlackText() => BlackValueText.Text = ((int)BlackSlider.Value).ToString();
        private void UpdateWhiteText() => WhiteValueText.Text = ((int)WhiteSlider.Value).ToString();
    }
}
