using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class BlendEffectPanel : EffectPanelBase {
        private double _defaultValue;

        public BlendEffectPanel() {
            this.InitializeComponent();
            _defaultValue = Slider.Value;
            UpdateValueText();
        }

        public override EffectParams Params => new() { Value = (float)Slider.Value, Dpi = 96f };

        public override void Reset() {
            Slider.Value = _defaultValue;
            UpdateValueText();
        }

        private void Slider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateValueText();
            RaiseParamsChanged();
        }

        private void UpdateValueText() => ValueText.Text = ((int)Slider.Value).ToString();
    }
}
