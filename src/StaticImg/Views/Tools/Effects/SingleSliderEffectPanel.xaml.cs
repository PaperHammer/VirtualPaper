using System;
using VirtualPaper.Shader.Models;
using Workloads.Creation.StaticImg.Utils;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class SingleSliderEffectPanel : EffectPanelBase {
        public SingleSliderEffectPanel(EffectSliderConfig cfg) {
            this.InitializeComponent();
            LabelText.Text = cfg.Label;
            Slider.Minimum = cfg.Min;
            Slider.Maximum = cfg.Max;
            Slider.Value = cfg.Default;
            Slider.TickFrequency = Math.Max(1, Math.Abs(cfg.Max - cfg.Min) / 8);
            Slider.SmallChange = Math.Max(1, Math.Abs(cfg.Max - cfg.Min) / 100);
            Slider.StepFrequency = Math.Max(1, Math.Abs(cfg.Max - cfg.Min) / 100);
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
