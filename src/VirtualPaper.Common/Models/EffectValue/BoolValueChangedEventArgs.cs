using VirtualPaper.Common.Models.EffectValue.Base;

namespace VirtualPaper.Common.Models.EffectValue {
    public class BoolValueChangedEventArgs : EffectValueChanged<bool> {
        public override bool Value { get; set; }
    }
}
