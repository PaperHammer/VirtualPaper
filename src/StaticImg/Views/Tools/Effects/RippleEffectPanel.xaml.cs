using System.Numerics;
using Microsoft.UI.Xaml;
using VirtualPaper.Shader.Models;
using Workloads.Creation.StaticImg;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class RippleEffectPanel : EffectPanelBase {
        private double _defaultFrequency, _defaultPhase, _defaultAmplitude, _defaultSpread;

        public RippleEffectPanel() {
            this.InitializeComponent();
            _defaultFrequency = FrequencySlider.Value;
            _defaultPhase = PhaseSlider.Value;
            _defaultAmplitude = AmplitudeSlider.Value;
            _defaultSpread = SpreadSlider.Value;
            UpdateFrequencyText();
            UpdatePhaseText();
            UpdateAmplitudeText();
            UpdateSpreadText();
        }

        public static readonly DependencyProperty CanvasSizeProperty = DependencyProperty.Register(
            nameof(CanvasSize), typeof(ArcSize), typeof(RippleEffectPanel), new PropertyMetadata(new ArcSize(1000, 1000, 96, RebuildMode.None)));

        public ArcSize CanvasSize {
            get => (ArcSize)GetValue(CanvasSizeProperty);
            set => SetValue(CanvasSizeProperty, value);
        }

        public override EffectParams Params => new() {
            Value = (float)FrequencySlider.Value,
            Value2 = (float)PhaseSlider.Value,
            Value3 = (float)AmplitudeSlider.Value,
            Value4 = (float)SpreadSlider.Value / 100f,
            Point1 = new Vector2(CanvasSize.Width / 2, CanvasSize.Height / 2),
            Dpi = 96f,
        };

        public override void Reset() {
            FrequencySlider.Value = _defaultFrequency;
            PhaseSlider.Value = _defaultPhase;
            AmplitudeSlider.Value = _defaultAmplitude;
            SpreadSlider.Value = _defaultSpread;
            UpdateFrequencyText();
            UpdatePhaseText();
            UpdateAmplitudeText();
            UpdateSpreadText();
        }

        private void FrequencySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateFrequencyText();
            RaiseParamsChanged();
        }

        private void PhaseSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdatePhaseText();
            RaiseParamsChanged();
        }

        private void AmplitudeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateAmplitudeText();
            RaiseParamsChanged();
        }

        private void SpreadSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateSpreadText();
            RaiseParamsChanged();
        }

        private void UpdateFrequencyText() => FrequencyValueText.Text = ((int)FrequencySlider.Value).ToString();
        private void UpdatePhaseText() => PhaseValueText.Text = ((int)PhaseSlider.Value).ToString();
        private void UpdateAmplitudeText() => AmplitudeValueText.Text = ((int)AmplitudeSlider.Value).ToString();
        private void UpdateSpreadText() => SpreadValueText.Text = ((int)SpreadSlider.Value).ToString();
    }
}
