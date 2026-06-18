using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class RippleEffectPanel : EffectPanelBase {
        public RippleEffectPanel() {
            this.InitializeComponent();
            UpdateFrequencyText();
            UpdatePhaseText();
            UpdateAmplitudeText();
            UpdateSpreadText();
        }

        public override EffectParams Params => new() {
            Value = (float)FrequencySlider.Value,
            Value2 = (float)PhaseSlider.Value,
            Value3 = (float)AmplitudeSlider.Value,
            Value4 = (float)SpreadSlider.Value / 100f,
            Dpi = 96f,
        };

        private void FrequencySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateFrequencyText();
            RaiseParamsChanged();
        }

        private void PhaseSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdatePhaseText();
            RaiseParamsChanged();
        }

        private void AmplitudeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateAmplitudeText();
            RaiseParamsChanged();
        }

        private void SpreadSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateSpreadText();
            RaiseParamsChanged();
        }

        private void UpdateFrequencyText() => FrequencyValueText.Text = ((int)FrequencySlider.Value).ToString();
        private void UpdatePhaseText() => PhaseValueText.Text = ((int)PhaseSlider.Value).ToString();
        private void UpdateAmplitudeText() => AmplitudeValueText.Text = ((int)AmplitudeSlider.Value).ToString();
        private void UpdateSpreadText() => SpreadValueText.Text = ((int)SpreadSlider.Value).ToString();
    }
}
