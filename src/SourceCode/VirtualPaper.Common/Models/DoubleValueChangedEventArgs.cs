namespace VirtualPaper.Common.Models
{
    public class DoubleValueChangedEventArgs : EventArgs
    {
        public string ControlName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public double Value { get; set; }
    }
}
