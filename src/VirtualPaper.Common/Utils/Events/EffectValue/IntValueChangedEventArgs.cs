using VirtualPaper.Common.Events.EffectValue.Base;

namespace VirtualPaper.Common.Events.EffectValue {
    public class IntValueChangedEventArgs : EffectValueChanged<int> {
        public override int Value { get; set; }
    }
}
