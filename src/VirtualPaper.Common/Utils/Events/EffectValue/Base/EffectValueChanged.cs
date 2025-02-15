namespace VirtualPaper.Common.Events.EffectValue.Base {
    public class EffectValueChanged<T> : EventArgs {
        public string ControlName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public virtual T Value { get; set; }
    }
}
