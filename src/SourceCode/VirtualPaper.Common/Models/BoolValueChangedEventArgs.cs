namespace VirtualPaper.Common.Models
{
    public class BoolValueChangedEventArgs : EventArgs
    {
        public string ControlName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public bool Value { get; set; }
    }
}
