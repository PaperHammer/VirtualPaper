using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class ChromaKeyEffectPanel : EffectPanelBase {
        public ChromaKeyEffectPanel() {
            this.InitializeComponent();
            UpdateToleranceText();
        }

        public override EffectParams Params => new() {
            Value = (float)ToleranceSlider.Value,
            Flag = InvertToggle.IsOn,
            Mode = FeatherToggle.IsOn ? 1 : 0,
            Dpi = 96f,
        };

        private void ToleranceSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateToleranceText();
            RaiseParamsChanged();
        }

        private void InvertToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            RaiseParamsChanged();
        }

        private void FeatherToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            RaiseParamsChanged();
        }

        private void UpdateToleranceText() => ToleranceValueText.Text = ((int)ToleranceSlider.Value).ToString();
    }
}
