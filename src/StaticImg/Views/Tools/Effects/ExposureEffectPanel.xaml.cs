using Microsoft.UI;
using VirtualPaper.Shader.Models;
using VirtualPaper.UIComponent.Input;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class ExposureEffectPanel : EffectPanelBase {
        private double _defaultValue;

        public ExposureEffectPanel() {
            this.InitializeComponent();
            _defaultValue = Slider.Value;
            Slider.TrackFill = Gradient(
                (0, Colors.Black), (0.5, Colors.Gray), (1, Colors.White));
            Slider.TrackFillMode = ArcSliderTrackFillMode.Full;
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
