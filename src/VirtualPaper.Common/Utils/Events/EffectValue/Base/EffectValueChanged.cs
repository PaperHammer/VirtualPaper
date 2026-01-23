namespace VirtualPaper.Common.Events.EffectValue.Base {
    public abstract class EffectValueChangedBase : EventArgs {
        public string ControlName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
    }

    public class EffectValueChanged<T> : EffectValueChangedBase {
        public virtual T Value { get; set; }
    }
}
