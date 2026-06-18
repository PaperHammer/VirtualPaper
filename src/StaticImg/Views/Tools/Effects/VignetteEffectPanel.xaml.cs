using Microsoft.UI;
using VirtualPaper.Shader.Models;
using VirtualPaper.UIComponent.Input;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class VignetteEffectPanel : EffectPanelBase {
        public VignetteEffectPanel() {
            this.InitializeComponent();
            Slider.TrackFill = Gradient((0, Colors.White), (1, Colors.Black));
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
