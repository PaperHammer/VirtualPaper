using VirtualPaper.Common.Models.EffectValue;

namespace VirtualPaper.Common.Utils.ObserverMode {
    public interface ICustomizeValueChangedObserver {
        void OnEffectValueChanged(object sender, IntValueChangedEventArgs args);
        void OnEffectValueChanged(object sender, DoubleValueChangedEventArgs args);
        void OnEffectValueChanged(object sender, BoolValueChangedEventArgs args);
        void OnEffectValueChanged(object sender, StringValueChangedEventArgs args);
    }
}
