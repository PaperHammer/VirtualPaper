using Microsoft.UI;
using VirtualPaper.Shader.Models;
using VirtualPaper.UIComponent.Input;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class HueRotationEffectPanel : EffectPanelBase {
        public HueRotationEffectPanel() {
            this.InitializeComponent();
            Slider.TrackFill = Gradient(
                (0, Colors.Red),
                (1d / 6, Colors.Yellow),
                (2d / 6, Colors.Lime),
                (3d / 6, Colors.Cyan),
                (4d / 6, Colors.Blue),
                (5d / 6, Colors.Magenta),
                (1, Colors.Red));
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
