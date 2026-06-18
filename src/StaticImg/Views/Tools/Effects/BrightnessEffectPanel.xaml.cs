using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class BrightnessEffectPanel : EffectPanelBase {
        private readonly float[] _rTable = new float[256];
        private readonly float[] _gTable = new float[256];
        private readonly float[] _bTable = new float[256];

        public BrightnessEffectPanel() {
            this.InitializeComponent();
            Curve.CurveChanged += (_, lut) => {
                for (int i = 0; i < 256; i++) {
                    _rTable[i] = (float)lut[i];
                    _gTable[i] = (float)lut[i];
                    _bTable[i] = (float)lut[i];
                }
                RaiseParamsChanged();
            };
            ResetToIdentity();
        }

        public override EffectParams Params => new() {
            RedTable = _rTable,
            GreenTable = _gTable,
            BlueTable = _bTable,
            AlphaTable = null,
            Dpi = 96f,
        };

        private void ResetToIdentity() {
            for (int i = 0; i < 256; i++) {
                float v = i / 255f;
                _rTable[i] = v;
                _gTable[i] = v;
                _bTable[i] = v;
            }
        }
    }
}
