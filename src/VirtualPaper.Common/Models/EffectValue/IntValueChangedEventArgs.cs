using VirtualPaper.Common.Models.EffectValue.Base;

namespace VirtualPaper.Common.Models.EffectValue {
    public class IntValueChangedEventArgs : EffectValueChanged<int> {
        public override int Value { get; set; }
    }
}
