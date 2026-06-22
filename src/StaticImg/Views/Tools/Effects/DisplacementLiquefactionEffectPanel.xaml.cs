using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class DisplacementLiquefactionEffectPanel : EffectPanelBase {
        private double _defaultRadius, _defaultPressure;
        private int _defaultMode;

        public DisplacementLiquefactionEffectPanel() {
            this.InitializeComponent();
            _defaultRadius = RadiusSlider.Value;
            _defaultPressure = PressureSlider.Value;
            _defaultMode = ModeCombo.SelectedIndex;
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

        public override void Reset() {
            RadiusSlider.Value = _defaultRadius;
            PressureSlider.Value = _defaultPressure;
            ModeCombo.SelectedIndex = _defaultMode;
            UpdateRadiusText();
            UpdatePressureText();
        }

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
