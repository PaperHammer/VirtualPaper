using System.Numerics;
using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class ThresholdEffectPanel : EffectPanelBase {
        public ThresholdEffectPanel() {
            this.InitializeComponent();
            UpdateValueText();
        }

        public override EffectParams Params => new() {
            Value = (float)Slider.Value / 100f,
            Color1 = new Vector4(1, 1, 1, 1),
            Color2 = new Vector4(0, 0, 0, 1),
            Dpi = 96f,
        };

        private void Slider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateValueText();
            RaiseParamsChanged();
        }

        private void UpdateValueText() => ValueText.Text = ((int)Slider.Value).ToString();
    }
}
