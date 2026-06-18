using Microsoft.UI;
using VirtualPaper.Shader.Models;
using VirtualPaper.UIComponent.Input;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class ColouringEffectPanel : EffectPanelBase {
        public ColouringEffectPanel() {
            this.InitializeComponent();
            Slider.TrackFill = Gradient(
                (0, Colors.Red), (0.17, Colors.Yellow), (0.33, Colors.Lime),
                (0.5, Colors.Cyan), (0.67, Colors.Blue), (0.83, Colors.Magenta), (1, Colors.Red));
            Slider.TrackFillMode = ArcSliderTrackFillMode.Full;
            UpdateValueText();
        }

        public override EffectParams Params => new() { Value = (float)Slider.Value, Dpi = 96f };

        private void Slider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateValueText();
            RaiseParamsChanged();
        }

        private void UpdateValueText() => ValueText.Text = ((int)Slider.Value).ToString() + "°";
    }
}
