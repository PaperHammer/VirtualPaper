using Microsoft.UI;
using VirtualPaper.Shader.Models;
using VirtualPaper.UIComponent.Input;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class TemperatureTintEffectPanel : EffectPanelBase {
        private double _defaultTemp, _defaultTint;

        public TemperatureTintEffectPanel() {
            this.InitializeComponent();
            _defaultTemp = TemperatureSlider.Value;
            _defaultTint = TintSlider.Value;
            TemperatureSlider.TrackFill = Gradient((0, Colors.DodgerBlue), (0.5, Colors.White), (1, Colors.Orange));
            TemperatureSlider.TrackFillMode = ArcSliderTrackFillMode.Full;
            TintSlider.TrackFill = Gradient((0, Colors.MediumVioletRed), (0.5, Colors.White), (1, Colors.MediumSeaGreen));
            TintSlider.TrackFillMode = ArcSliderTrackFillMode.Full;
            UpdateTempText();
            UpdateTintText();
        }

        public override EffectParams Params => new() {
            Value = (float)TemperatureSlider.Value,
            Value2 = (float)TintSlider.Value,
            Dpi = 96f,
        };

        public override void Reset() {
            TemperatureSlider.Value = _defaultTemp;
            TintSlider.Value = _defaultTint;
            UpdateTempText();
            UpdateTintText();
        }

        private void TemperatureSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateTempText();
            RaiseParamsChanged();
        }

        private void TintSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateTintText();
            RaiseParamsChanged();
        }

        private void UpdateTempText() => TempValueText.Text = ((int)TemperatureSlider.Value).ToString();
        private void UpdateTintText() => TintValueText.Text = ((int)TintSlider.Value).ToString();
    }
}
