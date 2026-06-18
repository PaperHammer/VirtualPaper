using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class GammaTransferEffectPanel : EffectPanelBase {
        public GammaTransferEffectPanel() {
            this.InitializeComponent();
            UpdateAmplitudeText();
            UpdateExponentText();
            UpdateOffsetText();
        }

        public override EffectParams Params => new() {
            Value = (float)AmplitudeSlider.Value,
            Value2 = (float)ExponentSlider.Value,
            Value3 = (float)OffsetSlider.Value,
            Dpi = 96f,
        };

        private void AmplitudeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateAmplitudeText();
            RaiseParamsChanged();
        }

        private void ExponentSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateExponentText();
            RaiseParamsChanged();
        }

        private void OffsetSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateOffsetText();
            RaiseParamsChanged();
        }

        private void UpdateAmplitudeText() => AmplitudeValueText.Text = ((int)AmplitudeSlider.Value).ToString();
        private void UpdateExponentText() => ExponentValueText.Text = ((int)ExponentSlider.Value).ToString();
        private void UpdateOffsetText() => OffsetValueText.Text = ((int)OffsetSlider.Value).ToString();
    }
}
