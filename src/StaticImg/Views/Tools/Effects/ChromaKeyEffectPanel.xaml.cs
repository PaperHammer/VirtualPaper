using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class ChromaKeyEffectPanel : EffectPanelBase {
        private double _defaultTolerance;
        private bool _defaultInvert, _defaultFeather;

        public ChromaKeyEffectPanel() {
            this.InitializeComponent();
            _defaultTolerance = ToleranceSlider.Value;
            _defaultInvert = InvertToggle.IsOn;
            _defaultFeather = FeatherToggle.IsOn;
            UpdateToleranceText();
        }

        public override EffectParams Params => new() {
            Value = (float)ToleranceSlider.Value,
            Flag = InvertToggle.IsOn,
            Mode = FeatherToggle.IsOn ? 1 : 0,
            Dpi = 96f,
        };

        public override void Reset() {
            ToleranceSlider.Value = _defaultTolerance;
            InvertToggle.IsOn = _defaultInvert;
            FeatherToggle.IsOn = _defaultFeather;
            UpdateToleranceText();
        }

        private void ToleranceSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateToleranceText();
            RaiseParamsChanged();
        }

        private void InvertToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            RaiseParamsChanged();
        }

        private void FeatherToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            RaiseParamsChanged();
        }

        private void UpdateToleranceText() => ToleranceValueText.Text = ((int)ToleranceSlider.Value).ToString();
    }
}
