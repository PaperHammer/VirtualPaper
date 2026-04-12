using VirtualPaper.Common.Events.EffectValue.Base;

namespace VirtualPaper.PlayerWeb.Core.Utils.Interfaces {
    public interface IEffectService {
        void UpdateEffectValue<T>(EffectValueChanged<T> value);
    }
}
