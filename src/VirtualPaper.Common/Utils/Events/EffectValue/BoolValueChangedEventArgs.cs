using VirtualPaper.Common.Events.EffectValue.Base;

namespace VirtualPaper.Common.Events.EffectValue {
    public class BoolValueChangedEventArgs : EffectValueChanged<bool> {
        public override bool Value { get; set; }
    }
}
