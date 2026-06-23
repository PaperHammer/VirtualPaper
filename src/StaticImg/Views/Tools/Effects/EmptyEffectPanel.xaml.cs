using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class EmptyEffectPanel : EffectPanelBase {
        public EmptyEffectPanel() {
            this.InitializeComponent();
        }

        public override bool IsOneShot => true;
        public override EffectParams Params => EffectParams.Default;
    }
}
