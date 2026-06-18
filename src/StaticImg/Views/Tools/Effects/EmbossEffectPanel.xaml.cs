using Microsoft.UI;
using VirtualPaper.Shader.Models;
using VirtualPaper.UIComponent.Input;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class EmbossEffectPanel : EffectPanelBase {
        public EmbossEffectPanel() {
            this.InitializeComponent();
            AngleSlider.TrackFill = Gradient(
                (0, Colors.Red), (0.25, Colors.Yellow), (0.5, Colors.Cyan), (0.75, Colors.Blue), (1, Colors.Red));
            AngleSlider.TrackFillMode = ArcSliderTrackFillMode.Full;
            UpdateAmountText();
            UpdateAngleText();
        }

        public override EffectParams Params => new() {
            Value = (float)AmountSlider.Value,
            Value2 = (float)AngleSlider.Value,
            Dpi = 96f,
        };

        private void AmountSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateAmountText();
            RaiseParamsChanged();
        }

        private void AngleSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateAngleText();
            RaiseParamsChanged();
        }

        private void UpdateAmountText() => AmountValueText.Text = ((int)AmountSlider.Value).ToString();
        private void UpdateAngleText() => AngleValueText.Text = ((int)AngleSlider.Value).ToString();
    }
}
