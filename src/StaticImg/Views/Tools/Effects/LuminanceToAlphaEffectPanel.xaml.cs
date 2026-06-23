using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class LuminanceToAlphaEffectPanel : EffectPanelBase {
        private int _defaultMode;

        public LuminanceToAlphaEffectPanel() {
            this.InitializeComponent();
            _defaultMode = ModeCombo.SelectedIndex;
        }

        public override EffectParams Params => new() {
            Mode = ModeCombo.SelectedIndex,
            Dpi = 96f,
        };

        public override void Reset() {
            ModeCombo.SelectedIndex = _defaultMode;
        }

        private void ModeCombo_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e) {
            RaiseParamsChanged();
        }
    }
}
