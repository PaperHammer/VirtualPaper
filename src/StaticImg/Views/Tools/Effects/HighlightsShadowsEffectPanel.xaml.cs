using Microsoft.UI;
using VirtualPaper.Shader.Models;
using VirtualPaper.UIComponent.Input;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class HighlightsShadowsEffectPanel : EffectPanelBase {
        private double _defaultShadows, _defaultHighlights, _defaultClarity, _defaultBlur;

        public HighlightsShadowsEffectPanel() {
            this.InitializeComponent();
            _defaultShadows = ShadowsSlider.Value;
            _defaultHighlights = HighlightsSlider.Value;
            _defaultClarity = ClaritySlider.Value;
            _defaultBlur = BlurSlider.Value;
            ShadowsSlider.TrackFill = Gradient((0, Colors.Black), (1, Colors.White));
            ShadowsSlider.TrackFillMode = ArcSliderTrackFillMode.Full;
            HighlightsSlider.TrackFill = Gradient((0, Colors.Black), (1, Colors.White));
            HighlightsSlider.TrackFillMode = ArcSliderTrackFillMode.Full;
            UpdateShadowsText();
            UpdateHighlightsText();
            UpdateClarityText();
            UpdateBlurText();
        }

        public override EffectParams Params => new() {
            Value = (float)ShadowsSlider.Value,
            Value2 = (float)HighlightsSlider.Value,
            Value3 = (float)ClaritySlider.Value,
            Value4 = (float)BlurSlider.Value,
            Dpi = 96f,
        };

        public override void Reset() {
            ShadowsSlider.Value = _defaultShadows;
            HighlightsSlider.Value = _defaultHighlights;
            ClaritySlider.Value = _defaultClarity;
            BlurSlider.Value = _defaultBlur;
            UpdateShadowsText();
            UpdateHighlightsText();
            UpdateClarityText();
            UpdateBlurText();
        }

        private void ShadowsSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateShadowsText();
            RaiseParamsChanged();
        }

        private void HighlightsSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateHighlightsText();
            RaiseParamsChanged();
        }

        private void ClaritySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateClarityText();
            RaiseParamsChanged();
        }

        private void BlurSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateBlurText();
            RaiseParamsChanged();
        }

        private void UpdateShadowsText() => ShadowsValueText.Text = ((int)ShadowsSlider.Value).ToString();
        private void UpdateHighlightsText() => HighlightsValueText.Text = ((int)HighlightsSlider.Value).ToString();
        private void UpdateClarityText() => ClarityValueText.Text = ((int)ClaritySlider.Value).ToString();
        private void UpdateBlurText() => BlurValueText.Text = ((int)BlurSlider.Value).ToString();
    }
}
