using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class GlassEffectPanel : EffectPanelBase {
        public GlassEffectPanel() {
            this.InitializeComponent();
            UpdateAmountText();
        }

        public override EffectParams Params => new() {
            Mode = ScaleCombo.SelectedIndex,
            Value = (float)AmountSlider.Value,
            Dpi = 96f,
        };

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
