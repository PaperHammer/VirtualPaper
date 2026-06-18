using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class BrightnessEffectPanel : EffectPanelBase {
        public BrightnessEffectPanel() {
            this.InitializeComponent();
            UpdateBlackText();
            UpdateWhiteText();
        }

        public override EffectParams Params => new() {
            Value = (float)BlackSlider.Value,
            Value2 = (float)WhiteSlider.Value,
            Dpi = 96f,
        };

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
