using VirtualPaper.Common.Events.EffectValue.Base;

namespace VirtualPaper.PlayerWeb.Core.Utils.Interfaces {
    public interface IEffectService {
        void Close();
        void UpdateEffectValue(EffectValueChanged<double> value);
        void UpdateEffectValue(EffectValueChanged<int> value);
        void UpdateEffectValue(EffectValueChanged<bool> value);
        void UpdateEffectValue(EffectValueChanged<string> value);
    }
}
