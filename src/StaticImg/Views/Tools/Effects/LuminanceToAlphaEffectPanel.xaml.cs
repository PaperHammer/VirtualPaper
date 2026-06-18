using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class LuminanceToAlphaEffectPanel : EffectPanelBase {
        public LuminanceToAlphaEffectPanel() {
            this.InitializeComponent();
        }

        public override EffectParams Params => new() {
            Mode = ModeCombo.SelectedIndex,
            Dpi = 96f,
        };

        private void ModeCombo_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e) {
            RaiseParamsChanged();
        }
    }
}
