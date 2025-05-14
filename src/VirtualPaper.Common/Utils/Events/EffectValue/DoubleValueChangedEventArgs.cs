using VirtualPaper.Common.Events.EffectValue.Base;

namespace VirtualPaper.Common.Events.EffectValue {
    public class DoubleValueChangedEventArgs : EffectValueChanged<double> {
        public override double Value { get; set; }
    }
}
