using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class FogEffectPanel : EffectPanelBase {
        private double _defaultAmount;
        private int _defaultScale;

        public FogEffectPanel() {
            this.InitializeComponent();
            _defaultAmount = AmountSlider.Value;
            _defaultScale = ScaleCombo.SelectedIndex;
            UpdateAmountText();
        }

        public override EffectParams Params => new() {
            Mode = ScaleCombo.SelectedIndex,
            Value = (float)AmountSlider.Value,
            Dpi = 96f,
        };

        public override void Reset() {
            AmountSlider.Value = _defaultAmount;
            ScaleCombo.SelectedIndex = _defaultScale;
            UpdateAmountText();
        }

        private void ScaleCombo_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e) {
            RaiseParamsChanged();
        }

        private void AmountSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateAmountText();
            RaiseParamsChanged();
        }

        private void UpdateAmountText() => AmountValueText.Text = ((int)AmountSlider.Value).ToString();
    }
}
