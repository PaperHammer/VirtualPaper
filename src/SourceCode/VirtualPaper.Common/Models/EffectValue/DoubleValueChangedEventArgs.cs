using VirtualPaper.Common.Models.EffectValue.Base;

namespace VirtualPaper.Common.Models.EffectValue {
    public class DoubleValueChangedEventArgs : EffectValueChanged<double> {
        public override double Value { get; set; }
    }
}
