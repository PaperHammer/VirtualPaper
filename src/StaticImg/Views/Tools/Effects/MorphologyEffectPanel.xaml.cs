using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class MorphologyEffectPanel : EffectPanelBase {
        private double _defaultWidth, _defaultHeight;
        private int _defaultMode;

        public MorphologyEffectPanel() {
            this.InitializeComponent();
            _defaultWidth = WidthSlider.Value;
            _defaultHeight = HeightSlider.Value;
            _defaultMode = ModeCombo.SelectedIndex;
            UpdateWidthText();
            UpdateHeightText();
        }

        public override EffectParams Params => new() {
            Mode = ModeCombo.SelectedIndex,
            Value = (float)WidthSlider.Value,
            Value2 = (float)HeightSlider.Value,
            Dpi = 96f,
        };

        public override void Reset() {
            WidthSlider.Value = _defaultWidth;
            HeightSlider.Value = _defaultHeight;
            ModeCombo.SelectedIndex = _defaultMode;
            UpdateWidthText();
            UpdateHeightText();
        }

        private void ModeCombo_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e) {
            RaiseParamsChanged();
        }

        private void WidthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateWidthText();
            RaiseParamsChanged();
        }

        private void HeightSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateHeightText();
            RaiseParamsChanged();
        }

        private void UpdateWidthText() => WidthValueText.Text = ((int)WidthSlider.Value).ToString();
        private void UpdateHeightText() => HeightValueText.Text = ((int)HeightSlider.Value).ToString();
    }
}
