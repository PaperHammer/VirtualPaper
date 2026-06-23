using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class EdgeDetectionEffectPanel : EffectPanelBase {
        private double _defaultAmount, _defaultBlur;
        private bool _defaultOverlay;

        public EdgeDetectionEffectPanel() {
            this.InitializeComponent();
            _defaultAmount = AmountSlider.Value;
            _defaultBlur = BlurSlider.Value;
            _defaultOverlay = OverlayToggle.IsOn;
            UpdateAmountText();
            UpdateBlurText();
        }

        public override EffectParams Params => new() {
            Value = (float)AmountSlider.Value,
            Value2 = (float)BlurSlider.Value,
            Flag = OverlayToggle.IsOn,
            Dpi = 96f,
        };

        public override void Reset() {
            AmountSlider.Value = _defaultAmount;
            BlurSlider.Value = _defaultBlur;
            OverlayToggle.IsOn = _defaultOverlay;
            UpdateAmountText();
            UpdateBlurText();
        }

        private void AmountSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateAmountText();
            RaiseParamsChanged();
        }

        private void BlurSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateBlurText();
            RaiseParamsChanged();
        }

        private void OverlayToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            RaiseParamsChanged();
        }

        private void UpdateAmountText() => AmountValueText.Text = ((int)AmountSlider.Value).ToString();
        private void UpdateBlurText() => BlurValueText.Text = ((int)BlurSlider.Value).ToString();
    }
}
