using System;
using VirtualPaper.Shader.Models;
using Workloads.Creation.StaticImg.Utils;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class DoubleSliderEffectPanel : EffectPanelBase {
        public DoubleSliderEffectPanel(EffectSliderConfig cfg) {
            this.InitializeComponent();
            Label1Text.Text = cfg.Label;
            Slider1.Minimum = cfg.Min;
            Slider1.Maximum = cfg.Max;
            Slider1.Value = cfg.Default;
            Slider1.TickFrequency = Math.Max(1, Math.Abs(cfg.Max - cfg.Min) / 8);
            Slider1.SmallChange = Math.Max(1, Math.Abs(cfg.Max - cfg.Min) / 100);
            Slider1.StepFrequency = Math.Max(1, Math.Abs(cfg.Max - cfg.Min) / 100);

            Label2Text.Text = cfg.Label2;
            Slider2.Minimum = cfg.Min2;
            Slider2.Maximum = cfg.Max2;
            Slider2.Value = cfg.Default2;
            Slider2.TickFrequency = Math.Max(1, Math.Abs(cfg.Max2 - cfg.Min2) / 8);
            Slider2.SmallChange = Math.Max(1, Math.Abs(cfg.Max2 - cfg.Min2) / 100);
            Slider2.StepFrequency = Math.Max(1, Math.Abs(cfg.Max2 - cfg.Min2) / 100);

            UpdateValue1Text();
            UpdateValue2Text();
        }

        public override EffectParams Params => new() {
            Value = (float)Slider1.Value,
            Value2 = (float)Slider2.Value,
            Dpi = 96f,
        };

        private void Slider1_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateValue1Text();
            RaiseParamsChanged();
        }

        private void Slider2_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
            UpdateValue2Text();
            RaiseParamsChanged();
        }

        private void UpdateValue1Text() => Value1Text.Text = ((int)Slider1.Value).ToString();
        private void UpdateValue2Text() => Value2Text.Text = ((int)Slider2.Value).ToString();
    }
}
