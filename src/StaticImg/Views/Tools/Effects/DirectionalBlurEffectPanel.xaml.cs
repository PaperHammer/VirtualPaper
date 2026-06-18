using Microsoft.UI;
using VirtualPaper.Shader.Models;
using VirtualPaper.UIComponent.Input;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class DirectionalBlurEffectPanel : EffectPanelBase {
        public DirectionalBlurEffectPanel() {
            this.InitializeComponent();
            AngleSlider.TrackFill = Gradient(
                (0, Colors.Red), (0.25, Colors.Yellow), (0.5, Colors.Cyan), (0.75, Colors.Blue), (1, Colors.Red));
            AngleSlider.TrackFillMode = ArcSliderTrackFillMode.Full;
            UpdateBlurText();
            UpdateAngleText();
        }

        public override EffectParams Params => new() {
            Value = (float)BlurSlider.Value,
            Value2 = (float)AngleSlider.Value,
            Dpi = 96f,
        };

        private void BlurSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateBlurText();
            RaiseParamsChanged();
        }

        private void AngleSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateAngleText();
            RaiseParamsChanged();
        }

        private void UpdateBlurText() => BlurValueText.Text = ((int)BlurSlider.Value).ToString();
        private void UpdateAngleText() => AngleValueText.Text = ((int)AngleSlider.Value).ToString();
    }
}
