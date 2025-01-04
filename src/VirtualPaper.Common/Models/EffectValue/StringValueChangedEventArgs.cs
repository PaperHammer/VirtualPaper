using VirtualPaper.Common.Models.EffectValue.Base;

namespace VirtualPaper.Common.Models.EffectValue {
    public class StringValueChangedEventArgs : EffectValueChanged<string> {
        public override string Value { get; set; }
    }
}
