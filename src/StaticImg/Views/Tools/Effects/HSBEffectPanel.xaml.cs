using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class HSBEffectPanel : EffectPanelBase {
        public HSBEffectPanel() {
            this.InitializeComponent();
            UpdateHueText();
            UpdateSaturationText();
            UpdateBrightnessText();
        }

        public override EffectParams Params => new() {
            Value = (float)HueSlider.Value,
            Value2 = (float)SaturationSlider.Value,
            Value3 = (float)BrightnessSlider.Value,
            Dpi = 96f,
        };

        private void HueSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateHueText();
            RaiseParamsChanged();
        }

        private void SaturationSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateSaturationText();
            RaiseParamsChanged();
        }

        private void BrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateBrightnessText();
            RaiseParamsChanged();
        }

        private void UpdateHueText() => HueValueText.Text = ((int)HueSlider.Value).ToString() + "°";
        private void UpdateSaturationText() => SaturationValueText.Text = ((int)SaturationSlider.Value).ToString() + "%";
        private void UpdateBrightnessText() => BrightnessValueText.Text = ((int)BrightnessSlider.Value).ToString();
    }
}
