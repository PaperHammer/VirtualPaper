using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class StraightenEffectPanel : EffectPanelBase {
        public StraightenEffectPanel() {
            this.InitializeComponent();
            UpdateValueText();
        }

        public override EffectParams Params => new() { Value = (float)Slider.Value, Dpi = 96f };

        private void Slider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateValueText();
            RaiseParamsChanged();
        }

        private void UpdateValueText() => ValueText.Text = ((int)Slider.Value).ToString();
    }
}
