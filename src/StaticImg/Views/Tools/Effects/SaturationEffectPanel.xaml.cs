using Microsoft.UI;
using VirtualPaper.Shader.Models;
using VirtualPaper.UIComponent.Input;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class SaturationEffectPanel : EffectPanelBase {
        public SaturationEffectPanel() {
            this.InitializeComponent();
            Slider.TrackFill = Gradient((0, Colors.Gray), (1, Colors.DeepSkyBlue));
            Slider.TrackFillMode = ArcSliderTrackFillMode.Full;
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
