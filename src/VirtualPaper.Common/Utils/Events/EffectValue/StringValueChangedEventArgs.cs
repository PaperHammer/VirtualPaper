using VirtualPaper.Common.Events.EffectValue.Base;

namespace VirtualPaper.Common.Events.EffectValue {
    public class StringValueChangedEventArgs : EffectValueChanged<string> {
        public override string Value { get; set; } = string.Empty;
    }
}
