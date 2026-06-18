using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class DisplacementLiquefactionEffectPanel : EffectPanelBase {
        public DisplacementLiquefactionEffectPanel() {
            this.InitializeComponent();
            UpdateRadiusText();
            UpdatePressureText();
        }

        public override EffectParams Params => new() {
            Mode = ModeCombo.SelectedIndex,
            Value = (float)PressureSlider.Value / 100f,
            Value2 = (float)RadiusSlider.Value,
            Amount = 512,
            Dpi = 96f,
        };

        private void ModeCombo_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e) {
            RaiseParamsChanged();
        }

        private void RadiusSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateRadiusText();
            RaiseParamsChanged();
        }

        private void PressureSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdatePressureText();
            RaiseParamsChanged();
        }

        private void UpdateRadiusText() => RadiusValueText.Text = ((int)RadiusSlider.Value).ToString();
        private void UpdatePressureText() => PressureValueText.Text = ((int)PressureSlider.Value).ToString();
    }
}
